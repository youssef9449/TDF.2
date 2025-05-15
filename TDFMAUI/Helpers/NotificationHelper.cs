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
        private static IExtendedNotificationService _notificationService;
        private static IPlatformNotificationService _platformNotificationService;
        private static readonly List<NotificationRecord> _notificationHistory = new List<NotificationRecord>();
        
        /// <summary>
        /// Initialize the notification helper with required services
        /// </summary>
        public static void Initialize(IServiceProvider serviceProvider)
        {
            _notificationService = serviceProvider.GetRequiredService<IExtendedNotificationService>();
            _platformNotificationService = serviceProvider.GetRequiredService<IPlatformNotificationService>();
            
            // Subscribe to in-app notification requests
            _platformNotificationService.InAppNotificationRequested += OnInAppNotificationRequested;
        }
        
        /// <summary>
        /// Handle in-app notification requests from the platform notification service
        /// </summary>
        private static void OnInAppNotificationRequested(object sender, NotificationEventArgs args)
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
                System.Diagnostics.Debug.WriteLine($"Error displaying in-app notification: {ex.Message}");
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
            // Record the notification
            _notificationHistory.Add(new NotificationRecord
            {
                Title = title,
                Message = message,
                Type = type,
                Timestamp = DateTime.Now,
                Data = data
            });

            // Assuming the operation is successful if no exceptions occur
            // Actual notification display logic might be asynchronous and return a more meaningful boolean
            // return await Task.FromResult(true); // This line made the subsequent code unreachable

            if (_platformNotificationService != null)
            {
                return await _platformNotificationService.ShowNotificationAsync(title, message, type, data);
            }
            
            // Fallback if service not initialized
            if (Application.Current?.MainPage != null)
            {
                var currentPage = GetCurrentContentPage(Application.Current.MainPage);
                if (currentPage != null)
                {
                    await NotificationToast.ShowToastAsync(currentPage, title, message, type);
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Get notification history
        /// </summary>
        public static async Task<List<NotificationRecord>> GetNotificationHistoryAsync()
        {
            return Task.FromResult(_notificationHistory.ToList()).Result;
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