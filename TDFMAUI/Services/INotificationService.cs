using System;
using TDFShared.DTOs.Messages;
using TDFShared.Models.Notification;

namespace TDFMAUI.Services
{
    public interface INotificationService
    {
        event EventHandler<NotificationDto> NotificationReceived;
        Task<IEnumerable<NotificationEntity>> GetUnreadNotificationsAsync();
        Task<bool> MarkAsSeenAsync(int notificationId);
        Task<bool> MarkNotificationsAsSeenAsync(IEnumerable<int> notificationIds);
        /// <summary>
        /// Creates a notification for a user, optionally scheduled for a future time.
        /// </summary>
        /// <param name="receiverId">The user to receive the notification.</param>
        /// <param name="message">The notification message.</param>
        /// <param name="senderId">The sender's user ID (optional).</param>
        /// <param name="fireAt">Optional: The time to schedule the notification. If null, send immediately.</param>
        /// <returns>True if the notification was created or scheduled successfully.</returns>
        Task<bool> CreateNotificationAsync(int receiverId, string message, int? senderId = null, DateTime? fireAt = null);
        Task<bool> BroadcastNotificationAsync(string message, string department = null);
        Task<bool> SendChatMessageAsync(int receiverId, string message, bool queueIfOffline = true);
        Task<bool> MarkMessagesAsReadAsync(int senderId);
        Task<bool> MarkMessagesAsDeliveredAsync(int senderId);
        Task<bool> MarkMessagesAsDeliveredAsync(IEnumerable<int> messageIds);
        Task<int> GetUnreadMessagesCountAsync();
        
        // Add SendNotificationAsync as an alias for CreateNotificationAsync
        Task<bool> SendNotificationAsync(int receiverId, string message);
        
        // Add missing methods used by RequestApprovalViewModel
        Task ShowErrorAsync(string message);
        Task ShowSuccessAsync(string message);
        Task ShowWarningAsync(string message);
    }
} 