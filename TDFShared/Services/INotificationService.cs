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
        // Notification methods
        event EventHandler<NotificationDto> NotificationReceived;
        Task<IEnumerable<NotificationEntity>> GetUnreadNotificationsAsync(int? userId = null);
        Task<bool> MarkAsSeenAsync(int notificationId, int? userId = null);
        Task<bool> MarkNotificationsAsSeenAsync(IEnumerable<int> notificationIds, int? userId = null);
        Task<bool> CreateNotificationAsync(int receiverId, string message, int? senderId = null);
        Task<bool> BroadcastNotificationAsync(string message, int? senderId = null, string? department = null);
        Task<bool> SendNotificationAsync(int receiverId, string message);

        // Messaging methods
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

        // Add missing methods used by RequestApprovalViewModel
        Task ShowErrorAsync(string message);
        Task ShowSuccessAsync(string message);
        Task ShowWarningAsync(string message);
    }
}