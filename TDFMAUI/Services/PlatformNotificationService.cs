using System;
using TDFMAUI.Helpers;
using Microsoft.Extensions.Logging;
using TDFShared.Enums;
#if !WINDOWS && !MACCATALYST
using Plugin.LocalNotification;
using Plugin.LocalNotification.AndroidOption;
using Plugin.LocalNotification.iOSOption;
using Plugin.LocalNotification.EventArgs;
#endif
#if MACCATALYST
using UIKit;
using Foundation;
using UserNotifications;
#endif
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TDFShared.DTOs.Messages;
using TDFShared.Services;

namespace TDFMAUI.Services
{
    public class PlatformNotificationService : IPlatformNotificationService
    {
        private class ScheduledNotification
        {
            public string Id { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public DateTime DeliveryTime { get; set; }
            public string? Data { get; set; }
        }

        private const string SCHEDULED_NOTIFICATIONS_KEY = "scheduled_notifications";
        private const int MAX_SCHEDULED_NOTIFICATIONS = 50;
        private const int MAX_NOTIFICATION_LENGTH = 2000;
        private const int MAX_TITLE_LENGTH = 100;
        private const int MAX_RETRY_ATTEMPTS = 3;
        private const int RETRY_DELAY_MS = 1000;
        private const int CLEANUP_INTERVAL_MS = 3600000; // 1 hour
        private System.Threading.Timer _cleanupTimer;
        private readonly SemaphoreSlim _permissionSemaphore = new(1, 1);
        private readonly SemaphoreSlim _scheduleSemaphore = new(1, 1);
        private NotificationPermissionStatus _currentPermissionStatus = NotificationPermissionStatus.NotDetermined;

        public event EventHandler<TDFShared.DTOs.Messages.NotificationEventArgs> LocalNotificationRequested = delegate { };
        public NotificationPermissionStatus CurrentPermissionStatus => _currentPermissionStatus;

        private readonly ILogger<PlatformNotificationService> _logger;
        private readonly ILocalStorageService _localStorage;

        public PlatformNotificationService(
            ILogger<PlatformNotificationService> logger,
            ILocalStorageService localStorage)
        {
            _logger = logger;
            _localStorage = localStorage;
            
            // Initialize notification event handlers
            InitializeNotificationEventHandlers();
            
            // Initialize scheduled notifications
            InitializeScheduledNotificationsAsync().ConfigureAwait(false);
            
            // Initialize cleanup timer for notification ID mappings
            _cleanupTimer = new System.Threading.Timer(
                async (state) => await PerformPeriodicCleanupAsync(),
                null,
                CLEANUP_INTERVAL_MS, // Initial delay
                CLEANUP_INTERVAL_MS  // Periodic interval
            );
        }
        
        private async Task PerformPeriodicCleanupAsync()
        {
            try
            {
                _logger.LogInformation("Performing periodic notification cleanup");
                await CleanupNotificationIdMappingAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during periodic notification cleanup");
            }
        }
        
        private void InitializeNotificationEventHandlers()
        {
            try
            {
#if !WINDOWS && !MACCATALYST
                // Register for notification delivery events
                Plugin.LocalNotification.LocalNotificationCenter.Current.NotificationReceived += OnNotificationReceived;
                Plugin.LocalNotification.LocalNotificationCenter.Current.NotificationActionTapped += OnNotificationTapped;
#endif
                _logger.LogInformation("Notification event handlers initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing notification event handlers");
            }
        }
        
#if !WINDOWS
        private void OnNotificationReceived(Plugin.LocalNotification.EventArgs.NotificationEventArgs e)
        {
            try
            {
                _logger.LogInformation("Notification received: ID={NotificationId}, Title={Title}", 
                    e.Request.NotificationId, e.Request.Title);
                
                // Convert to our shared DTO type and raise our event
                var notificationArgs = new TDFShared.DTOs.Messages.NotificationEventArgs
                {
                    NotificationId = e.Request.NotificationId,
                    Title = e.Request.Title,
                    Message = e.Request.Description,
                    Data = e.Request.ReturningData
                };
                LocalNotificationRequested?.Invoke(this, notificationArgs);
                
                // Use Task.Run to avoid blocking the UI thread with async operations
                Task.Run(async () => 
                {
                    try
                    {
                        // First check if the tracking ID is directly available in the ReturningData
                        string trackingId = e.Request.ReturningData;
                        
                        // If not found in ReturningData, check the mapping
                        if (string.IsNullOrEmpty(trackingId))
                        {
                            // Get the mapping between system notification ID and our tracking ID
                            var notificationMap = await _localStorage.GetItemAsync<Dictionary<int, string>>("notification_id_map") 
                                ?? new Dictionary<int, string>();
                            
                            // Look up our tracking ID
                            notificationMap.TryGetValue(e.Request.NotificationId, out trackingId);
                        }
                        
                        if (!string.IsNullOrEmpty(trackingId))
                        {
                            // Update delivery status to indicate successful delivery
                            bool success = await UpdateNotificationDeliveryStatusAsync(trackingId, true);
                            
                            if (success)
                            {
                                _logger.LogInformation("Updated delivery status for notification {TrackingId}", trackingId);
                            }
                            else
                            {
                                _logger.LogWarning("Failed to update delivery status for notification {TrackingId}", trackingId);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Received notification with ID {NotificationId} but no tracking ID was found", 
                                e.Request.NotificationId);
                        }
                    }
                    catch (Exception innerEx)
                    {
                        _logger.LogError(innerEx, "Error updating notification delivery status");
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling notification received event");
            }
        }
        
        private void OnNotificationTapped(Plugin.LocalNotification.EventArgs.NotificationEventArgs e)
        {
            try
            {
                _logger.LogInformation("Notification tapped: ID={NotificationId}, Title={Title}", 
                    e.Request.NotificationId, e.Request.Title);
                
                // Convert to our shared DTO type and raise our event
                var notificationArgs = new TDFShared.DTOs.Messages.NotificationEventArgs
                {
                    NotificationId = e.Request.NotificationId,
                    Title = e.Request.Title,
                    Message = e.Request.Description,
                    Data = e.Request.ReturningData
                };
                LocalNotificationRequested?.Invoke(this, notificationArgs);
                
                // Use Task.Run to avoid blocking the UI thread with async operations
                Task.Run(async () => 
                {
                    try
                    {
                        // First check if the tracking ID is directly available in the ReturningData
                        string trackingId = e.Request.ReturningData;
                        
                        // If not found in ReturningData, check the mapping
                        if (string.IsNullOrEmpty(trackingId))
                        {
                            // Get the mapping between system notification ID and our tracking ID
                            var notificationMap = await _localStorage.GetItemAsync<Dictionary<int, string>>("notification_id_map") 
                                ?? new Dictionary<int, string>();
                            
                            // Look up our tracking ID
                            notificationMap.TryGetValue(e.Request.NotificationId, out trackingId);
                        }
                        
                        if (!string.IsNullOrEmpty(trackingId))
                        {
                            // First ensure it's marked as delivered
                            bool success = await UpdateNotificationDeliveryStatusAsync(trackingId, true);
                            
                            if (success)
                            {
                                _logger.LogInformation("Updated delivery status for notification {TrackingId}", trackingId);
                            }
                            else
                            {
                                _logger.LogWarning("Failed to update delivery status for notification {TrackingId}", trackingId);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Tapped notification with ID {NotificationId} but no tracking ID was found", 
                                e.Request.NotificationId);
                        }
                    }
                    catch (Exception innerEx)
                    {
                        _logger.LogError(innerEx, "Error updating notification delivery status");
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling notification tapped event");
            }
        }
#endif

        private async Task InitializeScheduledNotificationsAsync()
        {
            try
            {
                // Clean up any stale scheduled notifications
                var scheduledIds = await GetScheduledNotificationIdsAsync();
                foreach (var id in scheduledIds)
                {
                    if (!await ValidateScheduledNotificationAsync(id))
                    {
                        await CancelScheduledNotificationAsync(id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing scheduled notifications");
            }
        }

        private async Task<bool> ValidateScheduledNotificationAsync(string id)
        {
            try
            {
                if (!int.TryParse(id, out var notificationId))
                    return false;

                // Check if the notification exists in our stored list
                var scheduledNotifications = await GetScheduledNotificationsAsync();
                var notification = scheduledNotifications.FirstOrDefault(n => n.Id == id);
                
                // If not found in our list, it's invalid
                if (notification == null)
                    return false;

                // Check if the notification time is still in the future
                return notification.DeliveryTime > DateTime.Now;
            }
            catch
            {
                return false;
            }
        }

        private void ValidateNotificationContent(string title, string message)
        {
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("Notification title cannot be empty", nameof(title));
            
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Notification message cannot be empty", nameof(message));
            
            if (title.Length > MAX_TITLE_LENGTH)
                throw new ArgumentException($"Notification title exceeds maximum length of {MAX_TITLE_LENGTH} characters", nameof(title));
            
            if (message.Length > MAX_NOTIFICATION_LENGTH)
                throw new ArgumentException($"Notification message exceeds maximum length of {MAX_NOTIFICATION_LENGTH} characters", nameof(message));
        }

        private string SanitizeNotificationContent(string content)
        {
            if (string.IsNullOrEmpty(content))
                return content;

            // Remove any potentially harmful HTML/script content
            content = System.Text.RegularExpressions.Regex.Replace(content, "<[^>]*>", string.Empty);
            
            // Escape special characters for XML/HTML
            content = System.Security.SecurityElement.Escape(content);
            
            return content;
        }

        private async Task<bool> RetryOperationAsync(Func<Task<bool>> operation, string operationName)
        {
            for (int attempt = 1; attempt <= MAX_RETRY_ATTEMPTS; attempt++)
            {
                try
                {
                    var result = await operation();
                    if (result)
                        return true;

                    if (attempt < MAX_RETRY_ATTEMPTS)
                    {
                        _logger.LogWarning("Retry attempt {Attempt} for {Operation}", attempt, operationName);
                        await Task.Delay(RETRY_DELAY_MS * attempt);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during {Operation} (attempt {Attempt})", operationName, attempt);
                    if (attempt < MAX_RETRY_ATTEMPTS)
                        await Task.Delay(RETRY_DELAY_MS * attempt);
                }
            }
            return false;
        }

        public async Task<bool> ShowNotificationAsync(string title, string message, NotificationType notificationType = NotificationType.Info, string? data = null, DateTime? fireAt = null)
        {
            try
            {
                _logger.LogInformation("Showing notification: {Title} - {Message} (Type: {NotificationType})", title, message, notificationType);

                // Validate and sanitize content
                ValidateNotificationContent(title, message);
                title = SanitizeNotificationContent(title);
                message = SanitizeNotificationContent(message);

                // Log notification for history and get tracking ID
                string trackingId = await LogNotificationAsync(title, message, notificationType, data);

                // Check permission status
                if (_currentPermissionStatus != NotificationPermissionStatus.Granted)
                {
                    await _permissionSemaphore.WaitAsync();
                    try
                    {
                        if (_currentPermissionStatus != NotificationPermissionStatus.Granted)
                        {
                            var status = await RequestNotificationPermissionAsync();
                            if (status != NotificationPermissionStatus.Granted)
                            {
                                _logger.LogWarning("Notification permission not granted, falling back to in-app notification");
                                await ShowLocalNotificationAsync(title, message, notificationType, data);
                                return false;
                            }
                        }
                    }
                    finally
                    {
                        _permissionSemaphore.Release();
                    }
                }

                if (DeviceHelper.IsDesktop)
                {
                    return await RetryOperationAsync(
                        () => ShowDesktopNotificationAsync(title, message, notificationType, data),
                        "ShowDesktopNotification");
                }
                else if (DeviceHelper.IsMobile)
                {
                    return await RetryOperationAsync(
                        () => ShowMobileNotificationAsync(title, message, notificationType, data, fireAt),
                        "ShowMobileNotification");
                }
                else
                {
                    await ShowLocalNotificationAsync(title, message, notificationType, data);
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
                // Use the tracking ID passed from the parent method
                // If we need to create a new one, we'll use the title and message to create a consistent ID
                string trackingId = await LogNotificationAsync(title, message, notificationType, data, false);
                
#if WINDOWS
                try
                {
                    // Windows Toast Notification (WinUI 3)
                    var toastXmlString = $@"<toast><visual><binding template=""ToastGeneric""><text>{title}</text><text>{message}</text></binding></visual></toast>";
                    var xmlDoc = new Windows.Data.Xml.Dom.XmlDocument();
                    xmlDoc.LoadXml(toastXmlString);
                    var toast = new Windows.UI.Notifications.ToastNotification(xmlDoc);
                    Windows.UI.Notifications.ToastNotificationManager.CreateToastNotifier().Show(toast);
                    
                    // Mark as delivered since we can't track Windows toast notifications reliably
                    await UpdateNotificationDeliveryStatusAsync(trackingId, true);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error showing Windows toast notification");
                    await UpdateNotificationDeliveryStatusAsync(trackingId, false, ex.Message);
                    return false;
                }
#elif MACCATALYST
                try
                {
                    var notification = new NSUserNotification
                    {
                        Title = title,
                        InformativeText = message,
                        SoundName = NSUserNotification.NSUserNotificationDefaultSoundName
                    };
                    NSUserNotificationCenter.DefaultUserNotificationCenter.DeliverNotification(notification);
                    
                    // Mark as delivered since we can't track macOS notifications reliably
                    await UpdateNotificationDeliveryStatusAsync(trackingId, true);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error showing macOS notification");
                    await UpdateNotificationDeliveryStatusAsync(trackingId, false, ex.Message);
                    return false;
                }
#else
                _logger.LogWarning("Desktop notifications not implemented for this platform");
                // Mark as not delivered with a specific error message
                await UpdateNotificationDeliveryStatusAsync(trackingId, false, "Platform not supported");
                return false;
#endif
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing desktop notification");
                return false;
            }
        }

        private int GenerateNotificationId()
        {
            return new Random().Next(10000, 99999);
        }

        private async Task<bool> ShowMobileNotificationAsync(string title, string message, NotificationType notificationType, string? data = null, DateTime? fireAt = null)
        {
            try
            {
                // Request permission if not already granted
                var status = await RequestNotificationPermissionAsync();
                if (status != NotificationPermissionStatus.Granted)
                {
                    _logger.LogWarning("Notification permission not granted");
                    // Fall back to in-app notification
                    await ShowLocalNotificationAsync(title, message, notificationType, data);
                    return false;
                }

#if !WINDOWS
                // Generate a unique notification ID
                var notificationId = GenerateNotificationId();
                
                // Log the notification for tracking
                string trackingId = await LogNotificationAsync(title, message, notificationType, data, false);
                
                // Store the mapping between system notification ID and our tracking ID
                var notificationMap = await _localStorage.GetItemAsync<Dictionary<int, string>>("notification_id_map") 
                    ?? new Dictionary<int, string>();
                
                notificationMap[notificationId] = trackingId;
                await _localStorage.SetItemAsync("notification_id_map", notificationMap);

                var request = new NotificationRequest
                {
                    NotificationId = notificationId,
                    Title = title,
                    Description = message,
                    ReturningData = trackingId,
                    CategoryType = NotificationCategoryType.Status,
                    Android = new AndroidOptions
                    {
                        Priority = AndroidPriority.High,
                        VisibilityType = AndroidVisibilityType.Public
                    },
                    iOS = new iOSOptions()
                };
                request.Schedule = fireAt.HasValue ? new NotificationRequestSchedule
                {
                    NotifyTime = fireAt.Value
                } : null;

                await LocalNotificationCenter.Current.Show(request);

                // If this is an immediate notification (not scheduled), mark it as delivered
                if (!fireAt.HasValue)
                {
                    await UpdateNotificationDeliveryStatusAsync(trackingId, true);
                }

                return true;
#else
                // For Windows, use in-app notification
                await ShowLocalNotificationAsync(title, message, notificationType, data);
                return false;
#endif
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing mobile notification");
                
                // Log the failed notification with error details
                await LogNotificationAsync(title, message, notificationType, data, false, ex.Message);
                
                // Fall back to in-app notification
                await ShowLocalNotificationAsync(title, message, notificationType, data);
                return false;
            }
        }

        public Task<bool> ShowLocalNotificationAsync(string title, string message, NotificationType type, string? data = null)
        {
            try
            {
                var args = new TDFShared.DTOs.Messages.NotificationEventArgs
                {
                    Title = title,
                    Message = message,
                    Type = type,
                    Timestamp = DateTime.Now,
                    NotificationId = 0,
                    SenderId = null,
                    SenderName = null,
                    IsBroadcast = false,
                    Department = null,
                    Data = data
                };
                LocalNotificationRequested?.Invoke(this, args);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing local notification");
                return Task.FromResult(false);
            }
        }

        private async Task<string> LogNotificationAsync(string title, string message, NotificationType type, string? data = null, bool wasDelivered = true, string? deliveryError = null)
        {
            try
            {
                var history = await _localStorage.GetItemAsync<List<TDFMAUI.Helpers.NotificationRecord>>("notification_history")
                    ?? new List<TDFMAUI.Helpers.NotificationRecord>();

                // Generate a unique tracking ID for this notification
                string trackingId = Guid.NewGuid().ToString();
                
                history.Add(new TDFMAUI.Helpers.NotificationRecord
                {
                    Id = trackingId,
                    Title = title,
                    Message = message,
                    Type = type,
                    Timestamp = DateTime.Now,
                    Data = data,
                    WasDelivered = wasDelivered,
                    DeliveryTime = wasDelivered ? DateTime.Now : null,
                    DeliveryError = deliveryError,
                    RetryCount = 0
                });

                if (history.Count > 100)
                {
                    history = history.OrderByDescending(n => n.Timestamp).Take(100).ToList();
                }

                await _localStorage.SetItemAsync("notification_history", history);
                
                // Return the tracking ID so it can be used for delivery status updates
                return trackingId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging notification to history");
                return Guid.NewGuid().ToString(); // Fallback to a new ID if logging fails
            }
        }

        public async Task<List<TDFShared.DTOs.Messages.NotificationRecord>> GetNotificationHistoryAsync()
        {
            try
            {
                var localHistory = await _localStorage.GetItemAsync<List<TDFMAUI.Helpers.NotificationRecord>>("notification_history");
                if (localHistory == null || !localHistory.Any())
                {
                    return new List<TDFShared.DTOs.Messages.NotificationRecord>();
                }
                
                // Convert from local NotificationRecord to shared NotificationRecord
                return localHistory.Select(local => new TDFShared.DTOs.Messages.NotificationRecord
                {
                    Id = local.Id,
                    Title = local.Title,
                    Message = local.Message,
                    Type = local.Type,
                    Timestamp = local.Timestamp,
                    Data = local.Data
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving notification history");
                return new List<TDFShared.DTOs.Messages.NotificationRecord>();
            }
        }

        public async Task ClearNotificationHistoryAsync()
        {
            try
            {
                await _localStorage.SetItemAsync("notification_history", new List<TDFMAUI.Helpers.NotificationRecord>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing notification history");
            }
        }

        public async Task<bool> ScheduleNotificationAsync(string title, string message, DateTime deliveryTime, string? data = null)
        {
            try
            {
                // Validate the notification content
                ValidateNotificationContent(title, message);

                // Get current scheduled notifications
                var notifications = await GetScheduledNotificationsAsync();

                // Check if we've reached the maximum limit
                if (notifications.Count >= MAX_SCHEDULED_NOTIFICATIONS)
                {
                    _logger.LogWarning("Maximum number of scheduled notifications reached");
                    return false;
                }

                // Create a new scheduled notification
                var notification = new ScheduledNotification
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = title,
                    Message = message,
                    DeliveryTime = deliveryTime,
                    Data = data
                };

                // Add to the list
                notifications.Add(notification);

                // Save the updated list
                await SaveScheduledNotificationsAsync(notifications);

                // Schedule the actual notification
                return await ShowNotificationAsync(title, message, NotificationType.Info, data, deliveryTime);
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
                // Get current scheduled notifications
                var notifications = await GetScheduledNotificationsAsync();

                // Find and remove the notification
                var notification = notifications.FirstOrDefault(n => n.Id == id);
                if (notification != null)
                {
                    notifications.Remove(notification);
                    await SaveScheduledNotificationsAsync(notifications);
                    return true;
                }

                return false;
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
                var notifications = await GetScheduledNotificationsAsync();
                return notifications.Select(n => n.Id);
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
                await SaveScheduledNotificationsAsync(new List<ScheduledNotification>());
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing all scheduled notifications");
                return false;
            }
        }

        /// <summary>
        /// Requests notification permission from the user using platform APIs.
        /// </summary>
        public async Task<NotificationPermissionStatus> RequestNotificationPermissionAsync(Page? page = null)
        {
#if ANDROID
            var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
            if (status != PermissionStatus.Granted)
            {
                if (Permissions.ShouldShowRationale<Permissions.PostNotifications>() && page != null)
                {
                    bool showSettings = await page.DisplayAlert(
                        "Permission Required",
                        "This app needs notification permission to alert you about important events. Please enable notifications.",
                        "Go to Settings", "Cancel");
                    if (showSettings)
                    {
                        AppInfo.ShowSettingsUI();
                        _currentPermissionStatus = NotificationPermissionStatus.Denied;
                        return _currentPermissionStatus;
                    }
                }
                status = await Permissions.RequestAsync<Permissions.PostNotifications>();
            }
            _currentPermissionStatus = status switch
            {
                PermissionStatus.Granted => NotificationPermissionStatus.Granted,
                PermissionStatus.Denied => NotificationPermissionStatus.Denied,
                _ => NotificationPermissionStatus.NotDetermined
            };
#elif IOS
            // Use platform-specific service for iOS notification permission
            var platformService = Application.Current?.Handler?.MauiContext?.Services?.GetService<INotificationPermissionPlatformService>();
            if (platformService != null)
            {
                bool granted = await platformService.RequestPlatformNotificationPermissionAsync();
                _currentPermissionStatus = granted ? NotificationPermissionStatus.Granted : NotificationPermissionStatus.Denied;
            }
            else
            {
                _currentPermissionStatus = NotificationPermissionStatus.NotDetermined;
            }
#elif MACCATALYST
            // Mac Catalyst: notifications are enabled by default, so assume granted
            _currentPermissionStatus = NotificationPermissionStatus.Granted;
#elif WINDOWS
            // Windows: Assume permission is granted (user controls via OS settings)
            _currentPermissionStatus = NotificationPermissionStatus.Granted;
#else
            _currentPermissionStatus = NotificationPermissionStatus.NotDetermined;
#endif
            return _currentPermissionStatus;
        }

        private async Task<List<ScheduledNotification>> GetScheduledNotificationsAsync()
        {
            try
            {
                var notifications = await _localStorage.GetItemAsync<List<ScheduledNotification>>(SCHEDULED_NOTIFICATIONS_KEY);
                return notifications ?? new List<ScheduledNotification>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving scheduled notifications");
                return new List<ScheduledNotification>();
            }
        }

        private async Task SaveScheduledNotificationsAsync(List<ScheduledNotification> notifications)
        {
            try
            {
                await _localStorage.SetItemAsync(SCHEDULED_NOTIFICATIONS_KEY, notifications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving scheduled notifications");
                throw;
            }
        }

        public async Task<bool> UpdateScheduledNotificationAsync(string id, string title, string message, DateTime newDeliveryTime, string? data = null)
        {
            try
            {
                await _scheduleSemaphore.WaitAsync();
                try
                {
                    var notifications = await GetScheduledNotificationsAsync();
                    var notification = notifications.FirstOrDefault(n => n.Id == id);
                    
                    if (notification == null)
                    {
                        _logger.LogWarning("Attempted to update non-existent notification: {NotificationId}", id);
                        return false;
                    }

                    // Validate input
                    if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(message))
                    {
                        _logger.LogWarning("Invalid notification content for update");
                        return false;
                    }

                    if (newDeliveryTime <= DateTime.Now)
                    {
                        _logger.LogWarning("Attempted to schedule notification in the past");
                        return false;
                    }

                    // Update notification
                    notification.Title = title;
                    notification.Message = message;
                    notification.DeliveryTime = newDeliveryTime;
                    notification.Data = data;

                    await SaveScheduledNotificationsAsync(notifications);
                    _logger.LogInformation("Successfully updated scheduled notification: {NotificationId}", id);
                    return true;
                }
                finally
                {
                    _scheduleSemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating scheduled notification: {NotificationId}", id);
                return false;
            }
        }

        /// <summary>
        /// Updates the delivery status of a notification
        /// </summary>
        /// <param name="notificationId">The tracking ID of the notification</param>
        /// <param name="wasDelivered">Whether the notification was successfully delivered</param>
        /// <param name="error">Optional error message if delivery failed</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task<bool> UpdateNotificationDeliveryStatusAsync(string notificationId, bool wasDelivered, string? error = null)
        {
            try
            {
                if (string.IsNullOrEmpty(notificationId))
                {
                    _logger.LogWarning("Cannot update delivery status: Invalid notification ID");
                    return false;
                }
                
                var history = await _localStorage.GetItemAsync<List<TDFMAUI.Helpers.NotificationRecord>>("notification_history")
                    ?? new List<TDFMAUI.Helpers.NotificationRecord>();

                var notification = history.FirstOrDefault(n => n.Id == notificationId);
                if (notification != null)
                {
                    // Update the notification status
                    notification.WasDelivered = wasDelivered;
                    notification.DeliveryTime = wasDelivered ? DateTime.Now : null;
                    notification.DeliveryError = error;
                    notification.RetryCount++;

                    await _localStorage.SetItemAsync("notification_history", history);
                    
                    // If this was a successful delivery, clean up the notification ID mapping
                    if (wasDelivered)
                    {
                        await CleanupNotificationIdMappingAsync();
                        
                        // Raise an event or perform any additional actions for successful delivery
                        _logger.LogInformation("Notification {NotificationId} was successfully delivered", notificationId);
                    }
                    else if (!string.IsNullOrEmpty(error))
                    {
                        // Handle failure case with specific error
                        _logger.LogWarning("Notification {NotificationId} delivery failed: {Error}", notificationId, error);
                        
                        // Implement retry logic if needed
                        if (notification.RetryCount <= 3)
                        {
                            _logger.LogInformation("Will retry notification {NotificationId} delivery (attempt {RetryCount}/3)", 
                                notificationId, notification.RetryCount);
                            // Retry logic would go here
                        }
                    }
                    
                    _logger.LogInformation("Updated delivery status for notification {NotificationId}: Delivered={WasDelivered}, Error={Error}",
                        notificationId, wasDelivered, error ?? "None");
                    
                    return true;
                }
                else
                {
                    _logger.LogWarning("Attempted to update delivery status for unknown notification: {NotificationId}", notificationId);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating notification delivery status");
                return false;
            }
        }
        
        /// <summary>
        /// Cleans up old notification ID mappings to prevent memory leaks
        /// </summary>
        private async Task CleanupNotificationIdMappingAsync()
        {
            try
            {
                // Get the current notification ID map
                var notificationMap = await _localStorage.GetItemAsync<Dictionary<int, string>>("notification_id_map") 
                    ?? new Dictionary<int, string>();
                
                if (notificationMap.Count == 0)
                    return;
                
                // Get the notification history
                var history = await _localStorage.GetItemAsync<List<TDFMAUI.Helpers.NotificationRecord>>("notification_history")
                    ?? new List<TDFMAUI.Helpers.NotificationRecord>();
                
                // Create a set of all tracking IDs that have been delivered
                var deliveredIds = new HashSet<string>(
                    history.Where(n => n.WasDelivered).Select(n => n.Id)
                );
                
                // Find all notification IDs that map to delivered tracking IDs
                var idsToRemove = notificationMap
                    .Where(kvp => deliveredIds.Contains(kvp.Value))
                    .Select(kvp => kvp.Key)
                    .ToList();
                
                // Remove them from the map
                foreach (var id in idsToRemove)
                {
                    notificationMap.Remove(id);
                }
                
                // Save the updated map
                await _localStorage.SetItemAsync("notification_id_map", notificationMap);
                
                if (idsToRemove.Count > 0)
                {
                    _logger.LogInformation("Cleaned up {Count} notification ID mappings", idsToRemove.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up notification ID mappings");
            }
        }

        public void Dispose()
        {
            try
            {
                // Unregister event handlers
#if !WINDOWS
                Plugin.LocalNotification.LocalNotificationCenter.Current.NotificationReceived -= OnNotificationReceived;
                Plugin.LocalNotification.LocalNotificationCenter.Current.NotificationActionTapped -= OnNotificationTapped;
#endif
                
                // Dispose the cleanup timer
                _cleanupTimer?.Dispose();
                
                // Perform one final cleanup of notification ID mappings
                Task.Run(async () => 
                {
                    try 
                    {
                        await CleanupNotificationIdMappingAsync();
                    }
                    catch (Exception cleanupEx)
                    {
                        _logger.LogError(cleanupEx, "Error during final notification ID mapping cleanup");
                    }
                }).Wait(1000); // Wait up to 1 second for cleanup to complete
                
                _logger.LogInformation("PlatformNotificationService disposed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing PlatformNotificationService");
            }
        }

        #if MACCATALYST
        private async Task ShowMacNotificationAsync(string title, string message, string? data = null)
        {
            try
            {
                var content = new UNMutableNotificationContent
                {
                    Title = title,
                    Body = message,
                    Sound = UNNotificationSound.Default
                };

                if (!string.IsNullOrEmpty(data))
                {
                    content.UserInfo = new NSDictionary("data", data);
                }

                var request = UNNotificationRequest.FromIdentifier(
                    Guid.NewGuid().ToString(),
                    content,
                    null);

                await UNUserNotificationCenter.Current.AddNotificationRequestAsync(request);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing macOS notification: {ex.Message}");
            }
        }
        #endif
    }
}