using System;
using TDFMAUI.Helpers;
using System.Collections.Generic;
using System.Threading.Tasks;
using TDFShared.DTOs.Messages;

namespace TDFMAUI.Services
{
    public interface IWebSocketService
    {
        bool IsConnected { get; }
        
        // Events
        event EventHandler<NotificationEventArgs> NotificationReceived;
        event EventHandler<ChatMessageEventArgs> ChatMessageReceived;
        event EventHandler<MessageStatusEventArgs> MessageStatusChanged;
        event EventHandler<ConnectionStatusEventArgs> ConnectionStatusChanged;
        event EventHandler<UserStatusEventArgs> UserStatusChanged;
        event EventHandler<UserAvailabilityEventArgs> UserAvailabilityChanged;
        event EventHandler<AvailabilitySetEventArgs> AvailabilityConfirmed;
        event EventHandler<StatusUpdateConfirmedEventArgs> StatusUpdateConfirmed;
        event EventHandler<WebSocketErrorEventArgs> ErrorReceived;
        
        // Connection methods
        Task<bool> ConnectAsync(string token = null);
        Task DisconnectAsync(bool sendCloseFrame = true);
        
        // Message sending methods
        Task SendMessageAsync(object message);
        Task SendChatMessageAsync(int receiverId, string message);
        Task JoinGroupAsync(string group);
        Task LeaveGroupAsync(string group);
        
        // Status update methods
        Task MarkMessagesAsReadAsync(int senderId);
        Task MarkMessagesAsDeliveredAsync(int senderId);
        Task MarkMessagesAsDeliveredAsync(IEnumerable<int> messageIds);
        Task MarkNotificationsAsSeenAsync(IEnumerable<int> notificationIds);
        Task UpdatePresenceStatusAsync(string status, string statusMessage = null);
        Task SetAvailableForChatAsync(bool isAvailable);
        Task SendActivityPingAsync();
    }
}