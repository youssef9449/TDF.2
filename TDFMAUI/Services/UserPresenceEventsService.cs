using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TDFMAUI.Helpers;
using TDFShared.DTOs.Users;
using TDFShared.Enums;

namespace TDFMAUI.Services
{
    public class UserPresenceEventsService : IUserPresenceEventsService
    {
        private readonly WebSocketService _webSocketService;
        private readonly ILogger<UserPresenceEventsService> _logger;

        public event EventHandler<UserStatusChangedEventArgs>? UserStatusChanged;
        public event EventHandler<UserAvailabilityChangedEventArgs>? UserAvailabilityChanged;
        public event EventHandler<AvailabilitySetEventArgs>? AvailabilityConfirmed;
        public event EventHandler<StatusUpdateConfirmedEventArgs>? StatusUpdateConfirmed;
        public event EventHandler<WebSocketErrorEventArgs>? PresenceErrorReceived;

        public bool IsConnected => _webSocketService.IsConnected;

        public UserPresenceEventsService(WebSocketService webSocketService, ILogger<UserPresenceEventsService> logger)
        {
            _webSocketService = webSocketService;
            _logger = logger;

            _webSocketService.UserStatusChanged += OnUserStatusChanged;
            _webSocketService.UserAvailabilityChanged += OnUserAvailabilityChanged;
            _webSocketService.AvailabilitySet += OnAvailabilitySet;
            _webSocketService.StatusUpdateConfirmed += OnStatusUpdateConfirmed;
            _webSocketService.ErrorReceived += OnErrorReceived;
        }

        public async Task UpdatePresenceStatusAsync(string status, string statusMessage)
        {
            if (_webSocketService.IsConnected)
            {
                await _webSocketService.UpdatePresenceStatusAsync(status, statusMessage);
            }
        }

        public async Task SetAvailableForChatAsync(bool isAvailable)
        {
            if (_webSocketService.IsConnected)
            {
                await _webSocketService.SetAvailableForChatAsync(isAvailable);
            }
        }

        public async Task SendActivityPingAsync()
        {
            if (_webSocketService.IsConnected)
            {
                await _webSocketService.SendActivityPingAsync();
            }
        }

        private void OnUserStatusChanged(object? sender, UserStatusEventArgs e)
        {
            UserStatusChanged?.Invoke(this, new UserStatusChangedEventArgs
            {
                UserId = e.UserId,
                Username = e.Username,
                Status = ParseStatus(e.PresenceStatus),
                StatusMessage = e.StatusMessage
            });
        }

        private void OnUserAvailabilityChanged(object? sender, UserAvailabilityEventArgs e)
        {
            UserAvailabilityChanged?.Invoke(this, new UserAvailabilityChangedEventArgs
            {
                UserId = e.UserId,
                Username = e.Username,
                IsAvailableForChat = e.IsAvailableForChat,
                Timestamp = e.Timestamp
            });
        }

        private void OnAvailabilitySet(object? sender, AvailabilitySetEventArgs e)
        {
            AvailabilityConfirmed?.Invoke(this, e);
        }

        private void OnStatusUpdateConfirmed(object? sender, StatusUpdateConfirmedEventArgs e)
        {
            StatusUpdateConfirmed?.Invoke(this, e);
        }

        private void OnErrorReceived(object? sender, WebSocketErrorEventArgs e)
        {
            PresenceErrorReceived?.Invoke(this, e);
        }

        private UserPresenceStatus ParseStatus(string status)
        {
            if (Enum.TryParse(status, true, out UserPresenceStatus result))
            {
                return result;
            }
            return UserPresenceStatus.Offline;
        }

        public void Dispose()
        {
            _webSocketService.UserStatusChanged -= OnUserStatusChanged;
            _webSocketService.UserAvailabilityChanged -= OnUserAvailabilityChanged;
            _webSocketService.AvailabilitySet -= OnAvailabilitySet;
            _webSocketService.StatusUpdateConfirmed -= OnStatusUpdateConfirmed;
            _webSocketService.ErrorReceived -= OnErrorReceived;
        }
    }
}
