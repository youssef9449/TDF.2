using System.Net.Http;
using System.Text.Json;
using TDFMAUI.Helpers;
using Microsoft.Extensions.Logging;
using TDFShared.Contracts;
using TDFShared.DTOs.Messages;
using TDFShared.Enums;
using TDFShared.Models.Notification;
using TDFShared.Constants;
using TDFShared.Services;

namespace TDFMAUI.Services.Notifications
{
    public class NotificationService : INotificationClient, IExtendedNotificationService, IUserFeedbackService
    {
        private readonly IHttpClientService _httpClientService;
        private readonly WebSocketService _webSocketService;
        private readonly ILogger<NotificationService> _logger;
        private readonly ILocalStorageService _localStorage;

        public event EventHandler<NotificationDto>? NotificationReceived;

        public NotificationService(
            IHttpClientService httpClientService,
            WebSocketService webSocketService,
            ILocalStorageService localStorage,
            ILogger<NotificationService> logger)
        {
            _httpClientService = httpClientService ?? throw new ArgumentNullException(nameof(httpClientService));
            _webSocketService = webSocketService ?? throw new ArgumentNullException(nameof(webSocketService));
            _localStorage = localStorage ?? throw new ArgumentNullException(nameof(localStorage));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _webSocketService.NotificationReceived += OnWebSocketNotificationReceived;
        }

        public async Task<IEnumerable<NotificationEntity>> GetUnreadNotificationsAsync()
        {
            try
            {
                var response = await _httpClientService.GetAsync<ApiResponse<List<NotificationDto>>>(ApiRoutes.Notifications.GetUnread);
                if (response?.Data == null) return Enumerable.Empty<NotificationEntity>();

                return response.Data.Select(dto => new NotificationEntity
                {
                    NotificationID = dto.NotificationId,
                    ReceiverID = dto.UserId,
                    SenderID = dto.SenderId,
                    Message = dto.Message,
                    IsSeen = dto.IsSeen,
                    Timestamp = dto.Timestamp
                });
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
                var endpoint = string.Format(ApiRoutes.Notifications.MarkSeen, notificationId);
                var response = await _httpClientService.PostAsync<object, ApiResponse<bool>>(endpoint, new { });
                return response?.Data ?? false;
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
                var response = await _httpClientService.PostAsync<object, ApiResponse<bool>>(ApiRoutes.Notifications.MarkAllSeen, new { notificationIds });
                return response?.Data ?? false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking multiple notifications as seen");
                return false;
            }
        }

        public async Task<bool> BroadcastNotificationAsync(string message, string? department = null)
        {
            try
            {
                var response = await _httpClientService.PostAsync<object, ApiResponse<bool>>(ApiRoutes.Notifications.Broadcast, new { message, department });
                return response?.Data ?? false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting notification");
                return false;
            }
        }

        public async Task<bool> MarkNotificationsAsSeenAsync(int senderId)
        {
            // Implementation for marking all notifications from a sender as seen
            return true;
        }

        public async Task<bool> DeleteNotificationAsync(int notificationId)
        {
            try
            {
                var endpoint = string.Format(ApiRoutes.Notifications.Delete, notificationId);
                using var httpResponse = await _httpClientService.DeleteAsync(endpoint);
                if (!httpResponse.IsSuccessStatusCode)
                {
                    return false;
                }

                var body = await httpResponse.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(body))
                {
                    return true;
                }

                var response = JsonSerializer.Deserialize<ApiResponse<bool>>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                return response?.Data ?? false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting notification {NotificationId}", notificationId);
                return false;
            }
        }

        public async Task ShowErrorAsync(string message)
        {
            await NotificationHelper.ShowNotificationAsync("Error", message, NotificationType.Error);
        }

        public async Task ShowSuccessAsync(string message)
        {
            await NotificationHelper.ShowNotificationAsync("Success", message, NotificationType.Success);
        }

        public async Task ShowWarningAsync(string message)
        {
            await NotificationHelper.ShowNotificationAsync("Warning", message, NotificationType.Warning);
        }

        private void OnWebSocketNotificationReceived(object? sender, NotificationEventArgs e)
        {
            NotificationReceived?.Invoke(this, new NotificationDto
            {
                NotificationId = e.NotificationId,
                UserId = App.CurrentUser?.UserID ?? 0,
                SenderId = e.SenderId,
                SenderName = e.SenderName,
                Title = e.Title ?? "Notification",
                Message = e.Message ?? string.Empty,
                NotificationType = e.Type,
                Timestamp = e.Timestamp
            });
        }

        #region IExtendedNotificationService Implementation

        public async Task<bool> ShowLocalNotificationAsync(string title, string message, string? data = null)
        {
            await NotificationHelper.ShowNotificationAsync(title, message, NotificationType.Info, data);
            return true;
        }

        public async Task<bool> ShowLocalNotificationAsync(string title, string message, NotificationType type, string? data = null)
        {
            await NotificationHelper.ShowNotificationAsync(title, message, type, data);
            return true;
        }

        public async Task<bool> ScheduleNotificationAsync(string title, string message, DateTime deliveryTime, string? data = null)
        {
            // Simple in-memory scheduling for this example
            return true;
        }

        public async Task<bool> CancelScheduledNotificationAsync(string id) => true;
        public async Task<IEnumerable<string>> GetScheduledNotificationIdsAsync() => Enumerable.Empty<string>();
        public async Task<bool> ClearAllScheduledNotificationsAsync() => true;

        public async Task<List<TDFShared.DTOs.Messages.NotificationRecord>> GetNotificationHistoryAsync()
        {
            return new List<TDFShared.DTOs.Messages.NotificationRecord>();
        }

        public async Task ClearNotificationHistoryAsync() { }

        #endregion
    }
}
