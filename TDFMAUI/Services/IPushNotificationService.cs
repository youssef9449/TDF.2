using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TDFShared.DTOs.Messages;
using TDFShared.Enums;

namespace TDFMAUI.Services
{
    /// <summary>
    /// Service for managing push notifications on the client side
    /// </summary>
    public interface IPushNotificationService
    {
        /// <summary>
        /// Event that fires when a notification is received
        /// </summary>
        event EventHandler<TDFShared.DTOs.Messages.NotificationEventArgs> NotificationReceived;

        /// <summary>
        /// Register the device for push notifications
        /// </summary>
        /// <returns>True if registration was successful</returns>
        Task<bool> RegisterTokenAsync();

        /// <summary>
        /// Unregister the device from push notifications
        /// </summary>
        /// <returns>True if unregistration was successful</returns>
        Task<bool> UnregisterTokenAsync();

        /// <summary>
        /// Show a local notification on the device
        /// </summary>
        /// <param name="title">Notification title</param>
        /// <param name="message">Notification message</param>
        /// <param name="type">Notification type</param>
        /// <param name="data">Additional data for the notification</param>
        /// <returns>True if the notification was shown successfully</returns>
        Task<bool> ShowLocalNotificationAsync(string title, string message, NotificationType type = NotificationType.Info, string? data = null);

        /// <summary>
        /// Get the notification history
        /// </summary>
        /// <returns>List of notification records</returns>
        Task<List<NotificationRecord>> GetNotificationHistoryAsync();

        /// <summary>
        /// Clear the notification history
        /// </summary>
        /// <returns>True if the history was cleared successfully</returns>
        Task<bool> ClearNotificationHistoryAsync();

        /// <summary>
        /// Schedule a notification to be delivered at a specific time
        /// </summary>
        /// <param name="title">Notification title</param>
        /// <param name="message">Notification message</param>
        /// <param name="deliveryTime">When to deliver the notification</param>
        /// <param name="data">Additional data for the notification</param>
        /// <returns>True if the notification was scheduled successfully</returns>
        Task<bool> ScheduleNotificationAsync(string title, string message, DateTime deliveryTime, string? data = null);

        /// <summary>
        /// Cancel a scheduled notification
        /// </summary>
        /// <param name="notificationId">ID of the notification to cancel</param>
        /// <returns>True if the notification was canceled successfully</returns>
        Task<bool> CancelScheduledNotificationAsync(string notificationId);

        /// <summary>
        /// Get the IDs of all scheduled notifications
        /// </summary>
        /// <returns>Collection of notification IDs</returns>
        Task<IEnumerable<string>> GetScheduledNotificationIdsAsync();
    }
}