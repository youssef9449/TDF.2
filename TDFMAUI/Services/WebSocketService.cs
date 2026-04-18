using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TDFMAUI.Config;
using TDFMAUI.Helpers;
using TDFMAUI.Services.WebSocket;
using TDFShared.Constants;
using TDFShared.DTOs.Messages;
using TDFShared.Helpers;
using TDFMAUI.Services.Notifications;

namespace TDFMAUI.Services
{
    /// <summary>
    /// Thin orchestrator for the MAUI client's WebSocket connection. Owns the
    /// socket, receive loop, reconnect timer and outgoing send API. Token
    /// resolution is delegated to <see cref="IWebSocketTokenProvider"/> and
    /// incoming-frame dispatch is delegated to <see cref="IWebSocketMessageRouter"/>.
    /// </summary>
    public class WebSocketService : IWebSocketService, IDisposable
    {
        private readonly ILogger<WebSocketService> _logger;
        private readonly IWebSocketTokenProvider _tokenProvider;
        private readonly IWebSocketMessageRouter _router;

        private ClientWebSocket? _webSocket;
        private string _serverUrl => ApiConfig.WebSocketUrl;
        private CancellationTokenSource? _cancellationTokenSource;
        private readonly SemaphoreSlim _connectionLock = new SemaphoreSlim(1, 1);
        private bool _isConnecting;
        private readonly Timer _reconnectTimer;
        private int _reconnectAttempts;
        private const int MaxReconnectAttempts = 5;
        private bool _disposed;

        public event EventHandler<NotificationEventArgs> NotificationReceived = delegate { };
        public event EventHandler<ChatMessageEventArgs>? ChatMessageReceived;
        public event EventHandler<MessageStatusEventArgs>? MessageStatusChanged;
        public event EventHandler<ConnectionStatusEventArgs>? ConnectionStatusChanged;
        public event EventHandler<UserStatusEventArgs>? UserStatusChanged;
        public event EventHandler<UserAvailabilityEventArgs>? UserAvailabilityChanged;
        public event EventHandler<AvailabilitySetEventArgs>? AvailabilitySet;
        public event EventHandler<StatusUpdateConfirmedEventArgs>? StatusUpdateConfirmed;
        public event EventHandler<WebSocketErrorEventArgs>? ErrorReceived;

        public bool IsConnected => _webSocket?.State == WebSocketState.Open;

        public WebSocketService(
            ILogger<WebSocketService> logger,
            IWebSocketTokenProvider tokenProvider,
            IWebSocketMessageRouter router)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
            _router = router ?? throw new ArgumentNullException(nameof(router));
            _reconnectTimer = new Timer(ReconnectCallback, null, Timeout.Infinite, Timeout.Infinite);

            _router.NotificationReceived += (s, e) => NotificationReceived?.Invoke(this, e);
            _router.ChatMessageReceived += (s, e) => ChatMessageReceived?.Invoke(this, e);
            _router.MessageStatusChanged += (s, e) => MessageStatusChanged?.Invoke(this, e);
            _router.UserStatusChanged += (s, e) => UserStatusChanged?.Invoke(this, e);
            _router.UserAvailabilityChanged += (s, e) => UserAvailabilityChanged?.Invoke(this, e);
            _router.AvailabilitySet += (s, e) => AvailabilitySet?.Invoke(this, e);
            _router.StatusUpdateConfirmed += (s, e) => StatusUpdateConfirmed?.Invoke(this, e);
            _router.ErrorReceived += (s, e) => ErrorReceived?.Invoke(this, e);
        }

        public async Task<bool> ConnectAsync(string? token = null)
        {
            if (_isConnecting)
            {
                _logger.LogInformation("WebSocket connection already in progress");
                return false;
            }

            await _connectionLock.WaitAsync();
            _isConnecting = true;

            try
            {
                if (_webSocket?.State == WebSocketState.Open)
                {
                    _logger.LogDebug("WebSocket already connected");
                    return true;
                }

                await CleanupExistingConnectionAsync();

                _webSocket = new ClientWebSocket();
                _cancellationTokenSource = new CancellationTokenSource();

                var authToken = await _tokenProvider.GetValidTokenAsync(token);
                if (string.IsNullOrEmpty(authToken))
                {
                    _logger.LogWarning("No authentication token available for WebSocket connection");
                    return false;
                }

                _logger.LogInformation("Using token for WebSocket connection. Token length: {Length}", authToken.Length);
                _logger.LogInformation("WebSocket token prefix: {Prefix}..., suffix: ...{Suffix}",
                    authToken.Substring(0, Math.Min(5, authToken.Length)),
                    authToken.Substring(Math.Max(0, authToken.Length - 5)));

                _webSocket.Options.SetRequestHeader("Authorization", $"Bearer {authToken}");

                if (ApiConfig.DevelopmentMode)
                {
                    _logger.LogWarning("WebSocket configured with development certificate validation (ALLOW ALL)");
                    _webSocket.Options.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
                }

                _logger.LogInformation("Connecting to WebSocket server at {Url}", _serverUrl);

                try
                {
                    await _webSocket.ConnectAsync(new Uri(_serverUrl), _cancellationTokenSource.Token);
                    _reconnectAttempts = 0;

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        try
                        {
                            ConnectionStatusChanged?.Invoke(this, new ConnectionStatusEventArgs
                            {
                                IsConnected = true,
                                Timestamp = DateTime.UtcNow
                            });
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error invoking ConnectionStatusChanged event");
                        }
                    });

                    StartReceiving();

                    await SendMessageAsync(new
                    {
                        type = ApiRoutes.WebSocket.MessageTypes.Ping,
                        timestamp = DateTime.UtcNow
                    });

                    return true;
                }
                catch (WebSocketException wsEx) when (wsEx.Message.Contains("401"))
                {
                    _logger.LogError(wsEx, "Authentication failed (401 Unauthorized) when connecting to WebSocket server");
                    return await HandleAuthenticationFailureAsync();
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("WebSocket connection timed out");
                    await CleanupExistingConnectionAsync();
                    return false;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error connecting to WebSocket server");
                    await CleanupExistingConnectionAsync();
                    return false;
                }
            }
            finally
            {
                _isConnecting = false;
                _connectionLock.Release();
            }
        }

        private async Task<bool> HandleAuthenticationFailureAsync()
        {
            if (DeviceHelper.IsDesktop)
            {
                _logger.LogWarning("Desktop platform authentication failed - user may need to log in again");

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        ErrorReceived?.Invoke(this, new WebSocketErrorEventArgs
                        {
                            ErrorCode = "401",
                            ErrorMessage = "Authentication failed. Please log in again.",
                            Timestamp = DateTime.UtcNow
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error invoking ErrorReceived event");
                    }
                });

                return false;
            }

            // Mobile: ask the token provider to refresh and retry once.
            var refreshed = await _tokenProvider.GetValidTokenAsync();
            if (!string.IsNullOrEmpty(refreshed))
            {
                _logger.LogInformation("Token refreshed successfully, attempting to reconnect");
                return await ConnectAsync();
            }

            _logger.LogWarning("Token refresh failed, cannot establish WebSocket connection");
            return false;
        }

        private async Task CleanupExistingConnectionAsync()
        {
            if (_webSocket == null)
            {
                return;
            }

            try
            {
                _cancellationTokenSource?.Cancel();

                if (_webSocket.State == WebSocketState.Open)
                {
                    using var closeTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closing connection",
                        closeTokenSource.Token);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during WebSocket cleanup, will proceed with disposal");
            }
            finally
            {
                _webSocket.Dispose();
                _webSocket = null;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private void StartReceiving()
        {
            Task.Run(ReceiveMessagesAsync);
        }

        private async Task ReceiveMessagesAsync()
        {
            var buffer = new byte[4096];
            var receiveBuffer = new ArraySegment<byte>(buffer);
            var messageBuilder = new StringBuilder();

            try
            {
                while (_webSocket != null && _webSocket.State == WebSocketState.Open)
                {
                    WebSocketReceiveResult? receiveResult = null;

                    try
                    {
                        if (_cancellationTokenSource == null || _cancellationTokenSource.IsCancellationRequested)
                        {
                            _logger.LogInformation("WebSocket receive loop stopping due to cancellation");
                            break;
                        }

                        receiveResult = await _webSocket.ReceiveAsync(receiveBuffer, _cancellationTokenSource.Token);
                    }
                    catch (ObjectDisposedException ode)
                    {
                        _logger.LogWarning("WebSocket stream was closed during receive: {Message}", ode.Message);
                        break;
                    }
                    catch (IOException ioEx)
                    {
                        _logger.LogWarning("IO Exception in WebSocket receive: {Message}", ioEx.Message);
                        break;
                    }
                    catch (WebSocketException wsEx)
                    {
                        _logger.LogWarning("WebSocket exception in receive: {Message}", wsEx.Message);
                        break;
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogInformation("WebSocket receive canceled");
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error receiving WebSocket message");
                        break;
                    }

                    if (receiveResult == null)
                    {
                        _logger.LogWarning("WebSocket receive result is null");
                        break;
                    }

                    if (receiveResult.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.LogInformation("WebSocket close frame received");
                        await HandleDisconnect(true);
                        break;
                    }

                    messageBuilder.Append(Encoding.UTF8.GetString(buffer, 0, receiveResult.Count));

                    if (receiveResult.EndOfMessage)
                    {
                        var jsonMessage = messageBuilder.ToString();
                        messageBuilder.Clear();

                        _logger.LogDebug("WebSocket message received: {MessageLength} bytes", jsonMessage.Length);

                        try
                        {
                            await _router.RouteAsync(jsonMessage);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing WebSocket message");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in WebSocket receive loop");
            }
            finally
            {
                await HandleDisconnect(false);
            }
        }

        private async Task HandleDisconnect(bool wasClean)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                ConnectionStatusChanged?.Invoke(this, new ConnectionStatusEventArgs
                {
                    IsConnected = false,
                    WasClean = wasClean,
                    Timestamp = DateTime.UtcNow
                });
            });

            if (!wasClean)
            {
                StartReconnectTimer();
            }
            else
            {
                await DisconnectAsync(sendCloseFrame: false);
            }
        }

        private void StartReconnectTimer()
        {
            if (_reconnectAttempts >= MaxReconnectAttempts)
            {
                _logger.LogWarning("Max reconnect attempts reached ({MaxAttempts})", MaxReconnectAttempts);
                return;
            }

            _reconnectAttempts++;

            var delayMs = (int)Math.Min(1000 * Math.Pow(2, _reconnectAttempts - 1), 30000);

            _logger.LogInformation("Scheduling reconnect attempt {Attempt} in {DelayMs}ms",
                _reconnectAttempts, delayMs);

            _reconnectTimer.Change(delayMs, Timeout.Infinite);
        }

        private void ReconnectCallback(object? state)
        {
            _logger.LogInformation("Attempting to reconnect (attempt {Attempt}/{MaxAttempts})",
                _reconnectAttempts, MaxReconnectAttempts);

            try
            {
                var success = ConnectAsync().ConfigureAwait(false).GetAwaiter().GetResult();

                if (success)
                {
                    _logger.LogInformation("Reconnection successful");
                    _reconnectAttempts = 0;
                }
                else if (_reconnectAttempts < MaxReconnectAttempts)
                {
                    StartReconnectTimer();
                }
                else
                {
                    _logger.LogWarning("Failed to reconnect after {MaxAttempts} attempts", MaxReconnectAttempts);
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        ConnectionStatusChanged?.Invoke(this, new ConnectionStatusEventArgs
                        {
                            IsConnected = false,
                            WasClean = false,
                            Timestamp = DateTime.UtcNow,
                            ReconnectionFailed = true
                        });
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during reconnection attempt");
                if (_reconnectAttempts < MaxReconnectAttempts)
                {
                    StartReconnectTimer();
                }
            }
        }

        public async Task SendMessageAsync(object message)
        {
            if (_webSocket?.State != WebSocketState.Open)
            {
                _logger.LogWarning("Cannot send message: WebSocket is not connected");
                return;
            }

            try
            {
                var json = JsonSerializationHelper.SerializeCompact(message);
                var bytes = Encoding.UTF8.GetBytes(json);
                await _webSocket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    _cancellationTokenSource?.Token ?? CancellationToken.None);

                _logger.LogDebug("Sent WebSocket message: {Message}", json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending WebSocket message");
                await HandleDisconnect(false);
            }
        }

        public async Task DisconnectAsync(bool sendCloseFrame = true)
        {
            await _connectionLock.WaitAsync();
            try
            {
                if (_webSocket?.State == WebSocketState.Open)
                {
                    _logger.LogInformation("Disconnecting from WebSocket server...");

                    if (sendCloseFrame)
                    {
                        try
                        {
                            using var closeTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure,
                                "Client initiated disconnect",
                                closeTokenSource.Token);
                            _logger.LogInformation("WebSocket closed gracefully");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to gracefully close WebSocket connection");
                        }
                    }
                }

                await CleanupExistingConnectionAsync();

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    ConnectionStatusChanged?.Invoke(this, new ConnectionStatusEventArgs
                    {
                        IsConnected = false,
                        Timestamp = DateTime.UtcNow
                    });
                });
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        public Task SendChatMessageAsync(int receiverId, string message) =>
            SendMessageAsync(new
            {
                type = ApiRoutes.WebSocket.MessageTypes.ChatMessage,
                receiverId,
                message,
                timestamp = DateTime.UtcNow
            });

        public Task JoinGroupAsync(string group) =>
            SendMessageAsync(new { type = "join_group", group });

        public Task LeaveGroupAsync(string group) =>
            SendMessageAsync(new { type = "leave_group", group });

        public Task MarkMessagesAsReadAsync(int senderId) =>
            SendMessageAsync(new { type = "mark_as_read", senderId });

        public Task MarkMessagesAsDeliveredAsync(int senderId) =>
            SendMessageAsync(new { type = "mark_as_delivered", senderId });

        public Task MarkMessagesAsDeliveredAsync(IEnumerable<int> messageIds)
        {
            if (messageIds == null || !messageIds.Any())
            {
                _logger.LogWarning("Attempted to mark messages as delivered but no message IDs were provided");
                return Task.CompletedTask;
            }

            return SendMessageAsync(new
            {
                type = "mark_messages_as_delivered",
                messageIds = messageIds.ToList()
            });
        }

        public Task MarkNotificationsAsSeenAsync(IEnumerable<int> notificationIds) =>
            SendMessageAsync(new
            {
                type = "mark_notifications_seen",
                notificationIds = notificationIds.ToList()
            });

        public Task UpdatePresenceStatusAsync(string status, string? statusMessage = null) =>
            SendMessageAsync(new
            {
                type = ApiRoutes.WebSocket.MessageTypes.UserPresence,
                status = status ?? string.Empty,
                statusMessage = statusMessage ?? string.Empty,
                timestamp = DateTime.UtcNow
            });

        public Task SetAvailableForChatAsync(bool isAvailable) =>
            SendMessageAsync(new
            {
                type = "set_availability",
                isAvailable,
                timestamp = DateTime.UtcNow
            });

        public Task SendActivityPingAsync() =>
            SendMessageAsync(new
            {
                type = ApiRoutes.WebSocket.MessageTypes.Ping,
                timestamp = DateTime.UtcNow
            });

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                try
                {
                    _connectionLock?.Dispose();
                    _reconnectTimer?.Dispose();

                    var disconnectTask = DisconnectAsync(false);
                    if (!disconnectTask.Wait(TimeSpan.FromSeconds(2)))
                    {
                        _logger.LogWarning("WebSocketService disconnect timed out during disposal");
                    }

                    if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
                    {
                        _cancellationTokenSource.Cancel();
                        _cancellationTokenSource.Dispose();
                        _cancellationTokenSource = null;
                    }

                    if (_webSocket != null)
                    {
                        _webSocket.Dispose();
                        _webSocket = null;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during WebSocketService disposal");
                }
            }

            _disposed = true;
        }

        ~WebSocketService()
        {
            Dispose(false);
        }
    }
}
