using System;
using TDFMAUI.Helpers;
using TDFShared.DTOs.Users;

namespace TDFMAUI.Services
{
    public interface IUserPresenceEventsService : IDisposable
    {
        event EventHandler<UserStatusChangedEventArgs> UserStatusChanged;
        event EventHandler<UserAvailabilityChangedEventArgs> UserAvailabilityChanged;
        event EventHandler<AvailabilitySetEventArgs> AvailabilityConfirmed;
        event EventHandler<StatusUpdateConfirmedEventArgs> StatusUpdateConfirmed;
        event EventHandler<WebSocketErrorEventArgs> PresenceErrorReceived;

        Task UpdatePresenceStatusAsync(string status, string statusMessage);
        Task SetAvailableForChatAsync(bool isAvailable);
        Task SendActivityPingAsync();
        bool IsConnected { get; }
    }
}
