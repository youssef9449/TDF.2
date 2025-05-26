using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System;
using Microsoft.Maui.Controls;
using TDFShared.Enums;
using TDFShared.DTOs.Messages;
using System.Linq;
using System.Text.Json;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.Storage;

namespace TDFMAUI.Services
{
    /// <summary>
    /// Extends the existing NotificationService with platform-specific notifications and local history
    /// </summary>
    public class NotificationExtensionService : IExtendedNotificationService
    {
        private readonly NotificationService _notificationService;
        private readonly IPlatformNotificationService _platformService;
        private readonly ILogger<NotificationExtensionService> _logger;
        private readonly ApiService _apiService;

        // Correct event signature to match INotificationService
        public event EventHandler<NotificationDto> NotificationReceived;

        public NotificationExtensionService(
            NotificationService notificationService,
            IPlatformNotificationService platformService,
            ApiService apiService,
            ILogger<NotificationExtensionService> logger)
        {
            _notificationService = notificationService;
            _platformService = platformService;
            _logger = logger;
            _apiService = apiService;
        }

        public async Task<IEnumerable<TDFShared.Models.Notification.NotificationEntity>> GetUnreadNotificationsAsync()
        {
            return await _notificationService.GetUnreadNotificationsAsync();
        }

        public async Task<bool> MarkAsSeenAsync(int notificationId)
        {
            return await _notificationService.MarkAsSeenAsync(notificationId);
        }

        public async Task<bool> MarkNotificationsAsSeenAsync(IEnumerable<int> notificationIds)
        {
            return await _notificationService.MarkNotificationsAsSeenAsync(notificationIds);
        }

        public async Task<bool> CreateNotificationAsync(int receiverId, string message, int? senderId = null)
        {
            // Use the base service to create the notification on the server
            var result = await _notificationService.CreateNotificationAsync(receiverId, message, senderId);

            // If the notification is for the current user, show a platform notification
            if (result && App.CurrentUser != null && receiverId == App.CurrentUser.UserID)
            {
                try
                {
                    string senderName = senderId.HasValue ?
                        await GetUserNameAsync(senderId.Value) :
                        "System";

                    await _platformService.ShowNotificationAsync(
                        $"New notification from {senderName}",
                        message,
                        NotificationType.Info);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error showing platform notification");
                }
            }

            return result;
        }

        public async Task<bool> BroadcastNotificationAsync(string message, string? department = null)
        {
            // Use the base service to broadcast the notification
            var result = await _notificationService.BroadcastNotificationAsync(message, department);

            // For broadcasts, we don't need to show a platform notification since the server
            // will send it to the current user through the WebSocket if appropriate

            return result;
        }

        public async Task<bool> SendChatMessageAsync(int receiverId, string message, bool queueIfOffline = true)
        {
            // Use the base service to send the chat message
            var result = await _notificationService.SendChatMessageAsync(receiverId, message, queueIfOffline);

            // If successful, we don't need to show a platform notification for sent messages

            return result;
        }

        public async Task<bool> MarkMessagesAsReadAsync(int senderId)
        {
            return await _notificationService.MarkMessagesAsReadAsync(senderId);
        }

        public async Task<bool> MarkMessagesAsDeliveredAsync(int senderId)
        {
            return await _notificationService.MarkMessagesAsDeliveredAsync(senderId);
        }

        public async Task<bool> MarkMessagesAsDeliveredAsync(IEnumerable<int> messageIds)
        {
            return await _notificationService.MarkMessagesAsDeliveredAsync(messageIds);
        }

        public async Task<int> GetUnreadMessagesCountAsync()
        {
            return await _notificationService.GetUnreadMessagesCountAsync();
        }

        // Implement IExtendedNotificationService methods

        // Added the missing overload with string data parameter
        public async Task<bool> ShowLocalNotificationAsync(string title, string message, string data = null)
        {
            return await ShowLocalNotificationAsync(title, message, NotificationType.Info, data);
        }

        public async Task<bool> ShowLocalNotificationAsync(string title, string message, NotificationType type, string data = null)
        {
            _logger.LogInformation("Showing local notification: {Title}, {Type}", title, type);

            try
            {
                var result = await _platformService.ShowLocalNotificationAsync(title, message, type, data);
                await AddNotificationToHistoryAsync(title, message, type, data);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing local notification");
                return false;
            }
        }

        private async Task AddNotificationToHistoryAsync(string title, string message, NotificationType type, string data = null)
        {
            try
            {
                var history = await GetNotificationHistoryAsync();
                history.Add(new Helpers.NotificationRecord
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = title,
                    Message = message,
                    Type = type,
                    Timestamp = DateTime.Now,
                    Data = data
                });

                await SaveNotificationHistoryAsync(history);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding notification to history");
            }
        }

        // This method is for internal use
        public async Task<List<Helpers.NotificationRecord>> GetNotificationHistoryAsync()
        {
            _logger.LogInformation("Getting notification history");

            try
            {
                var historyJson = Preferences.Get("NotificationHistory", "[]");
                return JsonSerializer.Deserialize<List<Helpers.NotificationRecord>>(historyJson) ?? new List<Helpers.NotificationRecord>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting notification history");
                return new List<Helpers.NotificationRecord>();
            }
        }

        // This method implements the interface
        async Task<List<NotificationRecord>> IExtendedNotificationService.GetNotificationHistoryAsync()
        {
            _logger.LogInformation("Getting notification history for interface");

            try
            {
                var helperRecords = await GetNotificationHistoryAsync();

                // Convert from helper records to shared DTO records
                return helperRecords.Select(hr => new NotificationRecord
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
                return new List<NotificationRecord>();
            }
        }

        // Added helper to save history (example using Preferences)
        private Task SaveNotificationHistoryAsync(List<Helpers.NotificationRecord> history)
        {
            try
            {
                // Limit history size if needed
                const int maxHistorySize = 100;
                if (history.Count > maxHistorySize)
                {
                    history = history.Skip(history.Count - maxHistorySize).ToList();
                }

                var historyJson = TDFShared.Helpers.JsonSerializationHelper.Serialize(history);
                Preferences.Set("NotificationHistory", historyJson);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving notification history");
            }
            return Task.CompletedTask;
        }

        // Changed return type to match interface
        public async Task ClearNotificationHistoryAsync()
        {
            _logger.LogInformation("Clearing notification history");

            try
            {
                await SaveNotificationHistoryAsync(new List<Helpers.NotificationRecord>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing notification history");
            }
        }

        // Helper methods

        private async Task<string> GetUserNameAsync(int userId)
        {
            try
            {
                var apiService = App.Services.GetService<ApiService>();
                if (apiService != null)
                {
                    var user = await apiService.GetUserAsync(userId);
                    if (user != null && !string.IsNullOrWhiteSpace(user.FullName))
                        return user.FullName;
                    if (user != null && !string.IsNullOrWhiteSpace(user.UserName))
                        return user.UserName;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to get user name for userId {userId}");
            }
            return $"User {userId}";
        }

        public async Task ShowErrorAsync(string message)
        {
            _logger.LogError("Notification Extension Error: {Message}", message);
            // await NotificationHelper.ShowNotificationAsync("Error", message, NotificationType.Error); // Commented out potentially problematic direct call
            await ShowLocalNotificationAsync("Error", message, NotificationType.Error);
        }

        public async Task ShowSuccessAsync(string message)
        {
            _logger.LogInformation("Notification Extension Success: {Message}", message);
            // await NotificationHelper.ShowNotificationAsync("Success", message, NotificationType.Success); // Commented out potentially problematic direct call
             await ShowLocalNotificationAsync("Success", message, NotificationType.Success);
        }

        public async Task ShowWarningAsync(string message)
        {
            _logger.LogWarning("Notification Extension Warning: {Message}", message);
            // await NotificationHelper.ShowNotificationAsync("Warning", message, NotificationType.Warning);
            await ShowLocalNotificationAsync("Warning", message, NotificationType.Warning);
        }

        public async Task<bool> ScheduleNotificationAsync(string title, string message, DateTime deliveryTime, string data = null)
        {
            _logger.LogInformation("Scheduling notification: {Title} for {DeliveryTime}", title, deliveryTime);

            try
            {
                return await _platformService.ScheduleNotificationAsync(title, message, deliveryTime, data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scheduling notification");
                return false;
            }
        }

        public async Task<bool> CancelScheduledNotificationAsync(string id)
        {
            _logger.LogInformation("Cancelling scheduled notification ID: {Id}", id);
            try
            {
                return await _platformService.CancelScheduledNotificationAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error cancelling scheduled notification ID: {id}");
                return false;
            }
        }

        public async Task<IEnumerable<string>> GetScheduledNotificationIdsAsync()
        {
            _logger.LogInformation("Getting scheduled notification IDs");
            try
            {
                return await _platformService.GetScheduledNotificationIdsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting scheduled notification IDs");
                return Enumerable.Empty<string>();
            }
        }

        public async Task<bool> ClearAllScheduledNotificationsAsync()
        {
            _logger.LogInformation("Clearing all scheduled notifications");
            try
            {
                return await _platformService.ClearAllScheduledNotificationsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing all scheduled notifications");
                return false;
            }
        }

        // Removed incorrect override
        public async Task<bool> SendNotificationAsync(int receiverId, string message)
        {
            // This seems redundant with CreateNotificationAsync which handles platform notification.
            // Maybe delegate to the base service?
            return await _notificationService.SendNotificationAsync(receiverId, message);
        }

        private int GenerateLocalId()
        {
            // Simple local ID generation, could be improved
            return new Random().Next(10000, 99999);
        }

        // Helper to map DTO string type to NotificationType enum
        private NotificationType MapDtoTypeToEnumType(string dtoType)
        {
            return dtoType?.ToLowerInvariant() switch
            {
                "error" => NotificationType.Error,
                "warning" => NotificationType.Warning,
                "success" => NotificationType.Success,
                "info" => NotificationType.Info,
                "chat" => NotificationType.Info, // Map Chat to Info for now
                "notification" => NotificationType.Info,
                _ => NotificationType.Info // Default
            };
        }

        // Method to handle incoming notifications (e.g., from WebSocket) and raise the event
        public void HandleIncomingNotification(NotificationDto notification)
        {
             _logger.LogInformation("Handling incoming notification: {NotificationId}", notification.NotificationId);

             // Raise the event for listeners (e.g., UI)
             NotificationReceived?.Invoke(this, notification);

             // Map DTO type to Enum type
             var notificationEnumType = MapDtoTypeToEnumType(notification.Type);

             // Optionally show a platform notification immediately
             ShowLocalNotificationAsync(
                notification.Title ?? "Notification",
                notification.Message,
                notificationEnumType // Use mapped enum type
             ).ConfigureAwait(false);

             // Add to local history
            AddNotificationToHistoryAsync(
                notification.Title ?? "Notification",
                notification.Message,
                notificationEnumType // Use mapped enum type
            ).ConfigureAwait(false);
        }

        // Method to handle incoming chat messages (if this service is responsible)
        public void HandleIncomingChatMessage(MessageDto message)
        {
            _logger.LogInformation("Handling incoming chat message from {SenderId}", message.SenderId);
            // Potentially show a notification for new chat messages
            // Customize title/message based on message content/sender
            ShowLocalNotificationAsync(
                $"New message from {message.FromUsername}", // Use FromUsername
                "You have received a new message.", // Generic message as Content is not on base DTO
                NotificationType.Info, // Use Info type as Chat type might not exist
                message.Id.ToString() // Pass message ID as data
            ).ConfigureAwait(false);
        }

        // Method to handle message status updates (if this service is responsible)
        public void HandleMessageStatusUpdate(MessageStatusUpdateDto statusUpdate)
        {
            _logger.LogInformation("Handling message status update for {MessageId}: {Status}", statusUpdate.MessageId, statusUpdate.Status);
            // Potentially update UI or show a subtle notification
        }

    }
}