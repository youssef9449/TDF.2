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

namespace TDFAPI.Services
{
    public class NotificationService : INotificationService
    {
        private readonly INotificationRepository _notificationRepository;
        private readonly IUserRepository _userRepository;
        private readonly WebSocketConnectionManager _webSocketManager;
        private readonly ILogger<NotificationService> _logger;
        private readonly IMessageRepository _messageRepository;
        private readonly IServiceProvider _serviceProvider;
        private readonly IEventMediator _eventMediator;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ConcurrentDictionary<int, (DateTime LastMessage, int MessageCount)> _messageRateLimits =
            new();
        private readonly ConcurrentDictionary<string, DateTime> _lastMessageTime = new();
        private readonly Timer _dictionaryCleanupTimer;

        // Configuration for WebSocket behavior
        private const int DefaultBufferSize = 4096;  // 4KB default
        private const int MaxBufferSize = 65536;     // 64KB max
        private const int HeartbeatIntervalSeconds = 30;
        private const int ConnectionTimeoutSeconds = 120;
        private const int MaxMessagesPerMinute = 60;
        private const int DictionaryCleanupIntervalMinutes = 10;

        public NotificationService(
            INotificationRepository notificationRepository,
            IUserRepository userRepository,
            WebSocketConnectionManager webSocketManager,
            IMessageRepository messageRepository,
            ILogger<NotificationService> logger,
            IServiceProvider serviceProvider,
            IEventMediator eventMediator,
            IUnitOfWork unitOfWork,
            IHttpContextAccessor httpContextAccessor)
        {
            _notificationRepository = notificationRepository ?? throw new ArgumentNullException(nameof(notificationRepository));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _webSocketManager = webSocketManager ?? throw new ArgumentNullException(nameof(webSocketManager));
            _messageRepository = messageRepository ?? throw new ArgumentNullException(nameof(messageRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceProvider = serviceProvider;
            _eventMediator = eventMediator ?? throw new ArgumentNullException(nameof(eventMediator));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));

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
                var users = await _userRepository.GetUsersByDepartmentAsync(department);
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
                _logger.LogError(ex, "Error broadcasting notification to department {Department}", department);
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
                Status = m.Status
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
                Status = m.Status
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
                    var json = JsonSerializer.Serialize(message);
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
                var json = JsonSerializer.Serialize(message);
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
                var json = JsonSerializer.Serialize(message);
                await _webSocketManager.SendToAllAsync(json, excludedConnections);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting message to all users");
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
    }
}
