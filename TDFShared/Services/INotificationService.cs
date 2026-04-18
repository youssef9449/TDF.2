using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading.Tasks;
using TDFShared.DTOs.Messages;
using TDFShared.Models.Message;
using TDFShared.Models.Notification;

namespace TDFShared.Services
{
    /// <summary>
    /// Cross-cutting notification contract that both the API and any non-UI consumers
    /// can depend on. Kept intentionally narrow: everything listed here has a real
    /// implementation on the server.
    ///
    /// UI-facing methods (<c>ShowErrorAsync</c>, <c>ShowSuccessAsync</c>, <c>ShowWarningAsync</c>)
    /// now live on <see cref="TDFShared.Contracts.IUserFeedbackService"/> because they are
    /// only meaningful on the client.
    ///
    /// Chat-message CRUD methods (<c>SendChatMessageAsync</c>, <c>MarkMessagesAs*Async</c>,
    /// <c>GetUnreadMessages*</c>, <c>GetMessageHistoryAsync</c>, <c>DeleteMessageAsync</c>,
    /// <c>DeleteConversationAsync</c>) have been moved off this interface as well; use the
    /// server's <c>IMessageService</c> directly instead of going through a notification
    /// abstraction.
    /// </summary>
    public interface INotificationService : IDisposable
    {
        /// <summary>
        /// Occurs when a notification is received.
        /// </summary>
        event EventHandler<NotificationDto> NotificationReceived;

        // Notification CRUD ----------------------------------------------------

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

        // WebSocket transport --------------------------------------------------

        /// <summary>
        /// Registers a user's WebSocket connection and pumps incoming frames for the
        /// lifetime of the socket. Completes when the peer disconnects.
        /// </summary>
        Task HandleUserConnectionAsync(WebSocketConnectionEntity connection, WebSocket socket);

        /// <summary>
        /// Sends a message to every active connection belonging to the given user.
        /// </summary>
        Task SendToUserAsync(int userId, object message);

        /// <summary>
        /// Sends a message to every connection that has joined the given group.
        /// </summary>
        Task SendToGroupAsync(string group, object message);

        /// <summary>
        /// Sends a message to every active connection, optionally excluding some.
        /// </summary>
        Task SendToAllAsync(object message, IEnumerable<string>? excludedConnections = null);

        /// <summary>
        /// Returns <c>true</c> when the given user has at least one active WebSocket connection.
        /// </summary>
        Task<bool> IsUserOnline(int userId);
    }
}
