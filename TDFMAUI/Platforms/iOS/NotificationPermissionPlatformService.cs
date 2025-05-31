using System.Threading.Tasks;
using TDFMAUI.Services;
using UserNotifications;
using Microsoft.Extensions.Logging;

namespace TDFMAUI.Platforms.iOS
{
    /// <summary>
    /// iOS implementation of notification permission service.
    /// </summary>
    public class NotificationPermissionPlatformService : INotificationPermissionPlatformService
    {
        private readonly ILogger<NotificationPermissionPlatformService> _logger;
        private bool _isRequestingPermission;

        public NotificationPermissionPlatformService(ILogger<NotificationPermissionPlatformService> logger)
        {
            _logger = logger;
        }

        public async Task<bool> RequestPlatformNotificationPermissionAsync()
        {
            if (_isRequestingPermission)
            {
                _logger.LogWarning("Notification permission request already in progress");
                return false;
            }

            try
            {
                _isRequestingPermission = true;
                _logger.LogInformation("Requesting iOS notification permission");

                // Check current authorization status
                var currentSettings = await UNUserNotificationCenter.Current.GetNotificationSettingsAsync();
                if (currentSettings.AuthorizationStatus == UNAuthorizationStatus.Authorized)
                {
                    _logger.LogInformation("Notification permission already granted");
                    return true;
                }

                var tcs = new TaskCompletionSource<bool>();
                UNUserNotificationCenter.Current.RequestAuthorization(
                    UNAuthorizationOptions.Alert | UNAuthorizationOptions.Badge | UNAuthorizationOptions.Sound,
                    (granted, error) =>
                    {
                        if (error != null)
                        {
                            _logger.LogError("Error requesting notification permission: {Error}", error.LocalizedDescription);
                            tcs.SetResult(false);
                            return;
                        }

                        _logger.LogInformation("Notification permission request result: {Granted}", granted);
                        tcs.SetResult(granted);
                    });

                return await tcs.Task;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while requesting notification permission");
                return false;
            }
            finally
            {
                _isRequestingPermission = false;
            }
        }

        public async Task<UNAuthorizationStatus> GetCurrentAuthorizationStatusAsync()
        {
            try
            {
                var settings = await UNUserNotificationCenter.Current.GetNotificationSettingsAsync();
                return settings.AuthorizationStatus;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current notification authorization status");
                return UNAuthorizationStatus.NotDetermined;
            }
        }
    }
} 