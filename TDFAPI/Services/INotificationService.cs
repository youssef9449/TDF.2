using System;
using System.Collections.Generic;
using System.Net.WebSockets;using TDFShared.Models.Message;
using TDFShared.DTOs.Messages;



namespace TDFAPI.Services
{
    public interface INotificationService : IDisposable
    {
        Task<IEnumerable<NotificationDto>> GetUnreadNotificationsAsync(int userId);
        Task<bool> MarkAsSeenAsync(int notificationId, int userId);
        Task<bool> MarkNotificationsAsSeenAsync(int userId, IEnumerable<int> notificationIds);
        Task<bool> CreateNotificationAsync(int receiverId, string message);
        Task<bool> BroadcastNotificationAsync(string message, int senderId, string? department = null);
        Task<bool> HandleUserConnectionAsync(int userId, bool isConnected, string? machineName = null);
        Task HandleUserConnectionAsync(WebSocketConnectionEntity connection, WebSocket socket);
        Task SendToUserAsync(int userId, object message);
        Task SendToGroupAsync(string group, object message);
        Task SendToAllAsync(object message, IEnumerable<string>? excludedConnections = null);
        
        // Chat-related methods
        Task<bool> SendChatMessageAsync(int receiverId, string message, int senderId, bool queueIfOffline = true);
        Task<Dictionary<int, PendingMessageInfo>> GetPendingMessageCountsAsync(int receiverId);
        Task<bool> MarkMessagesAsReadAsync(int senderId, int receiverId);
        Task<bool> MarkMessagesAsDeliveredAsync(int senderId, int receiverId);
    }
}