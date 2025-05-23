using System.Text.Json;
using TDFMAUI.Helpers;
using Microsoft.Extensions.Logging;
using TDFShared.DTOs.Messages;
using TDFShared.Enums;
using TDFShared.Models.Notification;

namespace TDFMAUI.Services
{
    public class NotificationService : INotificationService, IExtendedNotificationService
    {
        private readonly ApiService _apiService;
        private readonly WebSocketService _webSocketService;
        private readonly ILogger<NotificationService> _logger;
        private readonly ILocalStorageService _localStorage;

        public event EventHandler<NotificationDto> NotificationReceived;

        public NotificationService(
            ApiService apiService,
            WebSocketService webSocketService,
            ILocalStorageService localStorage,
            ILogger<NotificationService> logger)
        {
            // Log constructor entry
            logger?.LogInformation("NotificationService constructor started.");

            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            _webSocketService = webSocketService ?? throw new ArgumentNullException(nameof(webSocketService));
            _localStorage = localStorage ?? throw new ArgumentNullException(nameof(localStorage));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Subscribe to WebSocket notifications
            _webSocketService.NotificationReceived += OnWebSocketNotificationReceived;
            _logger.LogInformation("Subscribed to WebSocketService.NotificationReceived.");

            // Log constructor exit
            logger?.LogInformation("NotificationService constructor finished.");
        }

        public async Task<IEnumerable<NotificationEntity>> GetUnreadNotificationsAsync()
        {
            try
            {
                if (App.CurrentUser == null)
                {
                    _logger.LogWarning("Cannot get notifications: Current user is null");
                    return Enumerable.Empty<NotificationEntity>();
                }

                var userId = App.CurrentUser.UserID;
                // Use the correct endpoint from the API controller
                var notificationDtos = await _apiService.GetAsync<List<NotificationDto>>("notifications/unread");

                return notificationDtos?.Select(dto => MapNotificationDtoToEntity(dto)).ToList()
                       ?? Enumerable.Empty<NotificationEntity>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching unread notifications");
                return Enumerable.Empty<NotificationEntity>();
            }
        }

        public async Task<bool> MarkAsSeenAsync(int notificationId)
        {
            try
            {
                if (App.CurrentUser == null)
                {
                    _logger.LogWarning("Cannot mark notification as seen: Current user is null");
                    return false;
                }

                var userId = App.CurrentUser.UserID;
                var result = await _apiService.PostAsync<object, bool>($"notifications/{notificationId}/seen",
                    new { userId });

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking notification {NotificationId} as seen", notificationId);
                return false;
            }
        }

        public async Task<bool> MarkNotificationsAsSeenAsync(IEnumerable<int> notificationIds)
        {
            try
            {
                if (App.CurrentUser == null)
                {
                    _logger.LogWarning("Cannot mark notifications as seen: Current user is null");
                    return false;
                }

                if (notificationIds == null || !notificationIds.Any())
                {
                    return true; // Nothing to do
                }

                var userId = App.CurrentUser.UserID;
                var result = await _apiService.PostAsync<object, bool>("notifications/seen",
                    new { userId, notificationIds });

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking multiple notifications as seen");
                return false;
            }
        }

        public async Task<bool> CreateNotificationAsync(int receiverId, string message, int? senderId = null)
        {
            try
            {
                // If senderId is not provided, use current user's ID
                if (!senderId.HasValue && App.CurrentUser != null)
                {
                    senderId = App.CurrentUser.UserID;
                }

                var result = await _apiService.PostAsync<object, bool>("notifications",
                    new { receiverId, message, senderId });

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating notification for user {ReceiverId}", receiverId);
                return false;
            }
        }

        public async Task<bool> BroadcastNotificationAsync(string message, string? department = null)
        {
            try
            {
                if (App.CurrentUser == null)
                {
                    _logger.LogWarning("Cannot broadcast notification: Current user is null");
                    return false;
                }

                var senderId = App.CurrentUser.UserID;
                var result = await _apiService.PostAsync<object, bool>("notifications/broadcast",
                    new { message, senderId, department });

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting notification");
                return false;
            }
        }

        public async Task<bool> SendChatMessageAsync(int receiverId, string message, bool queueIfOffline = true)
        {
            try
            {
                if (App.CurrentUser == null)
                {
                    _logger.LogWarning("Cannot send chat message: Current user is null");
                    return false;
                }

                var senderId = App.CurrentUser.UserID;
                var result = await _apiService.PostAsync<object, bool>("messages/chat",
                    new { receiverId, senderId, message, queueIfOffline });

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending chat message to user {ReceiverId}", receiverId);
                return false;
            }
        }

        public async Task<bool> MarkMessagesAsReadAsync(int senderId)
        {
            try
            {
                if (App.CurrentUser == null)
                {
                    _logger.LogWarning("Cannot mark messages as read: Current user is null");
                    return false;
                }

                var receiverId = App.CurrentUser.UserID;
                var result = await _apiService.PostAsync<object, bool>("messages/read",
                    new { senderId, receiverId });

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking messages from user {SenderId} as read", senderId);
                return false;
            }
        }

        public async Task<bool> MarkMessagesAsDeliveredAsync(int senderId)
        {
            try
            {
                if (App.CurrentUser == null)
                {
                    _logger.LogWarning("Cannot mark messages as delivered: Current user is null");
                    return false;
                }

                var receiverId = App.CurrentUser.UserID;
                // Remove the "api/" prefix as ApiService handles this
                var result = await _apiService.PostAsync<object, bool>("messages/delivered/from-sender",
                    new { senderId });

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking messages from user {SenderId} as delivered", senderId);
                return false;
            }
        }

        public async Task<bool> MarkMessagesAsDeliveredAsync(IEnumerable<int> messageIds)
        {
            try
            {
                if (messageIds == null || !messageIds.Any())
                {
                    _logger.LogWarning("Cannot mark messages as delivered: No message IDs provided");
                    return false;
                }

                if (App.CurrentUser == null)
                {
                    _logger.LogWarning("Cannot mark messages as delivered: Current user is null");
                    return false;
                }

                // Remove the "api/" prefix as ApiService handles this
                var result = await _apiService.PostAsync<object, bool>("messages/delivered/bulk",
                    new { messageIds = messageIds.ToList() });

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking messages as delivered in bulk: {Error}", ex.Message);
                return false;
            }
        }

        public async Task<int> GetUnreadMessagesCountAsync()
        {
            try
            {
                if (App.CurrentUser == null)
                {
                    _logger.LogWarning("Cannot get unread messages count: Current user is null");
                    return 0;
                }

                var userId = App.CurrentUser.UserID;
                // Remove the "api/" prefix as ApiService handles this
                var count = await _apiService.GetAsync<int>($"messages/unread/count/{userId}");

                return count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching unread messages count");
                return 0;
            }
        }

        private NotificationEntity MapNotificationDtoToEntity(NotificationDto dto)
        {
            if (dto == null) return null;

            return new NotificationEntity
            {
                NotificationID = dto.NotificationId,
                ReceiverID = dto.UserId,
                Message = dto.Message,
                IsSeen = false,
                Timestamp = dto.Timestamp,
            };
        }

        #region Interface Implementation for Show Methods
        // Use NotificationHelper for platform-aware display

        public async Task ShowErrorAsync(string message)
        {
            _logger.LogError("Notification Service Error: {Message}", message);
            // Use NotificationHelper.ShowNotificationAsync
            await NotificationHelper.ShowNotificationAsync("Error", message, NotificationType.Error);
        }

        public async Task ShowSuccessAsync(string message)
        {
            _logger.LogInformation("Notification Service Success: {Message}", message);
            // Use NotificationHelper.ShowNotificationAsync
            await NotificationHelper.ShowNotificationAsync("Success", message, NotificationType.Success);
        }

        public async Task ShowWarningAsync(string message)
        {
            _logger.LogWarning("Notification Service Warning: {Message}", message);
            // Use NotificationHelper.ShowNotificationAsync
            await NotificationHelper.ShowNotificationAsync("Warning", message, NotificationType.Warning);
        }

        // Alias for CreateNotificationAsync to match the method name used in code
        public async Task<bool> SendNotificationAsync(int receiverId, string message)
        {
            return await CreateNotificationAsync(receiverId, message);
        }
        #endregion

        #region IExtendedNotificationService Implementation

        public async Task<bool> ShowLocalNotificationAsync(string title, string message, string data = null)
        {
            try
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await NotificationHelper.ShowNotificationAsync(title, message, NotificationType.Info, data);
                });
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing local notification");
                return false;
            }
        }

        public async Task<bool> ShowLocalNotificationAsync(string title, string message, NotificationType type, string data = null)
        {
            try
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await NotificationHelper.ShowNotificationAsync(title, message, type, data);
                });
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing local notification with type");
                return false;
            }
        }

        public async Task<bool> ScheduleNotificationAsync(string title, string message, DateTime deliveryTime, string data = null)
        {
            try
            {
                string id = Guid.NewGuid().ToString();
                var notification = new Helpers.NotificationRecord
                {
                    Id = id,
                    Title = title,
                    Message = message,
                    Type = NotificationType.Info,
                    Timestamp = deliveryTime,
                    Data = data
                };

                // Get existing scheduled notifications
                var scheduledNotifications = await GetScheduledNotificationsAsync();
                scheduledNotifications.Add(notification);

                // Save updated list
                await SaveScheduledNotificationsAsync(scheduledNotifications);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scheduling notification");
                return false;
            }
        }

        public async Task<bool> CancelScheduledNotificationAsync(string id)
        {
            try
            {
                var scheduledNotifications = await GetScheduledNotificationsAsync();
                var updatedNotifications = scheduledNotifications.Where(n => n.Id != id).ToList();

                if (scheduledNotifications.Count == updatedNotifications.Count)
                {
                    return false; // Nothing was removed
                }

                await SaveScheduledNotificationsAsync(updatedNotifications);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error canceling scheduled notification");
                return false;
            }
        }

        public async Task<IEnumerable<string>> GetScheduledNotificationIdsAsync()
        {
            try
            {
                var scheduledNotifications = await GetScheduledNotificationsAsync();
                return scheduledNotifications.Select(n => n.Id).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting scheduled notification IDs");
                return Enumerable.Empty<string>();
            }
        }

        public async Task<bool> ClearAllScheduledNotificationsAsync()
        {
            try
            {
                await SaveScheduledNotificationsAsync(new List<Helpers.NotificationRecord>());
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing all scheduled notifications");
                return false;
            }
        }

        // This method is for internal use
        public async Task<List<Helpers.NotificationRecord>> GetNotificationHistoryAsync()
        {
            try
            {
                return await GetNotificationHistoryInternalAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting notification history");
                return new List<Helpers.NotificationRecord>();
            }
        }

        // This method implements the interface
        async Task<List<TDFShared.DTOs.Messages.NotificationRecord>> IExtendedNotificationService.GetNotificationHistoryAsync()
        {
            try
            {
                var helperRecords = await GetNotificationHistoryInternalAsync();

                // Convert from helper records to shared DTO records
                return helperRecords.Select(hr => new TDFShared.DTOs.Messages.NotificationRecord
                {
                    Id = hr.Id,
                    Title = hr.Title,
                    Message = hr.Message,
                    Type = hr.Type,
                    Timestamp = hr.Timestamp,
                    Data = hr.Data
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting notification history for interface");
                return new List<TDFShared.DTOs.Messages.NotificationRecord>();
            }
        }

        public async Task ClearNotificationHistoryAsync()
        {
            try
            {
                await SaveNotificationHistoryAsync(new List<Helpers.NotificationRecord>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing notification history");
            }
        }

        // Helper methods for internal storage
        private async Task<List<Helpers.NotificationRecord>> GetScheduledNotificationsAsync()
        {
            try
            {
                var json = await _localStorage.GetItemAsync<string>("scheduled_notifications");
                if (string.IsNullOrEmpty(json))
                {
                    return new List<Helpers.NotificationRecord>();
                }
                return JsonSerializer.Deserialize<List<Helpers.NotificationRecord>>(json) ?? new List<Helpers.NotificationRecord>();
            }
            catch
            {
                return new List<Helpers.NotificationRecord>();
            }
        }

        private async Task SaveScheduledNotificationsAsync(List<Helpers.NotificationRecord> notifications)
        {
            var json = TDFShared.Helpers.JsonSerializationHelper.Serialize(notifications);
            await _localStorage.SetItemAsync("scheduled_notifications", json);
        }

        private async Task<List<Helpers.NotificationRecord>> GetNotificationHistoryInternalAsync()
        {
            try
            {
                var json = await _localStorage.GetItemAsync<string>("notification_history");
                if (string.IsNullOrEmpty(json))
                {
                    return new List<Helpers.NotificationRecord>();
                }
                return TDFShared.Helpers.JsonSerializationHelper.Deserialize<List<Helpers.NotificationRecord>>(json) ?? new List<Helpers.NotificationRecord>();
            }
            catch
            {
                return new List<Helpers.NotificationRecord>();
            }
        }

        private async Task SaveNotificationHistoryAsync(List<Helpers.NotificationRecord> history)
        {
            var json = TDFShared.Helpers.JsonSerializationHelper.Serialize(history);
            await _localStorage.SetItemAsync("notification_history", json);
        }
        #endregion

        private void OnWebSocketNotificationReceived(object sender, NotificationEventArgs e)
        {
            // Check if the notification type is relevant (assuming Type property exists)
            // if (e.Type == NotificationType.Info) // Or however you classify general notifications vs chat
            {
                var notificationDto = MapNotificationEventArgsToNotificationDto(e);
                NotificationReceived?.Invoke(this, notificationDto);
            }
        }

        private NotificationDto MapNotificationEventArgsToNotificationDto(NotificationEventArgs e)
        {
            return new NotificationDto
            {
                 NotificationId = e.NotificationId, // Use NotificationId
                 UserId = App.CurrentUser?.UserID ?? 0,
                 Title = e.Title ?? "Notification", // Use Title if available
                 Message = e.Message, // Use Message
                 Timestamp = e.Timestamp, // Use Timestamp
                 Level = MapTypeToLevel(e.Type), // Map Type to Level if needed
                 SenderId = e.SenderId,
                 SenderName = e.SenderName
            };
        }

        // Helper to map NotificationType enum (from WebSocket) to NotificationLevel enum (used internally?)
        // Adjust this based on your actual enums
        private NotificationLevel MapTypeToLevel(NotificationType type)
        {
            switch (type)
            {
                case NotificationType.Error: return NotificationLevel.High;
                case NotificationType.Warning: return NotificationLevel.High;
                case NotificationType.Success: return NotificationLevel.Medium;
                case NotificationType.Info:
                default: return NotificationLevel.Low;
            }
        }
    }
}