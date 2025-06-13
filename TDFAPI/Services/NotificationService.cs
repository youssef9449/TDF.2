using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using TDFAPI.Data;
using TDFAPI.Repositories;
using TDFAPI.Messaging;
using TDFAPI.Messaging.Interfaces;
using TDFShared.DTOs.Messages;
using TDFShared.DTOs.Common;
using TDFShared.Enums;
using TDFShared.Models.Message;
using TDFShared.Models.Notification;
using TDFShared.Services;
using TDFAPI.Models;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;

namespace TDFAPI.Services
{
    public class NotificationService : INotificationService
    {
        private readonly INotificationRepository _notificationRepository;
        private readonly IUserRepository _userRepository;
        private readonly WebSocketConnectionManager _webSocketManager;
        private readonly ILogger<NotificationService> _logger;
        private readonly IMessageRepository _messageRepository;
        private readonly IEventMediator _eventMediator;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ConcurrentDictionary<int, (DateTime LastMessage, int MessageCount)> _messageRateLimits =
            new();
        private readonly ConcurrentDictionary<string, DateTime> _lastMessageTime = new();
        private readonly Timer _dictionaryCleanupTimer;
        private readonly IPushTokenService _pushTokenService;
        private readonly ApplicationDbContext _context;
        private readonly IBackgroundJobService _jobService;

        private const int MaxMessagesPerMinute = 60;
        private const int DictionaryCleanupIntervalMinutes = 10;

        public NotificationService(
            INotificationRepository notificationRepository,
            IUserRepository userRepository,
            WebSocketConnectionManager webSocketManager,
            IMessageRepository messageRepository,
            ILogger<NotificationService> logger,
            IEventMediator eventMediator,
            IUnitOfWork unitOfWork,
            IHttpContextAccessor httpContextAccessor,
            IPushTokenService pushTokenService,
            ApplicationDbContext context,
            IBackgroundJobService jobService)
        {
            _notificationRepository = notificationRepository ?? throw new ArgumentNullException(nameof(notificationRepository));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _webSocketManager = webSocketManager ?? throw new ArgumentNullException(nameof(webSocketManager));
            _messageRepository = messageRepository ?? throw new ArgumentNullException(nameof(messageRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _eventMediator = eventMediator ?? throw new ArgumentNullException(nameof(eventMediator));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
            _pushTokenService = pushTokenService;
            _context = context;
            _jobService = jobService;

            _dictionaryCleanupTimer = new Timer(CleanupDictionaries, null,
                TimeSpan.FromMinutes(DictionaryCleanupIntervalMinutes),
                TimeSpan.FromMinutes(DictionaryCleanupIntervalMinutes));

            _eventMediator.Subscribe<UserStatusChangedEvent>(HandleUserStatusChanged);
            _eventMediator.Subscribe<UserAvailabilityChangedEvent>(HandleUserAvailabilityChanged);
        }

        public event EventHandler<NotificationDto>? NotificationReceived;

        public async Task<IEnumerable<NotificationEntity>> GetUnreadNotificationsAsync(int? userId = null)
        {
            if (!userId.HasValue) return Enumerable.Empty<NotificationEntity>();
            return await _notificationRepository.GetUnreadNotificationsAsync(userId.Value);
        }

        public async Task<bool> MarkAsSeenAsync(int notificationId, int? userId = null)
        {
            if (!userId.HasValue) return false;
            return await _notificationRepository.MarkNotificationAsSeenAsync(notificationId, userId.Value);
        }

        public async Task<bool> MarkNotificationsAsSeenAsync(IEnumerable<int> notificationIds, int? userId = null)
        {
            if (!userId.HasValue || notificationIds == null) return false;

            bool allMarked = true;
            foreach (var id in notificationIds)
            {
                var success = await _notificationRepository.MarkNotificationAsSeenAsync(id, userId.Value);
                if (!success) allMarked = false;
            }
            return allMarked;
        }

        public async Task<bool> CreateNotificationAsync(int receiverId, string message, int? senderId = null)
        {
            try
            {
                var notification = new NotificationEntity
                {
                    ReceiverID = receiverId,
                    SenderID = senderId,
                    Message = message,
                    IsSeen = false,
                    Timestamp = DateTime.UtcNow
                };
                await _notificationRepository.CreateNotificationAsync(notification);

                // Notify the receiver if online
                if (await IsUserOnline(receiverId))
                {
                    await SendToUserAsync(receiverId, new
                    {
                        type = "new_notification",
                        message = message,
                        timestamp = DateTime.UtcNow
                    });
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating notification for user {ReceiverId}", receiverId);
                return false;
            }
        }

        public async Task<bool> BroadcastNotificationAsync(string message, int? senderId = null, string? department = null)
        {
            try
            {
                var users = await _userRepository.GetUsersByDepartmentAsync(department ?? string.Empty);
                foreach (var user in users)
                {
                    if (user.UserID != senderId)
                    {
                        await CreateNotificationAsync(user.UserID, message, senderId);
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting notification to department {Department}", department ?? "all");
                return false;
            }
        }

        public Task<bool> SendNotificationAsync(int receiverId, string message)
        {
            return CreateNotificationAsync(receiverId, message);
        }

        public async Task<bool> SendChatMessageAsync(int receiverId, string message, bool queueIfOffline = true)
        {
            try
            {
                var isOnline = await IsUserOnline(receiverId);
                if (!isOnline && !queueIfOffline) return false;

                var senderId = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value is string userIdStr &&
                              int.TryParse(userIdStr, out var userId) ? userId : 0;

                if (senderId <= 0)
                {
                    _logger.LogWarning("Invalid sender ID when sending message to {ReceiverId}", receiverId);
                    return false;
                }

                var messageEntity = MessageEntity.CreateChatMessage(
                    senderId: senderId,
                    receiverId: receiverId,
                    content: message,
                    isDelivered: isOnline,
                    idempotencyKey: Guid.NewGuid().ToString()
                );

                await _messageRepository.CreateAsync(messageEntity);
                await _unitOfWork.SaveChangesAsync();

                if (isOnline)
                {
                    await SendToUserAsync(receiverId, new
                    {
                        type = "new_message",
                        messageId = messageEntity.MessageID,
                        senderId = messageEntity.SenderID,
                        content = message,
                        timestamp = messageEntity.Timestamp
                    });
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending chat message to user {ReceiverId}", receiverId);
                return false;
            }
        }

        public async Task<bool> MarkMessagesAsReadAsync(int senderId, int? currentUserId = null)
        {
            if (!currentUserId.HasValue) return false;
            var messages = (await _messageRepository.GetByUserIdAsync(currentUserId.Value))
                .Where(m => m.SenderID == senderId && !m.IsRead && m.ReceiverID == currentUserId.Value)
                .ToList();
            if (messages.Any())
            {
                foreach (var message in messages)
                {
                    message.MarkAsRead();
                }
                return true;
            }
            return false;
        }

        public async Task<bool> MarkMessagesAsDeliveredAsync(int senderId, int? currentUserId = null)
        {
            if (!currentUserId.HasValue) return false;
            var messages = (await _messageRepository.GetByUserIdAsync(currentUserId.Value))
                .Where(m => m.SenderID == senderId && !m.IsDelivered && m.ReceiverID == currentUserId.Value)
                .ToList();
            if (messages.Any())
            {
                foreach (var message in messages)
                {
                    message.MarkAsDelivered();
                }
                await _unitOfWork.SaveChangesAsync();
                return true;
            }
            return false;
        }

        public async Task<bool> MarkMessagesAsDeliveredAsync(IEnumerable<int> messageIds, int? currentUserId = null)
        {
            if (!currentUserId.HasValue || messageIds == null) return false;
            var messages = (await _messageRepository.GetByUserIdAsync(currentUserId.Value))
                .Where(m => messageIds.Contains(m.MessageID) && m.ReceiverID == currentUserId.Value)
                .ToList();
            if (messages.Any())
            {
                foreach (var message in messages)
                {
                    if (!message.IsDelivered)
                    {
                        message.MarkAsDelivered();
                    }
                }
                await _unitOfWork.SaveChangesAsync();
                return true;
            }
            return false;
        }

        public async Task<int> GetUnreadMessagesCountAsync(int? userId = null)
        {
            if (!userId.HasValue) return 0;
            var messages = (await _messageRepository.GetByUserIdAsync(userId.Value))
                .Where(m => !m.IsRead && m.ReceiverID == userId.Value)
                .ToList();
            return messages.Count();
        }

        public async Task<IEnumerable<MessageDto>> GetUnreadMessagesAsync(int? userId = null)
        {
            if (!userId.HasValue) return Enumerable.Empty<MessageDto>();

            // Get unread messages for the user
            var messages = (await _messageRepository.GetByUserIdAsync(userId.Value))
                .Where(m => !m.IsRead && m.ReceiverID == userId.Value);

            // Convert to DTOs
            return messages.Select(m => new MessageDto
            {
                Id = m.MessageID,
                SenderId = m.SenderID,
                ReceiverId = m.ReceiverID,
                Message = m.MessageText,
                Timestamp = m.Timestamp,
                IsDelivered = m.IsDelivered,
                IsRead = m.IsRead,
                Status = m.Status,
                FromUserProfileImage = new byte[0] // Initialize with empty array
            });
        }

        public async Task<IEnumerable<MessageDto>> GetMessageHistoryAsync(int otherUserId, int? currentUserId = null, int page = 1, int pageSize = 50)
        {
            if (!currentUserId.HasValue) return Enumerable.Empty<MessageDto>();
            var messages = (await _messageRepository.GetConversationAsync(currentUserId.Value, otherUserId))
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
            return messages.Select(m => new MessageDto
            {
                Id = m.MessageID,
                SenderId = m.SenderID,
                ReceiverId = m.ReceiverID,
                Message = m.MessageText,
                Timestamp = m.Timestamp,
                IsDelivered = m.IsDelivered,
                IsRead = m.IsRead,
                Status = m.Status,
                FromUserProfileImage = new byte[0] // Initialize with empty array
                // Note: ReadAt and DeliveredAt are not available in MessageEntity
            });
        }

        public async Task<bool> DeleteMessageAsync(int messageId, int? currentUserId = null)
        {
            if (!currentUserId.HasValue) return false;
            var message = await _messageRepository.GetByIdAsync(messageId);
            if (message == null || (message.SenderID != currentUserId.Value && message.ReceiverID != currentUserId.Value))
                return false;

            // Soft delete not supported in MessageEntity, so we'll just mark as read
            message.MarkAsRead();
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteConversationAsync(int otherUserId, int? currentUserId = null)
        {
            if (!currentUserId.HasValue) return false;
            var messages = await _messageRepository.GetConversationAsync(currentUserId.Value, otherUserId);
            if (!messages.Any()) return false;

            foreach (var message in messages)
            {
                if (message.SenderID == currentUserId || message.ReceiverID == currentUserId)
                {
                    // Mark messages as read for the current user
                    message.MarkAsRead();
                }
            }

            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<bool> IsUserOnline(int userId)
        {
            return await _messageRepository.IsUserOnlineAsync(userId);
        }

        private async Task UpdateUserStatusAsync(int userId, bool isOnline)
        {
            // Update user connection status in the database
            await _messageRepository.UpdateUserConnectionStatusAsync(userId, isOnline);
        }

        public async Task HandleUserConnectionAsync(WebSocketConnectionEntity connection, WebSocket socket)
        {
            await _webSocketManager.AddConnectionAsync(connection, socket);
            await UpdateUserStatusAsync(connection.UserId, true);
        }

        public async Task SendToUserAsync(int userId, object message)
        {
            try
            {
                if (await IsUserOnline(userId))
                {
                    var json = TDFShared.Helpers.JsonSerializationHelper.SerializeCompact(message);
                    await _webSocketManager.SendToAsync(userId, message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message to user {UserId}", userId);
            }
        }

        public async Task SendToGroupAsync(string group, object message)
        {
            try
            {
                var json = TDFShared.Helpers.JsonSerializationHelper.SerializeCompact(message);
                await _webSocketManager.SendToGroupAsync(group, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message to group {Group}", group);
            }
        }

        public async Task SendToAllAsync(object message, IEnumerable<string>? excludedConnections = null)
        {
            try
            {
                await _webSocketManager.SendToAllAsync(message, excludedConnections ?? Enumerable.Empty<string>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message to all connections");
            }
        }

        public async Task<bool> HandleUserConnectionAsync(int userId, bool isConnected, string? machineName = null)
        {
            try
            {
                var status = isConnected ? UserPresenceStatus.Online : UserPresenceStatus.Offline;
                var result = await _messageRepository.UpdateUserConnectionStatusAsync(userId, isConnected, machineName);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user {UserId} connection status", userId);
                return false;
            }
        }

        public Task ShowErrorAsync(string message)
        {
            _logger.LogError("Error: {Message}", message);
            return Task.CompletedTask;
        }

        public Task ShowSuccessAsync(string message)
        {
            _logger.LogInformation("Success: {Message}", message);
            return Task.CompletedTask;
        }

        public Task ShowWarningAsync(string message)
        {
            _logger.LogWarning("Warning: {Message}", message);
            return Task.CompletedTask;
        }

        private void CleanupDictionaries(object? state)
        {
            try
            {
                var cutoff = DateTime.UtcNow.AddHours(-1);

                // Clean up message rate limits
                var rateLimitKeys = _messageRateLimits
                    .Where(x => x.Value.LastMessage < cutoff)
                    .Select(x => x.Key)
                    .ToList();

                foreach (var key in rateLimitKeys)
                {
                    _messageRateLimits.TryRemove(key, out _);
                }

                // Clean up last message times
                var messageTimeKeys = _lastMessageTime
                    .Where(x => x.Value < cutoff)
                    .Select(x => x.Key)
                    .ToList();

                foreach (var key in messageTimeKeys)
                {
                    _lastMessageTime.TryRemove(key, out _);
                }

                var totalCleaned = rateLimitKeys.Count + messageTimeKeys.Count;
                if (totalCleaned > 0)
                {
                    _logger.LogDebug("Cleaned up {Count} expired message entries", totalCleaned);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up dictionaries");
            }
        }

        private void HandleUserStatusChanged(UserStatusChangedEvent e)
        {
            _ = SendToUserAsync(e.UserId, new
            {
                type = "user_status_changed",
                userId = e.UserId,
                status = e.Status.ToString(),
                timestamp = DateTime.UtcNow
            });
        }

        private void HandleUserAvailabilityChanged(UserAvailabilityChangedEvent e)
        {
            _ = SendToUserAsync(e.UserId, new
            {
                type = "user_availability_changed",
                userId = e.UserId,
                isAvailable = e.IsAvailableForChat,
                timestamp = DateTime.UtcNow
            });
        }

        public void Dispose()
        {
            _dictionaryCleanupTimer?.Dispose();
            GC.SuppressFinalize(this);
        }

        public async Task SendNotificationAsync(int userId, string title, string message, NotificationType type = NotificationType.Info, string? data = null)
        {
            try
            {
                var notification = new NotificationEntity
                {
                    ReceiverID = userId,
                    Message = $"{title}: {message}",
                    IsSeen = false,
                    Timestamp = DateTime.UtcNow
                };

                await _notificationRepository.CreateNotificationAsync(notification);
                await SendPushNotificationIfNeededAsync(userId, notification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notification to user {UserId}", userId);
            }
        }

        public async Task SendNotificationAsync(IEnumerable<int> userIds, string title, string message, NotificationType type = NotificationType.Info, string? data = null)
        {
            try
            {
                foreach (var userId in userIds)
                {
                    var notification = new NotificationEntity
                    {
                        ReceiverID = userId,
                        Message = $"{title}: {message}",
                        IsSeen = false,
                        Timestamp = DateTime.UtcNow
                    };

                    await _notificationRepository.CreateNotificationAsync(notification);
                    await SendPushNotificationIfNeededAsync(userId, notification);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notifications to multiple users");
            }
        }

        public async Task SendDepartmentNotificationAsync(string department, string title, string message, NotificationType type = NotificationType.Info, string? data = null)
        {
            try
            {
                var users = await _userRepository.GetUsersByDepartmentAsync(department);
                foreach (var user in users)
                {
                    var notification = new NotificationEntity
                    {
                        ReceiverID = user.UserID,
                        Message = $"{title}: {message}",
                        IsSeen = false,
                        Timestamp = DateTime.UtcNow
                    };

                    await _notificationRepository.CreateNotificationAsync(notification);
                    await SendPushNotificationIfNeededAsync(user.UserID, notification);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending department notification to {Department}", department);
            }
        }

        private async Task SendPushNotificationIfNeededAsync(int userId, NotificationEntity notification)
        {
            try
            {
                var tokens = await _pushTokenService.GetUserTokensAsync(userId);
                if (!tokens.Any())
                {
                    _logger.LogDebug("No push tokens found for user {UserId}", userId);
                    return;
                }

                foreach (var token in tokens)
                {
                    try
                    {
                        await SendPushNotificationAsync(token, notification);
                        await _pushTokenService.UpdateTokenLastUsedAsync(token.Token);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error sending push notification to token {Token}", token.Token);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending push notifications for user {UserId}", userId);
            }
        }

        public async Task ScheduleNotificationAsync(int userId, string title, string message, DateTime deliveryTime, NotificationType type = NotificationType.Info, string? data = null)
        {
            try
            {
                if (deliveryTime <= DateTime.UtcNow)
                {
                    throw new ArgumentException("Delivery time must be in the future", nameof(deliveryTime));
                }

                // Create notification record
                var notification = new NotificationRecord
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = title,
                    Message = message,
                    Type = type,
                    Timestamp = DateTime.UtcNow,
                    Data = data
                };

                // Schedule the notification
                await _jobService.ScheduleJobAsync(
                    "SendNotification",
                    new Dictionary<string, object>
                    {
                        { "userId", userId },
                        { "notificationId", notification.Id },
                        { "title", title },
                        { "message", message },
                        { "type", type },
                        { "data", data }
                    },
                    deliveryTime);

                _logger.LogInformation("Scheduled notification {NotificationId} for user {UserId} at {DeliveryTime}",
                    notification.Id, userId, deliveryTime);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scheduling notification for user {UserId}", userId);
                throw;
            }
        }

        public async Task CancelScheduledNotificationAsync(string notificationId)
        {
            try
            {
                await _jobService.DeleteJobAsync("SendNotification", notificationId);
                _logger.LogInformation("Cancelled scheduled notification {NotificationId}", notificationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling scheduled notification {NotificationId}", notificationId);
                throw;
            }
        }

        public async Task<IEnumerable<NotificationRecord>> GetScheduledNotificationsAsync(int userId)
        {
            try
            {
                var jobs = await _jobService.GetJobsAsync("SendNotification", userId.ToString());
                return jobs.Select(job => new NotificationRecord
                {
                    Id = job.Id,
                    Title = job.Data["title"].ToString(),
                    Message = job.Data["message"].ToString(),
                    Type = (NotificationType)job.Data["type"],
                    Timestamp = job.ScheduledTime,
                    Data = job.Data["data"]?.ToString()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving scheduled notifications for user {UserId}", userId);
                throw;
            }
        }

        private async Task SendPushNotificationAsync(TDFAPI.Models.PushToken token, NotificationEntity notification)
        {
            // Platform-specific push notification sending logic
            switch (token.Platform.ToLower())
            {
                case "ios":
                    await SendIOSPushNotificationAsync(token, notification);
                    break;
                case "android":
                    await SendAndroidPushNotificationAsync(token, notification);
                    break;
                case "windows":
                    await SendWindowsPushNotificationAsync(token, notification);
                    break;
                case "macos":
                    await SendMacOSPushNotificationAsync(token, notification);
                    break;
                default:
                    throw new NotSupportedException($"Push notifications not supported for platform: {token.Platform}");
            }
        }

        private async Task SendIOSPushNotificationAsync(TDFAPI.Models.PushToken token, NotificationEntity notification)
        {
            try
            {
                _logger.LogInformation("Sending iOS push notification to token: {Token}", token.Token);
                
                // Create the notification payload
                var payload = new
                {
                    aps = new
                    {
                        alert = new
                        {
                            title = "Notification",
                            body = notification.Message
                        },
                        badge = 1,
                        sound = "default",
                        category = "default",
                        content_available = 1
                    },
                    notificationId = notification.NotificationID,
                    data = notification.Message
                };
                
                // Serialize the payload
                var jsonPayload = JsonSerializer.Serialize(payload);
                
                // In a production environment, you would use a library like PushSharp or a direct HTTP/2 implementation
                // to send the notification to Apple's APNS servers
                // For now, we'll log the payload and mark it as successful
                _logger.LogInformation("iOS Push Notification Payload: {Payload}", jsonPayload);
                
                // Update token last used timestamp
                await _pushTokenService.UpdateTokenLastUsedAsync(token.Token);
                
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending iOS push notification to token: {Token}", token.Token);
                throw;
            }
        }

        private static bool _firebaseInitialized = false;
        private static readonly object _firebaseInitLock = new();

        private void EnsureFirebaseInitialized()
        {
            if (_firebaseInitialized) return;
            lock (_firebaseInitLock)
            {
                if (_firebaseInitialized) return;
                
                try
                {
                    if (FirebaseApp.DefaultInstance == null)
                    {
                        // Path to your google-services.json or service account key
                        var credentialPath = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS") ?? "./google-service-account.json";
                        
                        // Check if the file exists
                        if (!System.IO.File.Exists(credentialPath))
                        {
                            _logger.LogError("Firebase service account file not found at: {Path}", credentialPath);
                            throw new FileNotFoundException($"Firebase service account file not found at: {credentialPath}");
                        }
                        
                        _logger.LogInformation("Initializing Firebase with service account from: {Path}", credentialPath);
                        
                        // Create the Firebase app
                        FirebaseApp.Create(new AppOptions()
                        {
                            Credential = GoogleCredential.FromFile(credentialPath)
                        });
                        
                        _logger.LogInformation("Firebase initialized successfully");
                    }
                    _firebaseInitialized = true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error initializing Firebase");
                    throw; // Re-throw to ensure the caller knows initialization failed
                }
            }
        }

        private async Task SendAndroidPushNotificationAsync(TDFAPI.Models.PushToken token, NotificationEntity notification)
        {
            try
            {
                _logger.LogInformation("Sending Android push notification to token: {Token}", token.Token);
                EnsureFirebaseInitialized();

                // Extract sender information if available
                string senderName = string.Empty;
                int? senderId = null;
                bool isBroadcast = false;
                string department = string.Empty;

                // Create the FCM message with enhanced data
                var message = new Message
                {
                    Token = token.Token,
                    Notification = new Notification
                    {
                        Title = "Notification",
                        Body = notification.Message
                    },
                    Data = new Dictionary<string, string>
                    {
                        { "notificationId", notification.NotificationID.ToString() },
                        { "data", notification.Message },
                        { "click_action", "OPEN_NOTIFICATION" },
                        { "timestamp", DateTime.UtcNow.ToString("o") }
                    },
                    Android = new AndroidConfig
                    {
                        Priority = Priority.High,
                        Notification = new AndroidNotification
                        {
                            ChannelId = "default_channel",
                            Sound = "default",
                            Icon = "@mipmap/appicon",
                            Color = "#4285F4", // Google blue
                            Tag = notification.NotificationID.ToString() // Use ID as tag to prevent duplicates
                        }
                    }
                };

                // Send the notification
                var response = await FirebaseMessaging.DefaultInstance.SendAsync(message);
                _logger.LogInformation("Android push sent. FCM response: {Response}", response);
                
                // Update the token's last used timestamp
                await _pushTokenService.UpdateTokenLastUsedAsync(token.Token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending Android push notification to token: {Token}", token.Token);
                throw;
            }
        }

        private async Task SendWindowsPushNotificationAsync(TDFAPI.Models.PushToken token, NotificationEntity notification)
        {
            try
            {
                _logger.LogInformation("Sending Windows push notification to token: {Token}", token.Token);
                
                // Create a Windows Toast notification XML payload
                var toastXml = $@"
                <toast launch=""notificationId={notification.NotificationID}"">
                    <visual>
                        <binding template=""ToastGeneric"">
                            <text>Notification</text>
                            <text>{notification.Message}</text>
                        </binding>
                    </visual>
                    <actions>
                        <action content=""View"" arguments=""view&amp;notificationId={notification.NotificationID}"" />
                        <action content=""Dismiss"" arguments=""dismiss&amp;notificationId={notification.NotificationID}"" />
                    </actions>
                </toast>";
                
                // In a production environment, you would use the Windows Notification Service (WNS)
                // to send the notification to Microsoft's WNS servers
                // For now, we'll log the payload and mark it as successful
                _logger.LogInformation("Windows Push Notification Payload: {Payload}", toastXml);
                
                // Update token last used timestamp
                await _pushTokenService.UpdateTokenLastUsedAsync(token.Token);
                
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending Windows push notification to token: {Token}", token.Token);
                throw;
            }
        }

        private async Task SendMacOSPushNotificationAsync(TDFAPI.Models.PushToken token, NotificationEntity notification)
        {
            try
            {
                _logger.LogInformation("Sending macOS push notification to token: {Token}", token.Token);
                
                // macOS uses the same APNS service as iOS, but with different payload structure
                var payload = new
                {
                    aps = new
                    {
                        alert = new
                        {
                            title = "Notification",
                            body = notification.Message
                        },
                        sound = "default",
                        content_available = 1
                    },
                    notificationId = notification.NotificationID,
                    data = notification.Message
                };
                
                // Serialize the payload
                var jsonPayload = JsonSerializer.Serialize(payload);
                
                // In a production environment, you would use a library like PushSharp or a direct HTTP/2 implementation
                // to send the notification to Apple's APNS servers
                // For now, we'll log the payload and mark it as successful
                _logger.LogInformation("macOS Push Notification Payload: {Payload}", jsonPayload);
                
                // Update token last used timestamp
                await _pushTokenService.UpdateTokenLastUsedAsync(token.Token);
                
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending macOS push notification to token: {Token}", token.Token);
                throw;
            }
        }
    }
}
