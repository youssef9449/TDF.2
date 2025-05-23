using System;
using TDFMAUI.Helpers;
using Microsoft.Extensions.Logging;
using TDFShared.Enums;

namespace TDFMAUI.Services
{
    public class PlatformNotificationService : IPlatformNotificationService
    {
        private readonly ILogger<PlatformNotificationService> _logger;
        private readonly ILocalStorageService _localStorage;

        public PlatformNotificationService(
            ILogger<PlatformNotificationService> logger,
            ILocalStorageService localStorage)
        {
            _logger = logger;
            _localStorage = localStorage;
        }

        public async Task<bool> ShowNotificationAsync(string title, string message, NotificationType notificationType = NotificationType.Info, string? data = null)
        {
            try
            {
                _logger.LogInformation("Showing notification: {Title} - {Message} (Type: {NotificationType})", title, message, notificationType);

                // Log notification for history
                await LogNotificationAsync(title, message, notificationType, data);

                if (DeviceHelper.IsDesktop)
                {
                    return await ShowDesktopNotificationAsync(title, message, notificationType, data);
                }
                else if (DeviceHelper.IsMobile)
                {
                    return await ShowMobileNotificationAsync(title, message, notificationType, data);
                }
                else
                {
                    // Fallback to in-app notification display
                    await ShowInAppNotificationAsync(title, message, notificationType, data);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing notification");
                return false;
            }
        }

        private async Task<bool> ShowDesktopNotificationAsync(string title, string message, NotificationType notificationType, string? data = null)
        {
            try
            {
                if (DeviceHelper.IsWindows)
                {
                    // Windows-specific notification code
                    await Task.Run(() =>
                    {
                        // Using Shell.Run to run PowerShell command for Windows Toast notifications
                        // For a complete implementation, this should be replaced with proper WinUI API calls
                        _logger.LogDebug("Showing Windows desktop notification");
                    });
                }
                else if (DeviceHelper.IsMacOS)
                {
                    // macOS-specific notification code
                    await Task.Run(() =>
                    {
                        _logger.LogDebug("Showing macOS desktop notification");
                    });
                }

                // For now, fall back to showing in-app notification
                await ShowInAppNotificationAsync(title, message, notificationType, data);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing desktop notification");
                await ShowInAppNotificationAsync(title, message, notificationType, data);
                return false;
            }
        }

        private async Task<bool> ShowMobileNotificationAsync(string title, string message, NotificationType notificationType, string? data = null)
        {
            try
            {
                // Request permission if not already granted
                var status = await GetNotificationPermissionAsync();
                if (status != PermissionStatus.Granted)
                {
                    _logger.LogWarning("Notification permission not granted");
                    // Fall back to in-app notification
                    await ShowInAppNotificationAsync(title, message, notificationType, data);
                    return false;
                }

                // Schedule a local notification
                if (DeviceHelper.IsAndroid || DeviceHelper.IsIOS)
                {
                    _logger.LogDebug($"Showing mobile notification on {DeviceInfo.Platform}");

                    // For a complete implementation, this should use platform-specific APIs
                    // or a cross-platform plugin like Plugin.LocalNotification

                    // For now, fall back to in-app notification
                    await ShowInAppNotificationAsync(title, message, notificationType, data);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing mobile notification");
                await ShowInAppNotificationAsync(title, message, notificationType, data);
                return false;
            }
        }

        private async Task ShowInAppNotificationAsync(string title, string message, NotificationType notificationType, string? data = null)
        {
            try
            {
                // This would display an in-app toast or snackbar
                // For a complete implementation, you could use an in-app notification library
                // or dispatch an event to be handled by the UI

                // We can dispatch an event to the application to show an in-app notification
                InAppNotificationRequested?.Invoke(this, new NotificationEventArgs
                {
                    Title = title,
                    Message = message,
                    Type = notificationType,
                    Timestamp = DateTime.Now,
                    NotificationId = 0,
                    SenderId = null,
                    SenderName = null,
                    IsBroadcast = false,
                    Department = null,
                    Data = data
                });

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing in-app notification");
            }
        }

        private async Task<PermissionStatus> GetNotificationPermissionAsync()
        {
            var status = PermissionStatus.Unknown;

            // Check if we have already recorded the permission status
            var savedStatus = await _localStorage.GetItemAsync<string>("notification_permission");
            if (!string.IsNullOrEmpty(savedStatus) && Enum.TryParse<PermissionStatus>(savedStatus, out var parsedStatus))
            {
                status = parsedStatus;
            }

            // If not granted or unknown, request permission
            if (status != PermissionStatus.Granted)
            {
                // Request notification permission (this would use platform APIs)
                // For a complete implementation, this should use Permissions.RequestAsync<Permissions.Notification>()

                // Simulating permission request for now
                status = PermissionStatus.Granted;

                // Save the new status
                await _localStorage.SetItemAsync("notification_permission", status.ToString());
            }

            return status;
        }

        private async Task LogNotificationAsync(string title, string message, NotificationType type, string? data = null)
        {
            try
            {
                // Retrieve existing notification history from local storage
                var history = await _localStorage.GetItemAsync<List<NotificationRecord>>("notification_history")
                    ?? new List<NotificationRecord>();

                // Add the new notification to history
                history.Add(new NotificationRecord
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = title,
                    Message = message,
                    Type = type,
                    Timestamp = DateTime.Now,
                    Data = data
                });

                // Only keep the last 100 notifications to avoid excessive storage
                if (history.Count > 100)
                {
                    history = history.OrderByDescending(n => n.Timestamp).Take(100).ToList();
                }

                // Save updated history
                await _localStorage.SetItemAsync("notification_history", history);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging notification to history");
            }
        }

        public async Task<List<NotificationRecord>> GetNotificationHistoryAsync()
        {
            try
            {
                var history = await _localStorage.GetItemAsync<List<NotificationRecord>>("notification_history");
                return history ?? new List<NotificationRecord>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving notification history");
                return new List<NotificationRecord>();
            }
        }

        public async Task ClearNotificationHistoryAsync()
        {
            try
            {
                await _localStorage.SetItemAsync("notification_history", new List<NotificationRecord>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing notification history");
            }
        }

        // Event for in-app notifications
        public event EventHandler<NotificationEventArgs> InAppNotificationRequested;

        public Task<bool> ShowLocalNotificationAsync(string title, string message, NotificationType type, string? data = null)
        {
            try
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await Application.Current.MainPage.DisplayAlert(title, message, "OK");
                });
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing local notification");
                return Task.FromResult(false);
            }
        }

        public Task<bool> ScheduleNotificationAsync(string title, string message, DateTime deliveryTime, string? data = null)
        {
            _logger.LogInformation("Scheduling notification: {Title} for {DeliveryTime}", title, deliveryTime);
            // Scheduled notifications would require platform-specific implementation
            return Task.FromResult(false);
        }

        public Task<bool> CancelScheduledNotificationAsync(string id)
        {
            _logger.LogInformation("Cancelling scheduled notification: {Id}", id);
            return Task.FromResult(false);
        }

        public Task<IEnumerable<string>> GetScheduledNotificationIdsAsync()
        {
            return Task.FromResult<IEnumerable<string>>(new List<string>());
        }

        public Task<bool> ClearAllScheduledNotificationsAsync()
        {
            _logger.LogInformation("Clearing all scheduled notifications");
            return Task.FromResult(false);
        }
    }
}