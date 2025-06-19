using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading.Tasks;
using TDFShared.DTOs.Messages;
using TDFShared.Models.Message;
using TDFShared.Models.Notification;

namespace TDFShared.Services
{
    public interface INotificationService : IDisposable
    {
        /// <summary>
        /// Occurs when a notification is received.
        /// </summary>
        event EventHandler<NotificationDto> NotificationReceived;
        /// <summary>
        /// Gets all unread notifications for a user.
        /// </summary>
        Task<IEnumerable<NotificationEntity>> GetUnreadNotificationsAsync(int? userId = null);
        /// <summary>
        /// Marks a notification as seen for a user.
        /// </summary>
        Task<bool> MarkAsSeenAsync(int notificationId, int? userId = null);
        /// <summary>
        /// Marks multiple notifications as seen for a user.
        /// </summary>
        Task<bool> MarkNotificationsAsSeenAsync(IEnumerable<int> notificationIds, int? userId = null);
        /// <summary>
        /// Creates a notification for a user.
        /// </summary>
        Task<bool> CreateNotificationAsync(int receiverId, string message, int? senderId = null);
        /// <summary>
        /// Broadcasts a notification to all users or a department.
        /// </summary>
        Task<bool> BroadcastNotificationAsync(string message, int? senderId = null, string? department = null);
        /// <summary>
        /// Sends a notification to a specific user.
        /// </summary>
        Task<bool> SendNotificationAsync(int receiverId, string message);

        // Messaging methods
        /// <summary>
        /// Sends a chat message to a user, optionally queuing if offline.
        /// </summary>
        Task<bool> SendChatMessageAsync(int receiverId, string message, bool queueIfOffline = true);
        /// <summary>
        /// Marks messages as read for a sender and user.
        /// </summary>
        Task<bool> MarkMessagesAsReadAsync(int senderId, int? currentUserId = null);
        /// <summary>
        /// Marks messages as delivered for a sender and user.
        /// </summary>
        Task<bool> MarkMessagesAsDeliveredAsync(int senderId, int? currentUserId = null);
        /// <summary>
        /// Marks multiple messages as delivered for a user.
        /// </summary>
        Task<bool> MarkMessagesAsDeliveredAsync(IEnumerable<int> messageIds, int? currentUserId = null);
        /// <summary>
        /// Gets the count of unread messages for a user.
        /// </summary>
        Task<int> GetUnreadMessagesCountAsync(int? userId = null);
        /// <summary>
        /// Gets all unread messages for a user.
        /// </summary>
        Task<IEnumerable<MessageDto>> GetUnreadMessagesAsync(int? userId = null);
        /// <summary>
        /// Gets the message history between users.
        /// </summary>
        Task<IEnumerable<MessageDto>> GetMessageHistoryAsync(int otherUserId, int? currentUserId = null, int page = 1, int pageSize = 50);
        /// <summary>
        /// Deletes a message for a user.
        /// </summary>
        Task<bool> DeleteMessageAsync(int messageId, int? currentUserId = null);
        /// <summary>
        /// Deletes a conversation for a user.
        /// </summary>
        Task<bool> DeleteConversationAsync(int otherUserId, int? currentUserId = null);
        /// <summary>
        /// Checks if a user is online.
        /// </summary>
        Task<bool> IsUserOnline(int userId);

        // WebSocket methods
        /// <summary>
        /// Handles a user connection via WebSocket.
        /// </summary>
        Task HandleUserConnectionAsync(WebSocketConnectionEntity connection, WebSocket socket);
        /// <summary>
        /// Sends a message to a specific user via WebSocket.
        /// </summary>
        Task SendToUserAsync(int userId, object message);
        /// <summary>
        /// Sends a message to a group via WebSocket.
        /// </summary>
        Task SendToGroupAsync(string group, object message);
        /// <summary>
        /// Sends a message to all users via WebSocket, with optional exclusions.
        /// </summary>
        Task SendToAllAsync(object message, IEnumerable<string>? excludedConnections = null);
        /// <summary>
        /// Handles a user connection state change.
        /// </summary>
        Task<bool> HandleUserConnectionAsync(int userId, bool isConnected, string? machineName = null);

        // Add missing methods used by RequestApprovalViewModel
        /// <summary>
        /// Shows an error message to the user.
        /// </summary>
        Task ShowErrorAsync(string message);
        /// <summary>
        /// Shows a success message to the user.
        /// </summary>
        Task ShowSuccessAsync(string message);
        /// <summary>
        /// Shows a warning message to the user.
        /// </summary>
        Task ShowWarningAsync(string message);
    }
}