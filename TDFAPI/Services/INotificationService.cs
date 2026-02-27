using System.Collections.Generic;
using System.Threading.Tasks;
using TDFShared.DTOs.Messages;
using TDFShared.Enums;
using TDFShared.Models.Message;
using TDFShared.Models.Notification;
using System.Net.WebSockets;

namespace TDFAPI.Services
{
    /// <summary>
    /// Service for sending notifications to users
    /// </summary>
    public interface INotificationService
    {
        /// <summary>
        /// Create a new notification for a user
        /// </summary>
        /// <param name="receiverId">The ID of the user to notify</param>
        /// <param name="message">The notification message</param>
        /// <param name="senderId">Optional ID of the sender</param>
        /// <returns>True if the notification was created successfully</returns>
        Task<bool> CreateNotificationAsync(int receiverId, string message, int? senderId = null);

        /// <summary>
        /// Send a notification to a specific user
        /// </summary>
        /// <param name="userId">The ID of the user to notify</param>
        /// <param name="title">The notification title</param>
        /// <param name="message">The notification message</param>
        /// <param name="type">The type of notification</param>
        /// <param name="data">Additional data for the notification</param>
        Task SendNotificationAsync(int userId, string title, string message, NotificationType type = NotificationType.Info, string? data = null);

        /// <summary>
        /// Send a notification to multiple users
        /// </summary>
        /// <param name="userIds">The IDs of the users to notify</param>
        /// <param name="title">The notification title</param>
        /// <param name="message">The notification message</param>
        /// <param name="type">The type of notification</param>
        /// <param name="data">Additional data for the notification</param>
        Task SendNotificationAsync(IEnumerable<int> userIds, string title, string message, NotificationType type = NotificationType.Info, string? data = null);

        /// <summary>
        /// Send a notification to all users in a department
        /// </summary>
        /// <param name="department">The department to notify</param>
        /// <param name="title">The notification title</param>
        /// <param name="message">The notification message</param>
        /// <param name="type">The type of notification</param>
        /// <param name="data">Additional data for the notification</param>
        Task SendDepartmentNotificationAsync(string department, string title, string message, NotificationType type = NotificationType.Info, string? data = null);

        /// <summary>
        /// Schedule a notification to be sent at a specific time
        /// </summary>
        /// <param name="userId">The ID of the user to notify</param>
        /// <param name="title">The notification title</param>
        /// <param name="message">The notification message</param>
        /// <param name="deliveryTime">When to deliver the notification</param>
        /// <param name="type">The type of notification</param>
        /// <param name="data">Additional data for the notification</param>
        Task ScheduleNotificationAsync(int userId, string title, string message, DateTime deliveryTime, NotificationType type = NotificationType.Info, string? data = null);

        /// <summary>
        /// Cancel a scheduled notification
        /// </summary>
        /// <param name="notificationId">The ID of the scheduled notification</param>
        Task CancelScheduledNotificationAsync(string notificationId);

        /// <summary>
        /// Get all scheduled notifications for a user
        /// </summary>
        /// <param name="userId">The ID of the user</param>
        Task<IEnumerable<NotificationRecord>> GetScheduledNotificationsAsync(int userId);

        /// <summary>
        /// Sends a notification to a specific user.
        /// </summary>
        Task<bool> SendNotificationAsync(int receiverId, string message);

        // Messaging methods
        Task<IEnumerable<NotificationEntity>> GetUnreadNotificationsAsync(int? userId = null);
        Task<bool> MarkAsSeenAsync(int notificationId, int? userId = null);
        Task<bool> MarkNotificationsAsSeenAsync(IEnumerable<int> notificationIds, int? userId = null);
        Task<bool> BroadcastNotificationAsync(string message, int? senderId = null, string? department = null);
        Task<bool> SendChatMessageAsync(int receiverId, string message, bool queueIfOffline = true);
        Task<bool> MarkMessagesAsReadAsync(int senderId, int? currentUserId = null);
        Task<bool> MarkMessagesAsDeliveredAsync(int senderId, int? currentUserId = null);
        Task<bool> MarkMessagesAsDeliveredAsync(IEnumerable<int> messageIds, int? currentUserId = null);
        Task<int> GetUnreadMessagesCountAsync(int? userId = null);
        Task<IEnumerable<MessageDto>> GetUnreadMessagesAsync(int? userId = null);
        Task<IEnumerable<MessageDto>> GetMessageHistoryAsync(int otherUserId, int? currentUserId = null, int page = 1, int pageSize = 50);
        Task<bool> DeleteMessageAsync(int messageId, int? currentUserId = null);
        Task<bool> DeleteConversationAsync(int otherUserId, int? currentUserId = null);
        Task<bool> IsUserOnline(int userId);

        // WebSocket methods
        Task HandleUserConnectionAsync(WebSocketConnectionEntity connection, WebSocket socket);
        Task SendToUserAsync(int userId, object message);
        Task SendToGroupAsync(string group, object message);
        Task SendToAllAsync(object message, IEnumerable<string>? excludedConnections = null);
        Task<bool> HandleUserConnectionAsync(int userId, bool isConnected, string? machineName = null);

        // Alert methods
        Task ShowErrorAsync(string message);
        Task ShowSuccessAsync(string message);
        Task ShowWarningAsync(string message);
    }
}