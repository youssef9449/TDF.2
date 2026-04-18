using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TDFMAUI.Helpers;
using TDFShared.Constants;
using TDFShared.DTOs.Messages;
using TDFShared.Enums;
using TDFMAUI.Services.Notifications;

namespace TDFMAUI.Services.WebSocket
{
    /// <summary>
    /// Default <see cref="IWebSocketMessageRouter"/>. Every branch of
    /// <see cref="RouteAsync(string)"/> matches a message type that the server
    /// currently emits; unknown types are logged and ignored.
    /// </summary>
    public sealed class WebSocketMessageRouter : IWebSocketMessageRouter
    {
        private readonly ILogger<WebSocketMessageRouter> _logger;

        public event EventHandler<NotificationEventArgs> NotificationReceived = delegate { };
        public event EventHandler<ChatMessageEventArgs>? ChatMessageReceived;
        public event EventHandler<MessageStatusEventArgs>? MessageStatusChanged;
        public event EventHandler<UserStatusEventArgs>? UserStatusChanged;
        public event EventHandler<UserAvailabilityEventArgs>? UserAvailabilityChanged;
        public event EventHandler<AvailabilitySetEventArgs>? AvailabilitySet;
        public event EventHandler<StatusUpdateConfirmedEventArgs>? StatusUpdateConfirmed;
        public event EventHandler<WebSocketErrorEventArgs>? ErrorReceived;

        public WebSocketMessageRouter(ILogger<WebSocketMessageRouter> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task RouteAsync(string jsonMessage)
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
                    case "pending_message":
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
                        HandleUserStatus(root);
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
                    case "availability_set":
                        HandleAvailabilitySet(root);
                        break;
                    case "status_updated":
                        HandleStatusUpdated(root);
                        break;
                    case ApiRoutes.WebSocket.MessageTypes.Error:
                        HandleError(root);
                        break;
                    case ApiRoutes.WebSocket.MessageTypes.Pong:
                        _logger.LogTrace("Pong received from server");
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

        private void RaiseOnMainThread(Action action)
        {
            try
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        action();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error invoking WebSocket router event");
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marshalling WebSocket router event to main thread");
            }
        }

        private void HandleNotification(JsonElement element)
        {
            try
            {
                if (!element.TryGetProperty("notification", out var notificationElement))
                {
                    return;
                }

                int notificationId = 0;
                int? senderId = null;
                string senderName = "System";
                string message = string.Empty;
                DateTime timestamp = DateTime.UtcNow;

                if (notificationElement.TryGetProperty("notificationId", out var idElement))
                {
                    notificationId = idElement.GetInt32();
                }

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

                if (notificationElement.TryGetProperty("message", out var messageElement))
                {
                    message = messageElement.GetString() ?? string.Empty;
                }

                if (notificationElement.TryGetProperty("timestamp", out var timeElement))
                {
                    if (DateTime.TryParse(timeElement.GetString(), out var parsedTime))
                    {
                        timestamp = parsedTime;
                    }
                }

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

                RaiseOnMainThread(() => NotificationReceived?.Invoke(this, eventArgs));
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
                string? message = null;
                int senderId = 0;
                string? senderName = null;
                DateTime timestamp = DateTime.UtcNow;

                if (element.TryGetProperty("message", out var messageElement))
                {
                    message = messageElement.GetString() ?? string.Empty;
                }

                if (element.TryGetProperty("senderId", out var senderIdElement))
                {
                    senderId = senderIdElement.GetInt32();
                }

                if (element.TryGetProperty("senderName", out var senderNameElement))
                {
                    senderName = senderNameElement.GetString() ?? string.Empty;
                }

                if (element.TryGetProperty("timestamp", out var timestampElement))
                {
                    timestamp = timestampElement.GetDateTime();
                }

                RaiseOnMainThread(() =>
                {
                    ChatMessageReceived?.Invoke(this, new ChatMessageEventArgs
                    {
                        SenderId = senderId,
                        SenderName = senderName,
                        Message = message,
                        Timestamp = timestamp
                    });

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
                int messageId = 0;
                string? message = null;
                int senderId = 0;
                string? senderName = null;
                DateTime timestamp = DateTime.UtcNow;

                if (element.TryGetProperty("messageId", out var messageIdElement))
                {
                    messageId = messageIdElement.GetInt32();
                }

                if (element.TryGetProperty("message", out var messageElement))
                {
                    message = messageElement.GetString() ?? string.Empty;
                }

                if (element.TryGetProperty("senderId", out var senderIdElement))
                {
                    senderId = senderIdElement.GetInt32();
                }

                if (element.TryGetProperty("senderName", out var senderNameElement))
                {
                    senderName = senderNameElement.GetString() ?? string.Empty;
                }

                if (element.TryGetProperty("timestamp", out var timestampElement))
                {
                    timestamp = timestampElement.GetDateTime();
                }

                RaiseOnMainThread(() => ChatMessageReceived?.Invoke(this, new ChatMessageEventArgs
                {
                    MessageId = messageId,
                    SenderId = senderId,
                    SenderName = senderName,
                    Message = message,
                    Timestamp = timestamp,
                    IsPending = true
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling pending message");
            }
        }

        private void HandleMessagesRead(JsonElement element) =>
            HandleMessageStatusChange(element, MessageStatus.Read, "Error handling messages read notification");

        private void HandleMessagesDelivered(JsonElement element) =>
            HandleMessageStatusChange(element, MessageStatus.Delivered, "Error handling messages delivered notification");

        private void HandleMessageStatusChange(JsonElement element, MessageStatus status, string errorMessage)
        {
            try
            {
                int receiverId = 0;
                var messageIds = new List<int>();

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

                RaiseOnMainThread(() => MessageStatusChanged?.Invoke(this, new MessageStatusEventArgs
                {
                    RecipientId = receiverId,
                    MessageIds = messageIds,
                    Status = status,
                    Timestamp = DateTime.UtcNow
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, errorMessage);
            }
        }

        private void HandleUserStatus(JsonElement element)
        {
            try
            {
                int userId = 0;
                string? username = null;
                bool isConnected = false;
                string? machineName = null;
                string presenceStatus = "Offline";
                string? statusMessage = null;

                if (element.TryGetProperty("userId", out var userIdElement))
                {
                    userId = userIdElement.GetInt32();
                }

                if (element.TryGetProperty("username", out var usernameElement))
                {
                    username = usernameElement.GetString() ?? string.Empty;
                }

                if (element.TryGetProperty("isConnected", out var isConnectedElement))
                {
                    isConnected = isConnectedElement.GetBoolean();
                }

                if (element.TryGetProperty("machineName", out var machineNameElement) &&
                    machineNameElement.ValueKind != JsonValueKind.Null)
                {
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
                    statusMessage = statusMessageElement.GetString() ?? string.Empty;
                }

                RaiseOnMainThread(() => UserStatusChanged?.Invoke(this, new UserStatusEventArgs
                {
                    UserId = userId,
                    Username = username,
                    IsConnected = isConnected,
                    MachineName = machineName,
                    PresenceStatus = presenceStatus,
                    StatusMessage = statusMessage,
                    Timestamp = DateTime.UtcNow
                }));
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
                string? message = null;
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

                RaiseOnMainThread(() => NotificationReceived?.Invoke(this, new NotificationEventArgs
                {
                    Title = title,
                    Message = message,
                    Type = type,
                    Timestamp = DateTime.UtcNow
                }));
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
                string? username = null;
                bool isConnected = false;
                string presenceStatus = "Offline";
                string? statusMessage = null;

                if (element.TryGetProperty("userId", out var userIdElement))
                {
                    userId = userIdElement.GetInt32();
                }

                if (element.TryGetProperty("username", out var usernameElement))
                {
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
                    statusMessage = statusMessageElement.GetString() ?? string.Empty;
                }

                _logger.LogDebug("User status change: {Username} is now {Status}", username, presenceStatus);

                RaiseOnMainThread(() => UserStatusChanged?.Invoke(this, new UserStatusEventArgs
                {
                    UserId = userId,
                    Username = username,
                    IsConnected = isConnected,
                    PresenceStatus = presenceStatus,
                    StatusMessage = statusMessage,
                    Timestamp = DateTime.UtcNow
                }));
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
                string? username = null;
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

                _logger.LogDebug("User availability change: {Username} is now {Available} for chat",
                    username, isAvailableForChat ? "available" : "unavailable");

                RaiseOnMainThread(() => UserAvailabilityChanged?.Invoke(this, new UserAvailabilityEventArgs
                {
                    UserId = userId,
                    Username = username,
                    IsAvailableForChat = isAvailableForChat,
                    Timestamp = DateTime.UtcNow
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling user availability change");
            }
        }

        private void HandleNotificationSeen(JsonElement element)
        {
            try
            {
                var notificationId = element.GetProperty("notificationId").GetInt32();
                var timestamp = element.GetProperty("timestamp").GetDateTime();

                RaiseOnMainThread(() => NotificationReceived?.Invoke(this, new NotificationEventArgs
                {
                    Type = NotificationType.Info,
                    Message = $"Notification {notificationId} marked as seen",
                    Timestamp = timestamp
                }));
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

                RaiseOnMainThread(() => NotificationReceived?.Invoke(this, new NotificationEventArgs
                {
                    Type = NotificationType.Info,
                    Message = $"Notifications {string.Join(", ", notificationIds)} marked as seen",
                    Timestamp = timestamp
                }));
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

                RaiseOnMainThread(() => AvailabilitySet?.Invoke(this, new AvailabilitySetEventArgs
                {
                    IsAvailable = isAvailable,
                    Timestamp = timestamp
                }));
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

                RaiseOnMainThread(() => StatusUpdateConfirmed?.Invoke(this, new StatusUpdateConfirmedEventArgs
                {
                    Status = status,
                    StatusMessage = statusMessage,
                    Timestamp = timestamp
                }));
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
    }
}
