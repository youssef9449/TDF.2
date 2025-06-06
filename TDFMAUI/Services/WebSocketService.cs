using Microsoft.Extensions.Logging;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using TDFMAUI.Config;
using TDFMAUI.Helpers;
using TDFShared.Constants;
using TDFShared.DTOs.Messages;
using TDFShared.Enums;
using TDFShared.Helpers;
using TDFShared.Exceptions;
using TDFShared.Services;
using TDFMAUI.Services;

namespace TDFMAUI.Services
{
    public class WebSocketService : IWebSocketService, IDisposable
    {
        private ClientWebSocket? _webSocket;
        private readonly ILogger<WebSocketService> _logger;
        private readonly SecureStorageService _secureStorage;
        private readonly TDFShared.Services.IAuthService _authService;
        private string _serverUrl => ApiConfig.WebSocketUrl;
        private CancellationTokenSource? _cancellationTokenSource;
        private readonly SemaphoreSlim _connectionLock = new SemaphoreSlim(1, 1);
        private bool _isConnecting = false;
        private Timer _reconnectTimer;
        private int _reconnectAttempts = 0;
        private const int MaxReconnectAttempts = 5;
        private bool _disposed = false;

        // Events for different types of messages
        public event EventHandler<TDFShared.DTOs.Messages.NotificationEventArgs> NotificationReceived = delegate { };
        public event EventHandler<ChatMessageEventArgs>? ChatMessageReceived;
        public event EventHandler<MessageStatusEventArgs>? MessageStatusChanged;
        public event EventHandler<ConnectionStatusEventArgs>? ConnectionStatusChanged;
        public event EventHandler<UserStatusEventArgs>? UserStatusChanged;
        public event EventHandler<UserAvailabilityEventArgs>? UserAvailabilityChanged;
        // New Events Added:
        public event EventHandler<AvailabilitySetEventArgs>? AvailabilitySet;
        public event EventHandler<StatusUpdateConfirmedEventArgs>? StatusUpdateConfirmed;
        public event EventHandler<WebSocketErrorEventArgs>? ErrorReceived;

        public bool IsConnected => _webSocket?.State == WebSocketState.Open;

        public WebSocketService(
            ILogger<WebSocketService> logger,
            SecureStorageService secureStorage,
            TDFShared.Services.IAuthService authService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _secureStorage = secureStorage ?? throw new ArgumentNullException(nameof(secureStorage));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _reconnectTimer = new Timer(ReconnectCallback, null, Timeout.Infinite, Timeout.Infinite);
        }

        private async Task<string?> GetValidTokenAsync(string? providedToken = null)
        {
            try
            {
                // First try the provided token if any
                if (!string.IsNullOrEmpty(providedToken))
                {
                    _logger.LogDebug("Using provided token for WebSocket connection");
                    return providedToken;
                }

                // For desktop platforms, try to get the in-memory token
                if (DeviceHelper.IsDesktop)
                {
                    var desktopToken = ApiConfig.CurrentToken;
                    if (!string.IsNullOrEmpty(desktopToken) && ApiConfig.TokenExpiration > DateTime.UtcNow)
                    {
                        _logger.LogDebug("Using in-memory token for desktop platform");
                        return desktopToken;
                    }
                    else
                    {
                        _logger.LogWarning("Desktop platform has no valid in-memory token");
                        // On desktop, we don't try to refresh from secure storage
                        // since tokens aren't persisted there
                        return null;
                    }
                }

                // For mobile platforms, try to get the stored token
                var (storedToken, expiration) = await _secureStorage.GetTokenAsync();
                if (!string.IsNullOrEmpty(storedToken) && expiration > DateTime.UtcNow)
                {
                    _logger.LogDebug("Using stored token from secure storage");
                    return storedToken;
                }

                // If no token is available, try to refresh
                var (currentToken, _) = await _secureStorage.GetTokenAsync();
                var (currentRefreshToken, _) = await _secureStorage.GetRefreshTokenAsync();
                if (!string.IsNullOrEmpty(currentToken) && !string.IsNullOrEmpty(currentRefreshToken))
                {
                    var refreshResult = await _authService.RefreshTokenAsync(currentToken, currentRefreshToken);
                    if (refreshResult != null)
                    {
                        // After refresh, try to get the token again
                        if (DeviceHelper.IsDesktop)
                        {
                            return ApiConfig.CurrentToken;
                        }
                        else
                        {
                            var (newToken, _) = await _secureStorage.GetTokenAsync();
                            return newToken;
                        }
                    }
                }

                _logger.LogWarning("No valid token available for WebSocket connection");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting valid token for WebSocket connection");
                return null;
            }
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

                // Get a valid token using the new method
                var authToken = await GetValidTokenAsync(token);
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

                    MainThread.BeginInvokeOnMainThread(() => {
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
                    
                    // Handle differently based on platform
                    if (DeviceHelper.IsDesktop)
                    {
                        _logger.LogWarning("Desktop platform authentication failed - user may need to log in again");
                        
                        // For desktop, we should notify the app that authentication has failed
                        // This will allow the app to redirect to the login page if needed
                        MainThread.BeginInvokeOnMainThread(() => {
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
                    else
                    {
                        // For mobile platforms, try to refresh the token
                        var (currentToken, _) = await _secureStorage.GetTokenAsync();
                        var (currentRefreshToken, _) = await _secureStorage.GetRefreshTokenAsync();
                        if (!string.IsNullOrEmpty(currentToken) && !string.IsNullOrEmpty(currentRefreshToken))
                        {
                            var refreshResult = await _authService.RefreshTokenAsync(currentToken, currentRefreshToken);
                            if (refreshResult != null)
                            {
                                _logger.LogInformation("Token refreshed successfully, attempting to reconnect");
                                return await ConnectAsync(); // Retry connection with new token
                            }
                        }
                        
                        _logger.LogWarning("Token refresh failed, cannot establish WebSocket connection");
                        return false;
                    }
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

        private async Task CleanupExistingConnectionAsync()
        {
            if (_webSocket != null)
            {
                try
                {
                    // Cancel any ongoing operations
                    _cancellationTokenSource?.Cancel();

                    // Attempt a graceful close if needed
                    if (_webSocket.State == WebSocketState.Open)
                    {
                        // Use a new cancellation token with a short timeout for closing
                        using var closeTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closing connection",
                            closeTokenSource.Token);
                    }
                }
                catch (Exception ex)
                {
                    // Just log but continue with cleanup
                    _logger.LogWarning(ex, "Error during WebSocket cleanup, will proceed with disposal");
                }
                finally
                {
                    // Always dispose
                    _webSocket.Dispose();
                    _webSocket = null;
                    _cancellationTokenSource?.Dispose();
                    _cancellationTokenSource = null;
                }
            }
        }

        private void StartReceiving()
        {
            // Start the receive loop in a background task
            Task.Run(ReceiveMessagesAsync);
        }

        private async Task ReceiveMessagesAsync()
        {
            var buffer = new byte[4096];
            var receiveBuffer = new ArraySegment<byte>(buffer);
            var messageBuilder = new StringBuilder();
            var message = new StringBuilder();

            try
            {
                while (_webSocket != null && _webSocket.State == WebSocketState.Open)
                {
                    // For WebSocketReceiveResult, allow nullable
                    dynamic? receiveResult = null;

                    try
                    {
                        // Check if the cancellation token source is still valid
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
                            await ProcessMessageAsync(jsonMessage);
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

        private async Task ProcessMessageAsync(string jsonMessage)
        {
            await Task.Yield();

            try
            {
                using var jsonDocument = JsonDocument.Parse(jsonMessage);
                var root = jsonDocument.RootElement;

                if (!root.TryGetProperty("type", out var typeElement) || typeElement.GetString() is not string messageType)
                {
                    _logger.LogWarning("Received WebSocket message without a valid 'type' field: {JsonMessage}", jsonMessage);
                    return;
                }

                _logger.LogDebug("Processing WebSocket message type: {MessageType}", messageType);

                switch (messageType.ToLower())
                {
                    case ApiRoutes.WebSocket.MessageTypes.Notification:
                        HandleNotification(root);
                        break;
                    case ApiRoutes.WebSocket.MessageTypes.ChatMessage:
                        HandleChatMessage(root);
                        break;
                    case "pending_message": // Handle messages delivered while offline
                        HandlePendingMessage(root);
                        break;
                    case "messages_read":
                    case ApiRoutes.WebSocket.MessageTypes.MessageStatus:
                        HandleMessagesRead(root);
                        break;
                    case "messages_delivered":
                        HandleMessagesDelivered(root);
                        break;
                    case ApiRoutes.WebSocket.MessageTypes.UserPresence:
                         HandleUserStatus(root); // Assuming this exists
                         break;
                    case "user_status_changed":
                        HandleUserStatusChange(root);
                        break;
                    case "user_availability_changed":
                        HandleUserAvailabilityChange(root);
                        break;
                    case "broadcast_notification":
                        HandleBroadcastNotification(root);
                        break;
                    case "notification_seen":
                        HandleNotificationSeen(root);
                        break;
                    case "notifications_seen":
                        HandleNotificationsSeen(root);
                        break;
                    // New Handlers Added:
                    case "availability_set":
                        HandleAvailabilitySet(root);
                        break;
                    case "status_updated": // Handle confirmation if API sends it
                        HandleStatusUpdated(root);
                        break;
                    case ApiRoutes.WebSocket.MessageTypes.Error:
                        HandleError(root);
                        break;
                    // Keep pong or other control messages if necessary
                    case ApiRoutes.WebSocket.MessageTypes.Pong:
                         _logger.LogTrace("Pong received from server");
                         // Reset any watchdog timers if needed
                         break;
                    default:
                        _logger.LogWarning("Received unhandled WebSocket message type: {MessageType}", messageType);
                        break;
                }
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "Error parsing WebSocket JSON message: {JsonMessage}", jsonMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing WebSocket message: {JsonMessage}", jsonMessage);
            }
        }

        private void HandleNotification(JsonElement element)
        {
            try
            {
                // Extract notification data from JSON
                if (element.TryGetProperty("notification", out var notificationElement))
                {
                    int notificationId = 0;
                    int? senderId = null;
                    string senderName = "System";
                    string message = string.Empty;
                    DateTime timestamp = DateTime.UtcNow;

                    // Extract notification ID
                    if (notificationElement.TryGetProperty("notificationId", out var idElement))
                    {
                        notificationId = idElement.GetInt32();
                    }

                    // Extract sender info
                    if (notificationElement.TryGetProperty("senderId", out var senderIdElement) &&
                        senderIdElement.ValueKind != JsonValueKind.Null)
                    {
                        senderId = senderIdElement.GetInt32();
                    }

                    if (notificationElement.TryGetProperty("senderName", out var senderNameElement) &&
                        senderNameElement.ValueKind != JsonValueKind.Null)
                    {
                        senderName = senderNameElement.GetString() ?? "System";
                    }

                    // Extract message
                    if (notificationElement.TryGetProperty("message", out var messageElement))
                    {
                        message = messageElement.GetString() ?? string.Empty;
                    }

                    // Extract timestamp
                    if (notificationElement.TryGetProperty("timestamp", out var timeElement))
                    {
                        if (DateTime.TryParse(timeElement.GetString(), out var parsedTime))
                        {
                            timestamp = parsedTime;
                        }
                    }

                    // Create notification event args
                    var eventArgs = new TDFShared.DTOs.Messages.NotificationEventArgs
                    {
                        NotificationId = notificationId,
                        SenderId = senderId,
                        SenderName = senderName,
                        Title = "New Notification",
                        Message = message,
                        Type = NotificationType.Info,
                        Timestamp = timestamp
                    };

                    // Raise event on UI thread
                    MainThread.BeginInvokeOnMainThread(() => {
                        NotificationReceived?.Invoke(this, eventArgs);
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling notification message");
            }
        }

        private void HandleChatMessage(JsonElement element)
        {
            try
            {
                string message = null;
                int senderId = 0;
                string senderName = null;
                DateTime timestamp = DateTime.UtcNow;

                if (element.TryGetProperty("message", out var messageElement))
                {
                    // When assigning from .GetString(), use ?? string.Empty
                    message = messageElement.GetString() ?? string.Empty;
                }

                if (element.TryGetProperty("senderId", out var senderIdElement))
                {
                    senderId = senderIdElement.GetInt32();
                }

                if (element.TryGetProperty("senderName", out var senderNameElement))
                {
                    // When assigning from .GetString(), use ?? string.Empty
                    senderName = senderNameElement.GetString() ?? string.Empty;
                }

                if (element.TryGetProperty("timestamp", out var timestampElement))
                {
                    timestamp = timestampElement.GetDateTime();
                }

                // Raise events on UI thread
                MainThread.BeginInvokeOnMainThread(() => {
                    ChatMessageReceived?.Invoke(this, new ChatMessageEventArgs
                    {
                        SenderId = senderId,
                        SenderName = senderName,
                        Message = message,
                        Timestamp = timestamp
                    });

                    // Also raise as generic notification for systems that don't handle chat specifically
                    NotificationReceived?.Invoke(this, new TDFShared.DTOs.Messages.NotificationEventArgs
                    {
                        SenderId = senderId,
                        SenderName = senderName,
                        Title = "New Chat Message",
                        Message = message,
                        Timestamp = timestamp,
                        Type = NotificationType.Info
                    });
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling chat message");
            }
        }

        private void HandlePendingMessage(JsonElement element)
        {
            try
            {
                // Similar to HandleChatMessage but for pending messages
                int messageId = 0;
                string message = null;
                int senderId = 0;
                string senderName = null;
                DateTime timestamp = DateTime.UtcNow;

                if (element.TryGetProperty("messageId", out var messageIdElement))
                {
                    messageId = messageIdElement.GetInt32();
                }

                if (element.TryGetProperty("message", out var messageElement))
                {
                    // When assigning from .GetString(), use ?? string.Empty
                    message = messageElement.GetString() ?? string.Empty;
                }

                if (element.TryGetProperty("senderId", out var senderIdElement))
                {
                    senderId = senderIdElement.GetInt32();
                }

                if (element.TryGetProperty("senderName", out var senderNameElement))
                {
                    // When assigning from .GetString(), use ?? string.Empty
                    senderName = senderNameElement.GetString() ?? string.Empty;
                }

                if (element.TryGetProperty("timestamp", out var timestampElement))
                {
                    timestamp = timestampElement.GetDateTime();
                }

                // Raise event on the UI thread
                MainThread.BeginInvokeOnMainThread(() => {
                    ChatMessageReceived?.Invoke(this, new ChatMessageEventArgs
                    {
                        MessageId = messageId,
                        SenderId = senderId,
                        SenderName = senderName,
                        Message = message,
                        Timestamp = timestamp,
                        IsPending = true
                    });
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling pending message");
            }
        }

        private void HandleMessagesRead(JsonElement element)
        {
            try
            {
                int receiverId = 0;
                List<int> messageIds = new List<int>();

                if (element.TryGetProperty("receiverId", out var receiverIdElement))
                {
                    receiverId = receiverIdElement.GetInt32();
                }

                if (element.TryGetProperty("messageIds", out var messageIdsElement) &&
                    messageIdsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var idElement in messageIdsElement.EnumerateArray())
                    {
                        messageIds.Add(idElement.GetInt32());
                    }
                }

                // Raise event on the UI thread
                MainThread.BeginInvokeOnMainThread(() => {
                    MessageStatusChanged?.Invoke(this, new MessageStatusEventArgs
                    {
                        RecipientId = receiverId,
                        MessageIds = messageIds,
                        Status = MessageStatus.Read,
                        Timestamp = DateTime.UtcNow
                    });
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling messages read notification");
            }
        }

        private void HandleMessagesDelivered(JsonElement element)
        {
            try
            {
                int receiverId = 0;
                List<int> messageIds = new List<int>();

                if (element.TryGetProperty("receiverId", out var receiverIdElement))
                {
                    receiverId = receiverIdElement.GetInt32();
                }

                if (element.TryGetProperty("messageIds", out var messageIdsElement) &&
                    messageIdsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var idElement in messageIdsElement.EnumerateArray())
                    {
                        messageIds.Add(idElement.GetInt32());
                    }
                }

                // Raise event on the UI thread
                MainThread.BeginInvokeOnMainThread(() => {
                    MessageStatusChanged?.Invoke(this, new MessageStatusEventArgs
                    {
                        RecipientId = receiverId,
                        MessageIds = messageIds,
                        Status = MessageStatus.Delivered,
                        Timestamp = DateTime.UtcNow
                    });
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling messages delivered notification");
            }
        }

        private void HandleUserStatus(JsonElement element)
        {
            try
            {
                int userId = 0;
                string username = null;
                bool isConnected = false;
                string machineName = null;
                string presenceStatus = "Offline";
                string statusMessage = null;

                if (element.TryGetProperty("userId", out var userIdElement))
                {
                    userId = userIdElement.GetInt32();
                }

                if (element.TryGetProperty("username", out var usernameElement))
                {
                    // When assigning from .GetString(), use ?? string.Empty
                    username = usernameElement.GetString() ?? string.Empty;
                }

                if (element.TryGetProperty("isConnected", out var isConnectedElement))
                {
                    isConnected = isConnectedElement.GetBoolean();
                }

                if (element.TryGetProperty("machineName", out var machineNameElement) &&
                    machineNameElement.ValueKind != JsonValueKind.Null)
                {
                    // When assigning from .GetString(), use ?? string.Empty
                    machineName = machineNameElement.GetString() ?? string.Empty;
                }

                if (element.TryGetProperty("presenceStatus", out var presenceStatusElement) &&
                    presenceStatusElement.ValueKind != JsonValueKind.Null)
                {
                    presenceStatus = presenceStatusElement.GetString() ?? "Offline";
                }

                if (element.TryGetProperty("statusMessage", out var statusMessageElement) &&
                    statusMessageElement.ValueKind != JsonValueKind.Null)
                {
                    // When assigning from .GetString(), use ?? string.Empty
                    statusMessage = statusMessageElement.GetString() ?? string.Empty;
                }

                // Raise event on the UI thread
                MainThread.BeginInvokeOnMainThread(() => {
                    UserStatusChanged?.Invoke(this, new UserStatusEventArgs
                    {
                        UserId = userId,
                        Username = username,
                        IsConnected = isConnected,
                        MachineName = machineName,
                        PresenceStatus = presenceStatus,
                        StatusMessage = statusMessage,
                        Timestamp = DateTime.UtcNow
                    });
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling user status message");
            }
        }

        private void HandleBroadcastNotification(JsonElement element)
        {
            try
            {
                string message = null;
                string title = "System Notification";
                NotificationType type = NotificationType.Info;

                if (element.TryGetProperty("message", out var messageElement))
                {
                    message = messageElement.GetString();
                }

                if (element.TryGetProperty("title", out var titleElement) &&
                    titleElement.ValueKind != JsonValueKind.Null)
                {
                    title = titleElement.GetString() ?? "System Notification";
                }

                if (element.TryGetProperty("type", out var typeElement) &&
                    typeElement.ValueKind == JsonValueKind.String)
                {
                    var typeStr = typeElement.GetString()?.ToLower();
                    if (typeStr == "error" || typeStr == "danger")
                    {
                        type = NotificationType.Error;
                    }
                    else if (typeStr == "warning" || typeStr == "warn")
                    {
                        type = NotificationType.Warning;
                    }
                    else if (typeStr == "success")
                    {
                        type = NotificationType.Success;
                    }
                }

                // Raise event on the UI thread
                MainThread.BeginInvokeOnMainThread(() => {
                    NotificationReceived?.Invoke(this, new NotificationEventArgs
                    {
                        Title = title,
                        Message = message,
                        Type = type,
                        Timestamp = DateTime.UtcNow
                    });
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling broadcast notification");
            }
        }

        private void HandleUserStatusChange(JsonElement element)
        {
            try
            {
                int userId = 0;
                string username = null;
                bool isConnected = false;
                string presenceStatus = "Offline";
                string statusMessage = null;

                if (element.TryGetProperty("userId", out var userIdElement))
                {
                    userId = userIdElement.GetInt32();
                }

                if (element.TryGetProperty("username", out var usernameElement))
                {
                    // When assigning from .GetString(), use ?? string.Empty
                    username = usernameElement.GetString() ?? string.Empty;
                }

                if (element.TryGetProperty("isConnected", out var isConnectedElement))
                {
                    isConnected = isConnectedElement.GetBoolean();
                }

                if (element.TryGetProperty("presenceStatus", out var presenceStatusElement) &&
                    presenceStatusElement.ValueKind != JsonValueKind.Null)
                {
                    presenceStatus = presenceStatusElement.GetString() ?? "Offline";
                }

                if (element.TryGetProperty("statusMessage", out var statusMessageElement) &&
                    statusMessageElement.ValueKind != JsonValueKind.Null)
                {
                    // When assigning from .GetString(), use ?? string.Empty
                    statusMessage = statusMessageElement.GetString() ?? string.Empty;
                }

                // Log the update
                _logger.LogDebug("User status change: {Username} is now {Status}",
                    username, presenceStatus);

                // Raise event on the UI thread
                MainThread.BeginInvokeOnMainThread(() => {
                    UserStatusChanged?.Invoke(this, new UserStatusEventArgs
                    {
                        UserId = userId,
                        Username = username,
                        IsConnected = isConnected,
                        PresenceStatus = presenceStatus,
                        StatusMessage = statusMessage,
                        Timestamp = DateTime.UtcNow
                    });
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling user status change");
            }
        }

        private void HandleUserAvailabilityChange(JsonElement element)
        {
            try
            {
                int userId = 0;
                string username = null;
                bool isAvailableForChat = false;

                if (element.TryGetProperty("userId", out var userIdElement))
                {
                    userId = userIdElement.GetInt32();
                }

                if (element.TryGetProperty("username", out var usernameElement))
                {
                    username = usernameElement.GetString();
                }

                if (element.TryGetProperty("isAvailableForChat", out var availableElement))
                {
                    isAvailableForChat = availableElement.GetBoolean();
                }

                // Log the update
                _logger.LogDebug("User availability change: {Username} is now {Available} for chat",
                    username, isAvailableForChat ? "available" : "unavailable");

                // Raise event on the UI thread
                MainThread.BeginInvokeOnMainThread(() => {
                    UserAvailabilityChanged?.Invoke(this, new UserAvailabilityEventArgs
                    {
                        UserId = userId,
                        Username = username,
                        IsAvailableForChat = isAvailableForChat,
                        Timestamp = DateTime.UtcNow
                    });
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling user availability change");
            }
        }

        private void HandleMessageStatus(JsonElement element)
        {
            try
            {
                int messageId = 0;
                string status = null;

                if (element.TryGetProperty("messageId", out var messageIdElement))
                {
                    messageId = messageIdElement.GetInt32();
                }

                if (element.TryGetProperty("status", out var statusElement))
                {
                    status = statusElement.GetString();
                }

                if (Enum.TryParse<MessageStatus>(status, true, out var parsedStatus))
                {
                    _logger.LogInformation("Received status update for message {MessageId}: {Status}", messageId, parsedStatus);
                    // Update UI or local cache based on status
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling message status");
            }
        }

        private async Task HandleDisconnect(bool wasClean)
        {
            // Raise disconnect event on the UI thread
            MainThread.BeginInvokeOnMainThread(() => {
                ConnectionStatusChanged?.Invoke(this, new ConnectionStatusEventArgs
                {
                    IsConnected = false,
                    WasClean = wasClean,
                    Timestamp = DateTime.UtcNow
                });
            });

            if (!wasClean)
            {
                // Try to reconnect if the disconnect wasn't clean
                StartReconnectTimer();
            }
            else
            {
                // Just clean up resources
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

            // Exponential backoff (1s, 2s, 4s, 8s, 16s...)
            var delayMs = (int)Math.Min(1000 * Math.Pow(2, _reconnectAttempts - 1), 30000); // Max 30 seconds

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
                    // Reset reconnect attempts counter on successful connection
                    _reconnectAttempts = 0;
                }
                else if (_reconnectAttempts < MaxReconnectAttempts)
                {
                    // Try again with exponential backoff
                    StartReconnectTimer();
                }
                else
                {
                    _logger.LogWarning("Failed to reconnect after {MaxAttempts} attempts", MaxReconnectAttempts);
                    // Notify the UI that we've given up on reconnection
                    MainThread.BeginInvokeOnMainThread(() => {
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
                var json = TDFShared.Helpers.JsonSerializationHelper.SerializeCompact(message);
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

                // Always clean up resources
                await CleanupExistingConnectionAsync();

                // Notify that we're disconnected
                MainThread.BeginInvokeOnMainThread(() => {
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

        public async Task SendChatMessageAsync(int receiverId, string message)
        {
            await SendMessageAsync(new
            {
                type = ApiRoutes.WebSocket.MessageTypes.ChatMessage,
                receiverId = receiverId,
                message = message,
                timestamp = DateTime.UtcNow
            });
        }

        public async Task JoinGroupAsync(string group)
        {
            await SendMessageAsync(new
            {
                type = "join_group",
                group = group
            });
        }

        public async Task LeaveGroupAsync(string group)
        {
            await SendMessageAsync(new
            {
                type = "leave_group",
                group = group
            });
        }

        public async Task MarkMessagesAsReadAsync(int senderId)
        {
            await SendMessageAsync(new
            {
                type = "mark_as_read",
                senderId = senderId
            });
        }

        public async Task MarkMessagesAsDeliveredAsync(int senderId)
        {
            await SendMessageAsync(new
            {
                type = "mark_as_delivered",
                senderId = senderId
            });
        }

        public async Task MarkMessagesAsDeliveredAsync(IEnumerable<int> messageIds)
        {
            if (messageIds == null || !messageIds.Any())
            {
                _logger.LogWarning("Attempted to mark messages as delivered but no message IDs were provided");
                return;
            }

            await SendMessageAsync(new
            {
                type = "mark_messages_as_delivered",
                messageIds = messageIds.ToList()
            });
        }

        public async Task MarkNotificationsAsSeenAsync(IEnumerable<int> notificationIds)
        {
            await SendMessageAsync(new
            {
                type = "mark_notifications_seen",
                notificationIds = notificationIds.ToList()
            });
        }

        public async Task UpdatePresenceStatusAsync(string status, string? statusMessage = null)
        {
            await SendMessageAsync(new
            {
                type = ApiRoutes.WebSocket.MessageTypes.UserPresence,
                status = status ?? string.Empty,
                statusMessage = statusMessage ?? string.Empty,
                timestamp = DateTime.UtcNow
            });
        }

        public async Task SetAvailableForChatAsync(bool isAvailable)
        {
            await SendMessageAsync(new
            {
                type = "set_availability",
                isAvailable = isAvailable,
                timestamp = DateTime.UtcNow
            });
        }

        public async Task SendActivityPingAsync()
        {
            await SendMessageAsync(new
            {
                type = ApiRoutes.WebSocket.MessageTypes.Ping,
                timestamp = DateTime.UtcNow
            });
        }

        private void HandleNotificationSeen(JsonElement element)
        {
            try
            {
                var notificationId = element.GetProperty("notificationId").GetInt32();
                var timestamp = element.GetProperty("timestamp").GetDateTime();

                MainThread.BeginInvokeOnMainThread(() => {
                    try
                    {
                        NotificationReceived?.Invoke(this, new TDFShared.DTOs.Messages.NotificationEventArgs
                        {
                            Type = TDFShared.Enums.NotificationType.Info,
                            Message = $"Notification {notificationId} marked as seen",
                            Timestamp = timestamp
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error invoking NotificationReceived event");
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling notification seen event");
            }
        }

        private void HandleNotificationsSeen(JsonElement element)
        {
            try
            {
                var notificationIds = element.GetProperty("notificationIds").EnumerateArray()
                    .Select(x => x.GetInt32())
                    .ToList();
                var timestamp = element.GetProperty("timestamp").GetDateTime();

                MainThread.BeginInvokeOnMainThread(() => {
                    try
                    {
                        NotificationReceived?.Invoke(this, new TDFShared.DTOs.Messages.NotificationEventArgs
                        {
                            Type = TDFShared.Enums.NotificationType.Info,
                            Message = $"Notifications {string.Join(", ", notificationIds)} marked as seen",
                            Timestamp = timestamp
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error invoking NotificationReceived event");
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling notifications seen event");
            }
        }

        private void HandleAvailabilitySet(JsonElement element)
        {
            try
            {
                var isAvailable = element.GetProperty("isAvailable").GetBoolean();
                var timestamp = element.GetProperty("timestamp").GetDateTime();

                MainThread.BeginInvokeOnMainThread(() => {
                    try
                    {
                        AvailabilitySet?.Invoke(this, new TDFMAUI.Helpers.AvailabilitySetEventArgs
                        {
                            IsAvailable = isAvailable,
                            Timestamp = timestamp
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error invoking AvailabilitySet event");
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling availability set event");
            }
        }

        private void HandleStatusUpdated(JsonElement element)
        {
            try
            {
                var status = element.GetProperty("status").GetString();
                var statusMessage = element.GetProperty("statusMessage").GetString();
                var timestamp = element.GetProperty("timestamp").GetDateTime();

                MainThread.BeginInvokeOnMainThread(() => {
                    try
                    {
                        StatusUpdateConfirmed?.Invoke(this, new TDFMAUI.Helpers.StatusUpdateConfirmedEventArgs
                        {
                            Status = status,
                            StatusMessage = statusMessage,
                            Timestamp = timestamp
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error invoking StatusUpdateConfirmed event");
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling status updated event");
            }
        }

        private void HandleError(JsonElement element)
        {
            try
            {
                var errorCode = element.GetProperty("errorCode").GetString();
                var errorMessage = element.GetProperty("errorMessage").GetString();
                var timestamp = element.GetProperty("timestamp").GetDateTime();
                
                ErrorReceived?.Invoke(this, new WebSocketErrorEventArgs
                {
                    ErrorCode = errorCode,
                    ErrorMessage = errorMessage,
                    Timestamp = timestamp
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling error event");
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                try
                {
                    // Dispose managed resources
                    _connectionLock?.Dispose();
                    _reconnectTimer?.Dispose();

                    // Use a timeout to prevent hanging on disposal
                    var disconnectTask = DisconnectAsync(false);
                    if (!disconnectTask.Wait(TimeSpan.FromSeconds(2)))
                    {
                        _logger.LogWarning("WebSocketService disconnect timed out during disposal");
                    }

                    // Cancel any ongoing operations
                    if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
                    {
                        _cancellationTokenSource.Cancel();
                        _cancellationTokenSource.Dispose();
                        _cancellationTokenSource = null;
                    }

                    // Dispose the WebSocket
                    if (_webSocket != null)
                    {
                        _webSocket.Dispose();
                        _webSocket = null;
                    }
                }
                catch (Exception ex)
                {
                    // Log but don't throw from Dispose
                    _logger.LogError(ex, "Error during WebSocketService disposal");
                }
            }

            // Dispose unmanaged resources
            // (none in this class)

            _disposed = true;
        }

        ~WebSocketService()
        {
            Dispose(false);
        }
    }
}
