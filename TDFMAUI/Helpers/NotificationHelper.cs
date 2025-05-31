using System;
using TDFMAUI.Controls;
using TDFMAUI.Services;
using TDFShared.Enums;

namespace TDFMAUI.Helpers
{
    /// <summary>
    /// Helper for managing notifications across the application
    /// </summary>
    public static class NotificationHelper
    {
        private static IExtendedNotificationService? _notificationService;
        private static IPlatformNotificationService? _platformNotificationService;
        private static readonly List<NotificationRecord> _notificationHistory = new List<NotificationRecord>();
        
        /// <summary>
        /// Initialize the notification helper with required services
        /// </summary>
        public static void Initialize(IServiceProvider serviceProvider)
        {
            _notificationService = serviceProvider.GetRequiredService<IExtendedNotificationService>();
            _platformNotificationService = serviceProvider.GetRequiredService<IPlatformNotificationService>();
            
            // Subscribe to in-app notification requests
            _platformNotificationService.LocalNotificationRequested += OnLocalNotificationRequested;
        }
        
        /// <summary>
        /// Handle in-app notification requests from the platform notification service
        /// </summary>
        private static void OnLocalNotificationRequested(object? sender, TDFShared.DTOs.Messages.NotificationEventArgs args)
        {
            try
            {
                // Get the current page to display the notification
                if (Application.Current?.MainPage != null)
                {
                    var currentPage = GetCurrentContentPage(Application.Current.MainPage);
                    if (currentPage != null)
                    {
                        // Show the notification toast on the UI thread
                        MainThread.BeginInvokeOnMainThread(async () => 
                        {
                            await NotificationToast.ShowToastAsync(
                                currentPage,
                                args.Title,
                                args.Message,
                                args.Type);
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error displaying local notification: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Show a local notification using the appropriate mechanism based on the platform
        /// </summary>
        public static async Task<bool> ShowNotificationAsync(
            string title, 
            string message, 
            NotificationType type = NotificationType.Info,
            string? data = null)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"NotificationHelper: Showing notification: {title}");
                
                // We no longer need to manually record the notification here since
                // PlatformNotificationService.ShowNotificationAsync will log it to history
                // with proper delivery status tracking
                
                if (_platformNotificationService != null)
                {
                    // Use the platform notification service which now properly tracks delivery status
                    bool result = await _platformNotificationService.ShowNotificationAsync(title, message, type, data);
                    
                    System.Diagnostics.Debug.WriteLine($"NotificationHelper: Platform notification result: {result}");
                    return result;
                }
                
                // Fallback if service not initialized - show in-app notification
                if (Application.Current?.MainPage != null)
                {
                    var currentPage = GetCurrentContentPage(Application.Current.MainPage);
                    if (currentPage != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"NotificationHelper: Showing toast notification");
                        await NotificationToast.ShowToastAsync(currentPage, title, message, type);
                        
                        // Since we're showing the notification directly, we should record it manually
                        // Note: This is a fallback path that should rarely be used
                        _notificationHistory.Add(new NotificationRecord
                        {
                            Title = title,
                            Message = message,
                            Type = type,
                            Timestamp = DateTime.Now,
                            Data = data ?? string.Empty,
                            WasDelivered = true,
                            DeliveryTime = DateTime.Now
                        });
                        
                        return true;
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"NotificationHelper: Failed to show notification - no valid page found");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"NotificationHelper: Error showing notification: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Get notification history
        /// </summary>
        public static async Task<List<NotificationRecord>> GetNotificationHistoryAsync()
        {
            try
            {
                if (_platformNotificationService != null)
                {
                    // Get notification history from the platform service
                    // This will include delivery status information
                    var sharedRecords = await _platformNotificationService.GetNotificationHistoryAsync();
                    
                    // Convert from shared DTOs to local NotificationRecord objects
                    var records = sharedRecords.Select(r => new NotificationRecord
                    {
                        Id = r.Id,
                        Title = r.Title,
                        Message = r.Message,
                        Type = r.Type,
                        Timestamp = r.Timestamp,
                        Data = r.Data
                        // Note: Delivery status fields aren't available in the shared DTO
                    }).ToList();
                    
                    return records;
                }
                
                // Fallback to local history if service not initialized
                return _notificationHistory;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"NotificationHelper: Error getting notification history: {ex.Message}");
                return new List<NotificationRecord>();
            }
        }
        
        /// <summary>
        /// Clear notification history
        /// </summary>
        public static async Task ClearNotificationHistoryAsync()
        {
            if (_platformNotificationService != null)
            {
                await _platformNotificationService.ClearNotificationHistoryAsync();
            }
        }
        
        /// <summary>
        /// Helper to find the current visible content page
        /// </summary>
        private static ContentPage GetCurrentContentPage(Page page)
        {
            // Handle different navigation container types
            switch (page)
            {
                case ContentPage contentPage:
                    return contentPage;
                    
                case FlyoutPage flyoutPage:
                    return GetCurrentContentPage(flyoutPage.Detail);
                    
                case TabbedPage tabbedPage:
                    return GetCurrentContentPage(tabbedPage.CurrentPage);
                    
                case NavigationPage navPage:
                    return GetCurrentContentPage(navPage.CurrentPage);
                    
                case Shell shell:
                    if (shell.CurrentPage is ContentPage shellContentPage)
                        return shellContentPage;
                        
                    if (shell.CurrentPage is NavigationPage shellNavPage)
                        return GetCurrentContentPage(shellNavPage);
                        
                    break;
            }
            
            return null;
        }
    }
}