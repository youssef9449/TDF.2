using System;
using TDFShared.DTOs.Messages;
using TDFShared.Enums;

namespace TDFMAUI.Services
{
    /// <summary>
    /// Extended notification service interface for additional notification features
    /// </summary>
    public interface IExtendedNotificationService : INotificationService
    {
        /// <summary>
        /// Shows a local platform-specific notification
        /// </summary>
        /// <param name="title">The notification title</param>
        /// <param name="message">The notification message</param>
        /// <param name="data">Additional data for the notification</param>
        /// <returns>True if the notification was shown successfully</returns>
        Task<bool> ShowLocalNotificationAsync(string title, string message, string data = null);
        
        /// <summary>
        /// Shows a local platform-specific notification with type parameter
        /// </summary>
        /// <param name="title">The notification title</param>
        /// <param name="message">The notification message</param>
        /// <param name="type">The type/severity of the notification</param>
        /// <param name="data">Additional data for the notification</param>
        /// <returns>True if the notification was shown successfully</returns>
        Task<bool> ShowLocalNotificationAsync(string title, string message, NotificationType type, string data = null);
        
        /// <summary>
        /// Schedules a notification to be delivered at a specific time
        /// </summary>
        /// <param name="title">The notification title</param>
        /// <param name="message">The notification message</param>
        /// <param name="deliveryTime">The delivery time of the notification</param>
        /// <param name="data">Additional data for the notification</param>
        /// <returns>True if the notification was scheduled successfully</returns>
        Task<bool> ScheduleNotificationAsync(string title, string message, DateTime deliveryTime, string data = null);
        
        /// <summary>
        /// Cancels a scheduled notification
        /// </summary>
        /// <param name="id">The ID of the scheduled notification</param>
        /// <returns>True if the notification was canceled successfully</returns>
        Task<bool> CancelScheduledNotificationAsync(string id);
        
        /// <summary>
        /// Gets the IDs of all scheduled notifications
        /// </summary>
        /// <returns>A list of scheduled notification IDs</returns>
        Task<IEnumerable<string>> GetScheduledNotificationIdsAsync();
        
        /// <summary>
        /// Clears all scheduled notifications
        /// </summary>
        /// <returns>True if all notifications were cleared successfully</returns>
        Task<bool> ClearAllScheduledNotificationsAsync();
        
        /// <summary>
        /// Gets the history of notifications that have been shown on this device
        /// </summary>
        /// <returns>A list of notification history items</returns>
        Task<List<NotificationRecord>> GetNotificationHistoryAsync();
        
        /// <summary>
        /// Clears the local notification history
        /// </summary>
        Task ClearNotificationHistoryAsync();
    }
} 