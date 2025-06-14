using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TDFShared.DTOs.Messages;
using TDFShared.Enums;

namespace TDFMAUI.Services
{
    public interface IPlatformNotificationService
    {
        /// <summary>
        /// Shows a notification using the appropriate platform-specific mechanism
        /// </summary>
        /// <param name="title">The notification title</param>
        /// <param name="message">The notification message body</param>
        /// <param name="notificationType">The type/severity of the notification</param>
        /// <param name="data">Additional data for the notification</param>
        /// <param name="fireAt">Optional: The time to schedule the notification. If null, show immediately.</param>
        /// <returns>True if the notification was successfully shown or scheduled</returns>
        Task<bool> ShowNotificationAsync(string title, string message, NotificationType notificationType = NotificationType.Info, string? data = null, DateTime? fireAt = null);
        
        /// <summary>
        /// Shows a local platform-specific notification
        /// </summary>
        /// <param name="title">The notification title</param>
        /// <param name="message">The notification message</param>
        /// <param name="type">The type/severity of the notification</param>
        /// <param name="data">Additional data for the notification</param>
        /// <returns>True if the notification was shown successfully</returns>
        Task<bool> ShowLocalNotificationAsync(string title, string message, NotificationType type, string? data = null);
        
        /// <summary>
        /// Schedules a notification to be delivered at a specific time
        /// </summary>
        /// <param name="title">The notification title</param>
        /// <param name="message">The notification message</param>
        /// <param name="deliveryTime">The delivery time of the notification</param>
        /// <param name="data">Additional data for the notification</param>
        /// <returns>True if the notification was scheduled successfully</returns>
        Task<bool> ScheduleNotificationAsync(string title, string message, DateTime deliveryTime, string? data = null);
        
        /// <summary>
        /// Cancels a scheduled notification
        /// </summary>
        /// <param name="id">The ID of the scheduled notification</param>
        /// <returns>True if the notification was canceled successfully</returns>
        Task<bool> CancelScheduledNotificationAsync(string id);
        
        /// <summary>
        /// Updates an existing scheduled notification
        /// </summary>
        /// <param name="id">The ID of the scheduled notification to update</param>
        /// <param name="title">The new notification title</param>
        /// <param name="message">The new notification message</param>
        /// <param name="newDeliveryTime">The new delivery time</param>
        /// <param name="data">Additional data for the notification</param>
        /// <returns>True if the notification was updated successfully</returns>
        Task<bool> UpdateScheduledNotificationAsync(string id, string title, string message, DateTime newDeliveryTime, string? data = null);
        
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
        /// Retrieves the history of notifications that have been shown
        /// </summary>
        /// <returns>A list of notification history items</returns>
        Task<List<NotificationRecord>> GetNotificationHistoryAsync();
        
        /// <summary>
        /// Clears the notification history
        /// </summary>
        Task ClearNotificationHistoryAsync();
        
        /// <summary>
        /// Event that is triggered when an in-app notification should be shown
        /// </summary>
        event EventHandler<TDFShared.DTOs.Messages.NotificationEventArgs> LocalNotificationRequested;
    }
}