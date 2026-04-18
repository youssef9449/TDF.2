using System;
using System.Threading.Tasks;
using TDFMAUI.Helpers;
using TDFShared.DTOs.Messages;
using TDFMAUI.Services.Notifications;

namespace TDFMAUI.Services.WebSocket
{
    /// <summary>
    /// Parses raw JSON frames received over the WebSocket connection,
    /// dispatches them by their <c>type</c> discriminator, and raises a
    /// strongly-typed event for every recognised message. The router has no
    /// knowledge of the underlying transport — it is a pure dispatcher.
    /// </summary>
    public interface IWebSocketMessageRouter
    {
        event EventHandler<NotificationEventArgs> NotificationReceived;
        event EventHandler<ChatMessageEventArgs>? ChatMessageReceived;
        event EventHandler<MessageStatusEventArgs>? MessageStatusChanged;
        event EventHandler<UserStatusEventArgs>? UserStatusChanged;
        event EventHandler<UserAvailabilityEventArgs>? UserAvailabilityChanged;
        event EventHandler<AvailabilitySetEventArgs>? AvailabilitySet;
        event EventHandler<StatusUpdateConfirmedEventArgs>? StatusUpdateConfirmed;
        event EventHandler<WebSocketErrorEventArgs>? ErrorReceived;

        /// <summary>
        /// Routes a JSON frame to the matching handler. All exceptions are logged
        /// and swallowed so the receive loop keeps running on malformed frames.
        /// </summary>
        Task RouteAsync(string jsonMessage);
    }
}
