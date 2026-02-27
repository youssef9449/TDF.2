using Microsoft.Extensions.Logging;
using TDFMAUI.Helpers;
using TDFShared.Enums;
using TDFShared.DTOs.Users;
using System;

namespace TDFMAUI.Services
{
    /// <summary>
    /// Dedicated class to handle WebSocket events related to user presence
    /// and update the UserPresenceService state.
    /// </summary>
    public class PresenceWebSocketHandler : IDisposable
    {
        private readonly IUserPresenceService _presenceService;
        private readonly IWebSocketService _webSocketService;
        private readonly ILogger<PresenceWebSocketHandler> _logger;
        private readonly Timer _activityTimer;
        private bool _disposed;

        public PresenceWebSocketHandler(
            IUserPresenceService presenceService,
            IWebSocketService webSocketService,
            ILogger<PresenceWebSocketHandler> logger)
        {
            _presenceService = presenceService ?? throw new ArgumentNullException(nameof(presenceService));
            _webSocketService = webSocketService ?? throw new ArgumentNullException(nameof(webSocketService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Subscribe to WebSocket events
            _webSocketService.UserStatusChanged += OnUserStatusChanged;
            _webSocketService.UserAvailabilityChanged += OnUserAvailabilityChanged;
            _webSocketService.AvailabilityConfirmed += OnAvailabilityConfirmed;
            _webSocketService.StatusUpdateConfirmed += OnStatusUpdateConfirmed;
            _webSocketService.ErrorReceived += OnErrorReceived;

            // Start activity tracking timer (send activity ping every 60 seconds)
            _activityTimer = new Timer(SendActivityPing, null, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));

            _logger.LogInformation("PresenceWebSocketHandler initialized and subscribed to events.");
        }

        private async void SendActivityPing(object? state)
        {
            if (App.CurrentUser != null && _webSocketService.IsConnected)
            {
                try
                {
                    await _webSocketService.SendActivityPingAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending activity ping");
                }
            }
        }

        private void OnUserStatusChanged(object? sender, UserStatusEventArgs e)
        {
            if (_presenceService is UserPresenceService ups)
                ups.HandleRemoteStatusChanged(e);
        }

        private void OnUserAvailabilityChanged(object? sender, UserAvailabilityEventArgs e)
        {
            if (_presenceService is UserPresenceService ups)
                ups.HandleRemoteAvailabilityChanged(e);
        }

        private void OnAvailabilityConfirmed(object? sender, AvailabilitySetEventArgs e)
        {
            if (_presenceService is UserPresenceService ups)
                ups.HandleAvailabilityConfirmed(e);
        }

        private void OnStatusUpdateConfirmed(object? sender, StatusUpdateConfirmedEventArgs e)
        {
            if (_presenceService is UserPresenceService ups)
                ups.HandleStatusUpdateConfirmed(e);
        }

        private void OnErrorReceived(object? sender, WebSocketErrorEventArgs e)
        {
            if (_presenceService is UserPresenceService ups)
                ups.HandlePresenceError(e);
        }

        public void Dispose()
        {
            if (_disposed) return;

            _webSocketService.UserStatusChanged -= OnUserStatusChanged;
            _webSocketService.UserAvailabilityChanged -= OnUserAvailabilityChanged;
            _webSocketService.AvailabilityConfirmed -= OnAvailabilityConfirmed;
            _webSocketService.StatusUpdateConfirmed -= OnStatusUpdateConfirmed;
            _webSocketService.ErrorReceived -= OnErrorReceived;

            _activityTimer?.Dispose();
            _disposed = true;

            _logger.LogInformation("PresenceWebSocketHandler disposed.");
        }
    }
}
