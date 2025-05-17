using Microsoft.Extensions.Logging;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using TDFMAUI.Config;
using TDFMAUI.Helpers;
using TDFShared.DTOs.Messages;
using TDFShared.Enums;
using TDFShared.Helpers;

namespace TDFMAUI.Services
{
    public class WebSocketService : IWebSocketService, IDisposable
    {
        private ClientWebSocket _webSocket;
        private readonly ILogger<WebSocketService> _logger;
        private readonly SecureStorageService _secureStorage;
        private string _serverUrl => ApiConfig.WebSocketUrl;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly SemaphoreSlim _connectionLock = new SemaphoreSlim(1, 1);
        private bool _isConnecting = false;
        private Timer _reconnectTimer;
        private int _reconnectAttempts = 0;
        private const int MaxReconnectAttempts = 5;

        // Events for different types of messages
        public event EventHandler<NotificationEventArgs> NotificationReceived;
        public event EventHandler<ChatMessageEventArgs> ChatMessageReceived;
        public event EventHandler<MessageStatusEventArgs> MessageStatusChanged;
        public event EventHandler<ConnectionStatusEventArgs> ConnectionStatusChanged;
        public event EventHandler<UserStatusEventArgs> UserStatusChanged;
        public event EventHandler<UserAvailabilityEventArgs> UserAvailabilityChanged;
        // New Events Added:
        public event EventHandler<AvailabilitySetEventArgs> AvailabilitySet;
        public event EventHandler<StatusUpdateConfirmedEventArgs> StatusUpdateConfirmed;
        public event EventHandler<WebSocketErrorEventArgs> ErrorReceived;

        public bool IsConnected => _webSocket?.State == WebSocketState.Open;

        public WebSocketService(
            ILogger<WebSocketService> logger,
            SecureStorageService secureStorage)
        {
            // Log constructor entry
            logger?.LogInformation("WebSocketService constructor started.");

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _secureStorage = secureStorage;

            // Setup reconnect timer but don't start it yet
            _reconnectTimer = new Timer(ReconnectCallback, null, Timeout.Infinite, Timeout.Infinite);

            // Log constructor exit
            logger?.LogInformation("WebSocketService constructor finished.");
        }

        public async Task<bool> ConnectAsync(string token = null)
        {
            // Check if already connecting
            if (_isConnecting)
            {
                _logger.LogInformation("WebSocket connection already in progress");
                return false;
            }

            // Ensure we're not connecting simultaneously from multiple places
            await _connectionLock.WaitAsync();
            _isConnecting = true;

            try
            {
                // First check if already connected
                if (_webSocket?.State == WebSocketState.Open)
                {
                    _logger.LogDebug("WebSocket already connected");
                    return true;
                }

                // Dispose of existing WebSocket if any
                await CleanupExistingConnectionAsync();

                // Get the current token if one wasn't provided
                if (string.IsNullOrEmpty(token))
                {
                    var tokenResult = await _secureStorage.GetTokenAsync();
                    token = tokenResult.Item1;
                    if (string.IsNullOrEmpty(token))
                    {
                        _logger.LogWarning("Cannot connect WebSocket: No authentication token available");
                        return false;
                    }
                    else
                    {
                        _logger.LogInformation("Retrieved token from secure storage for WebSocket connection. Token length: {Length}", token.Length);
                    }
                }
                else
                {
                    _logger.LogInformation("Using provided token for WebSocket connection. Token length: {Length}", token.Length);
                }

                // Create new WebSocket and cancellation token
                _webSocket = new ClientWebSocket();
                _cancellationTokenSource = new CancellationTokenSource();

                // Add authentication header
                _webSocket.Options.SetRequestHeader("Authorization", $"Bearer {token}");

                // Log token length for debugging (don't log the actual token)
                _logger.LogInformation("WebSocket using token of length {TokenLength}", token?.Length ?? 0);

                // Log the first and last 5 characters of the token for debugging (but not the whole token)
                if (token?.Length > 10)
                {
                    string tokenPrefix = token.Substring(0, 5);
                    string tokenSuffix = token.Substring(token.Length - 5);
                    _logger.LogInformation("WebSocket token prefix: {Prefix}..., suffix: ...{Suffix}", tokenPrefix, tokenSuffix);
                }

                // Add explicit certificate validation for development mode
                if (ApiConfig.DevelopmentMode)
                {
                    _webSocket.Options.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
                    _logger.LogWarning("WebSocket configured with development certificate validation (ALLOW ALL)");
                }

                // Connect to the WebSocket server
                var serverUri = new Uri(_serverUrl);
                _logger.LogInformation("Connecting to WebSocket server at {Url}", serverUri);

                try
                {
                    // Set a reasonable timeout for the connection
                    _cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(15));

                    await _webSocket.ConnectAsync(serverUri, _cancellationTokenSource.Token);

                    // Create a new cancellation token source now that connection succeeded
                    _cancellationTokenSource.Dispose();
                    _cancellationTokenSource = new CancellationTokenSource();

                    _logger.LogInformation("Connected to WebSocket server");

                    // Reset reconnect attempts on successful connection
                    _reconnectAttempts = 0;

                    // Raise connection status event on the UI thread
                    MainThread.BeginInvokeOnMainThread(() => {
                        ConnectionStatusChanged?.Invoke(this, new ConnectionStatusEventArgs
                        {
                            IsConnected = true,
                            Timestamp = DateTime.UtcNow
                        });
                    });

                    // Start receiving messages
                    StartReceiving();

                    // Send a ping to verify connection
                    await SendMessageAsync(new
                    {
                        type = "ping",
                        timestamp = DateTime.UtcNow
                    });

                    return true;
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("WebSocket connection timed out");
                    await CleanupExistingConnectionAsync();
                    // Return false but don't throw - allow the app to function without WebSocket
                    return false;
                }
                catch (WebSocketException wsEx) when (wsEx.Message.Contains("401"))
                {
                    _logger.LogError(wsEx, "Authentication failed (401 Unauthorized) when connecting to WebSocket server. Token may be invalid or expired.");

                    // Raise error event
                    MainThread.BeginInvokeOnMainThread(() => {
                        ErrorReceived?.Invoke(this, new WebSocketErrorEventArgs
                        {
                            ErrorCode = "401",
                            ErrorMessage = "Authentication failed. Please log out and log in again.",
                            Timestamp = DateTime.UtcNow
                        });
                    });

                    await CleanupExistingConnectionAsync();
                    return false;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to connect to WebSocket server - continuing app in limited functionality mode");

                    // Raise error event
                    MainThread.BeginInvokeOnMainThread(() => {
                        ErrorReceived?.Invoke(this, new WebSocketErrorEventArgs
                        {
                            ErrorCode = ex.GetType().Name,
                            ErrorMessage = ex.Message,
                            Timestamp = DateTime.UtcNow
                        });
                    });

                    await CleanupExistingConnectionAsync();
                    // Return false instead of throwing to allow the app to continue
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
                while (_webSocket.State == WebSocketState.Open)
                {
                    WebSocketReceiveResult receiveResult = null;

                    try
                    {
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
                    case "notification":
                        HandleNotification(root);
                        break;
                    case "chat_message":
                        HandleChatMessage(root);
                        break;
                    case "pending_message": // Handle messages delivered while offline
                        HandlePendingMessage(root);
                        break;
                    case "messages_read":
                        HandleMessagesRead(root);
                        break;
                    case "messages_delivered":
                        HandleMessagesDelivered(root);
                        break;
                    case "user_status": // Maybe legacy or different type?
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
                    case "error":
                        HandleError(root);
                        break;
                    // Keep pong or other control messages if necessary
                    case "pong":
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
                    var eventArgs = new NotificationEventArgs
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
                    message = messageElement.GetString();
                }

                if (element.TryGetProperty("senderId", out var senderIdElement))
                {
                    senderId = senderIdElement.GetInt32();
                }

                if (element.TryGetProperty("senderName", out var senderNameElement))
                {
                    senderName = senderNameElement.GetString();
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
                    NotificationReceived?.Invoke(this, new NotificationEventArgs
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
                    message = messageElement.GetString();
                }

                if (element.TryGetProperty("senderId", out var senderIdElement))
                {
                    senderId = senderIdElement.GetInt32();
                }

                if (element.TryGetProperty("senderName", out var senderNameElement))
                {
                    senderName = senderNameElement.GetString();
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
                    username = usernameElement.GetString();
                }

                if (element.TryGetProperty("isConnected", out var isConnectedElement))
                {
                    isConnected = isConnectedElement.GetBoolean();
                }

                if (element.TryGetProperty("machineName", out var machineNameElement) &&
                    machineNameElement.ValueKind != JsonValueKind.Null)
                {
                    machineName = machineNameElement.GetString();
                }

                if (element.TryGetProperty("presenceStatus", out var presenceStatusElement) &&
                    presenceStatusElement.ValueKind != JsonValueKind.Null)
                {
                    presenceStatus = presenceStatusElement.GetString() ?? "Offline";
                }

                if (element.TryGetProperty("statusMessage", out var statusMessageElement) &&
                    statusMessageElement.ValueKind != JsonValueKind.Null)
                {
                    statusMessage = statusMessageElement.GetString();
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
                    username = usernameElement.GetString();
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
                    statusMessage = statusMessageElement.GetString();
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
                var json = JsonSerializer.Serialize(message);
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

        // Client-side methods for specific operations
        public async Task SendChatMessageAsync(int receiverId, string message)
        {
            await SendMessageAsync(new
            {
                type = "chat_message",
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

        // Add new methods to send user status updates
        public async Task UpdatePresenceStatusAsync(string status, string statusMessage = null)
        {
            await SendMessageAsync(new
            {
                type = "update_status",
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

        // Add the ping method to record activity
        public async Task SendActivityPingAsync()
        {
            await SendMessageAsync(new
            {
                type = "activity_ping",
                timestamp = DateTime.UtcNow
            });
        }

        // IDisposable Implementation
        private bool _disposed = false;

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
                // Dispose managed resources
                _connectionLock?.Dispose();
                _reconnectTimer?.Dispose();
                DisconnectAsync(true).ConfigureAwait(false).GetAwaiter().GetResult();
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                _webSocket?.Dispose();
            }

            // Dispose unmanaged resources
            // (none in this class)

            _disposed = true;
        }

        ~WebSocketService()
        {
            Dispose(false);
        }

        private void HandleNotificationSeen(JsonElement element)
        {
            try
            {
                int notificationId = 0;

                if (element.TryGetProperty("notificationId", out var notificationIdElement))
                {
                    notificationId = notificationIdElement.GetInt32();
                }

                // You could raise an event or update UI directly
                // For now, just log it
                _logger.LogDebug("Notification {NotificationId} marked as seen", notificationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling notification seen message");
            }
        }

        private void HandleNotificationsSeen(JsonElement element)
        {
            try
            {
                var notificationIds = new List<int>();

                if (element.TryGetProperty("notificationIds", out var idsElement) &&
                    idsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var idElement in idsElement.EnumerateArray())
                    {
                        notificationIds.Add(idElement.GetInt32());
                    }
                }

                // You could raise an event or update UI directly
                _logger.LogDebug("Multiple notifications marked as seen: {NotificationIds}",
                    string.Join(", ", notificationIds));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling notifications seen message");
            }
        }

        private void HandleAvailabilitySet(JsonElement element)
        {
            try
            {
                bool isAvailable = false;
                DateTime timestamp = DateTime.UtcNow;
                if (element.TryGetProperty("isAvailable", out var availableElement))
                {
                    isAvailable = availableElement.GetBoolean();
                }
                if (element.TryGetProperty("timestamp", out var ts) && ts.ValueKind == JsonValueKind.String)
                {
                     DateTime.TryParse(ts.GetString(), out timestamp);
                }

                _logger.LogInformation("Server confirmed availability set to: {IsAvailable}", isAvailable);
                // Raise the event
                MainThread.BeginInvokeOnMainThread(() => {
                    AvailabilitySet?.Invoke(this, new AvailabilitySetEventArgs
                    {
                        IsAvailable = isAvailable,
                        Timestamp = timestamp
                    });
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling availability_set message");
            }
        }

        private void HandleStatusUpdated(JsonElement element)
        {
             try
            {
                string status = "";
                string statusMessage = null;
                DateTime timestamp = DateTime.UtcNow;

                if (element.TryGetProperty("status", out var statusElement))
                {
                    status = statusElement.GetString();
                }
                 if (element.TryGetProperty("statusMessage", out var msgElement) && msgElement.ValueKind == JsonValueKind.String)
                {
                    statusMessage = msgElement.GetString();
                }
                 if (element.TryGetProperty("timestamp", out var ts) && ts.ValueKind == JsonValueKind.String)
                {
                     DateTime.TryParse(ts.GetString(), out timestamp);
                }

                _logger.LogInformation("Server confirmed status updated to: {Status} ({StatusMessage})", status, statusMessage ?? "null");
                // Raise the event
                 MainThread.BeginInvokeOnMainThread(() => {
                    StatusUpdateConfirmed?.Invoke(this, new StatusUpdateConfirmedEventArgs
                    {
                        Status = status,
                        StatusMessage = statusMessage,
                        Timestamp = timestamp
                    });
                 });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling status_updated message");
            }
        }

        private void HandleError(JsonElement element)
        {
             try
            {
                string errorMessage = "An unknown error occurred.";
                string errorCode = null;
                DateTime timestamp = DateTime.UtcNow;

                if (element.TryGetProperty("message", out var messageElement))
                {
                    errorMessage = messageElement.GetString() ?? errorMessage;
                }
                if (element.TryGetProperty("code", out var codeElement))
                {
                    errorCode = codeElement.GetString();
                }
                 if (element.TryGetProperty("timestamp", out var ts) && ts.ValueKind == JsonValueKind.String)
                {
                     DateTime.TryParse(ts.GetString(), out timestamp);
                }

                _logger.LogError("Received error from server: Code={ErrorCode}, Message={ErrorMessage}", errorCode ?? "N/A", errorMessage);

                // Raise a specific error event
                 MainThread.BeginInvokeOnMainThread(() => {
                     ErrorReceived?.Invoke(this, new WebSocketErrorEventArgs
                     {
                         ErrorCode = errorCode,
                         ErrorMessage = errorMessage,
                         Timestamp = timestamp
                     });
                 });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling error message");
            }
        }
    }

    // EventArgs definitions moved to TDFMAUI/Helpers/WebSocketEventArgs.cs
}
