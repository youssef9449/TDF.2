using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TDFShared.Constants;
using TDFShared.DTOs.Messages;
using TDFShared.Enums;
using TDFShared.Models.Message;

namespace TDFAPI.Services.Realtime
{
    /// <summary>
    /// Default <see cref="IServerWebSocketRouter"/>. Handles every inbound
    /// message type that <c>TDFMAUI.Services.WebSocketService</c> sends today —
    /// the branches mirror the <c>Send*</c> methods on the client.
    /// </summary>
    public sealed class ServerWebSocketRouter : IServerWebSocketRouter
    {
        private readonly WebSocketConnectionManager _connectionManager;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ServerWebSocketRouter> _logger;

        public ServerWebSocketRouter(
            WebSocketConnectionManager connectionManager,
            IServiceScopeFactory scopeFactory,
            ILogger<ServerWebSocketRouter> logger)
        {
            _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task RouteAsync(
            WebSocketConnectionEntity connection,
            string messageJson,
            CancellationToken cancellationToken)
        {
            if (connection is null) throw new ArgumentNullException(nameof(connection));
            if (string.IsNullOrWhiteSpace(messageJson)) return;

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(messageJson);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex,
                    "Received malformed WebSocket frame on connection {ConnectionId}: {Payload}",
                    connection.ConnectionId, messageJson);
                await SendErrorAsync(connection.ConnectionId, "invalid_json", "Payload is not valid JSON");
                return;
            }

            using (document)
            {
                var root = document.RootElement;
                if (!root.TryGetProperty("type", out var typeElement) ||
                    typeElement.ValueKind != JsonValueKind.String)
                {
                    _logger.LogWarning(
                        "Received WebSocket frame without a 'type' field on connection {ConnectionId}: {Payload}",
                        connection.ConnectionId, messageJson);
                    await SendErrorAsync(connection.ConnectionId, "missing_type", "Payload is missing 'type'");
                    return;
                }

                var type = typeElement.GetString() ?? string.Empty;
                _logger.LogDebug(
                    "Routing WebSocket frame type '{Type}' for user {UserId} on connection {ConnectionId}",
                    type, connection.UserId, connection.ConnectionId);

                try
                {
                    switch (type)
                    {
                        case ApiRoutes.WebSocket.MessageTypes.Ping:
                            await HandlePingAsync(connection.ConnectionId);
                            break;

                        case "join_group":
                            await HandleJoinGroupAsync(connection.ConnectionId, root);
                            break;

                        case "leave_group":
                            await HandleLeaveGroupAsync(connection.ConnectionId, root);
                            break;

                        case ApiRoutes.WebSocket.MessageTypes.ChatMessage:
                            await HandleChatMessageAsync(connection, root, cancellationToken);
                            break;

                        case "mark_as_read":
                            await HandleMarkAsReadBySenderAsync(connection, root, cancellationToken);
                            break;

                        case "mark_as_delivered":
                            await HandleMarkAsDeliveredBySenderAsync(connection, root, cancellationToken);
                            break;

                        case "mark_messages_as_delivered":
                            await HandleMarkMessagesAsDeliveredAsync(connection, root, cancellationToken);
                            break;

                        case "mark_notifications_seen":
                            await HandleMarkNotificationsSeenAsync(connection, root, cancellationToken);
                            break;

                        case ApiRoutes.WebSocket.MessageTypes.UserPresence:
                            await HandlePresenceUpdateAsync(connection, root, cancellationToken);
                            break;

                        case "set_availability":
                            await HandleSetAvailabilityAsync(connection, root, cancellationToken);
                            break;

                        default:
                            _logger.LogWarning(
                                "Unhandled WebSocket frame type '{Type}' from user {UserId}",
                                type, connection.UserId);
                            await SendErrorAsync(
                                connection.ConnectionId,
                                "unsupported_type",
                                $"Unsupported WebSocket message type '{type}'");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Error handling WebSocket frame type '{Type}' from user {UserId}",
                        type, connection.UserId);
                    await SendErrorAsync(connection.ConnectionId, "server_error", "Failed to process message");
                }
            }
        }

        // ----- Handlers ------------------------------------------------------

        private Task HandlePingAsync(string connectionId)
        {
            return _connectionManager.SendToConnectionAsync(connectionId, new
            {
                type = ApiRoutes.WebSocket.MessageTypes.Pong,
                timestamp = DateTime.UtcNow,
            });
        }

        private async Task HandleJoinGroupAsync(string connectionId, JsonElement root)
        {
            var group = GetString(root, "group");
            if (string.IsNullOrWhiteSpace(group))
            {
                await SendErrorAsync(connectionId, "missing_group", "'group' is required for join_group");
                return;
            }

            await _connectionManager.AddToGroupAsync(connectionId, group);
        }

        private async Task HandleLeaveGroupAsync(string connectionId, JsonElement root)
        {
            var group = GetString(root, "group");
            if (string.IsNullOrWhiteSpace(group))
            {
                await SendErrorAsync(connectionId, "missing_group", "'group' is required for leave_group");
                return;
            }

            await _connectionManager.RemoveFromGroupAsync(connectionId, group);
        }

        private async Task HandleChatMessageAsync(
            WebSocketConnectionEntity connection,
            JsonElement root,
            CancellationToken cancellationToken)
        {
            var receiverId = GetInt(root, "receiverId") ?? GetInt(root, "ReceiverId");
            var content = GetString(root, "message") ?? GetString(root, "content");

            if (receiverId is null || receiverId.Value <= 0)
            {
                await SendErrorAsync(connection.ConnectionId, "missing_receiver", "'receiverId' is required for chat_message");
                return;
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                await SendErrorAsync(connection.ConnectionId, "empty_message", "'message' content must not be empty");
                return;
            }

            var dto = new ChatMessageCreateDto
            {
                SenderId = connection.UserId,
                ReceiverId = receiverId.Value,
                Content = content!,
                MessageType = MessageType.Chat,
                IdempotencyKey = GetString(root, "idempotencyKey"),
            };

            using var scope = _scopeFactory.CreateScope();
            var messageService = scope.ServiceProvider.GetRequiredService<IMessageService>();

            var created = await messageService.CreateChatMessageAsync(
                dto,
                connection.UserId,
                connection.Username ?? string.Empty);

            var outbound = new
            {
                type = ApiRoutes.WebSocket.MessageTypes.ChatMessage,
                message = created,
                timestamp = DateTime.UtcNow,
            };

            // Fan out to the receiver, and echo back to the sender so every tab sees the accepted copy.
            await Task.WhenAll(
                _connectionManager.SendToAsync(receiverId.Value, outbound),
                _connectionManager.SendToAsync(connection.UserId, outbound));
        }

        private async Task HandleMarkAsReadBySenderAsync(
            WebSocketConnectionEntity connection,
            JsonElement root,
            CancellationToken cancellationToken)
        {
            var senderId = GetInt(root, "senderId");
            if (senderId is null || senderId.Value <= 0)
            {
                await SendErrorAsync(connection.ConnectionId, "missing_sender", "'senderId' is required for mark_as_read");
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var messageService = scope.ServiceProvider.GetRequiredService<IMessageService>();

            var conversation = await messageService.GetConversationAsync(senderId.Value, connection.UserId);
            var toMark = conversation
                .Where(m => m.SenderId == senderId.Value
                            && m.ReceiverId == connection.UserId
                            && !m.IsRead)
                .Select(m => m.Id)
                .ToList();

            foreach (var messageId in toMark)
            {
                await messageService.MarkAsReadAsync(messageId, connection.UserId);
            }

            var payload = new
            {
                type = "messages_read",
                readerId = connection.UserId,
                senderId = senderId.Value,
                messageIds = toMark,
                timestamp = DateTime.UtcNow,
            };

            await Task.WhenAll(
                _connectionManager.SendToAsync(senderId.Value, payload),
                _connectionManager.SendToAsync(connection.UserId, payload));
        }

        private async Task HandleMarkAsDeliveredBySenderAsync(
            WebSocketConnectionEntity connection,
            JsonElement root,
            CancellationToken cancellationToken)
        {
            var senderId = GetInt(root, "senderId");
            if (senderId is null || senderId.Value <= 0)
            {
                await SendErrorAsync(connection.ConnectionId, "missing_sender", "'senderId' is required for mark_as_delivered");
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var messageService = scope.ServiceProvider.GetRequiredService<IMessageService>();

            var undelivered = await messageService.GetUndeliveredMessagesAsync(senderId.Value, connection.UserId);
            var messageIds = undelivered.Select(m => m.Id).ToList();

            foreach (var messageId in messageIds)
            {
                await messageService.MarkAsDeliveredAsync(messageId, connection.UserId);
            }

            var payload = new
            {
                type = "messages_delivered",
                receiverId = connection.UserId,
                senderId = senderId.Value,
                messageIds,
                timestamp = DateTime.UtcNow,
            };

            await Task.WhenAll(
                _connectionManager.SendToAsync(senderId.Value, payload),
                _connectionManager.SendToAsync(connection.UserId, payload));
        }

        private async Task HandleMarkMessagesAsDeliveredAsync(
            WebSocketConnectionEntity connection,
            JsonElement root,
            CancellationToken cancellationToken)
        {
            var ids = GetIntArray(root, "messageIds");
            if (ids.Count == 0)
            {
                await SendErrorAsync(
                    connection.ConnectionId,
                    "missing_ids",
                    "'messageIds' must be a non-empty array");
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var messageService = scope.ServiceProvider.GetRequiredService<IMessageService>();

            var updated = new List<int>(ids.Count);
            foreach (var id in ids)
            {
                if (await messageService.MarkAsDeliveredAsync(id, connection.UserId))
                {
                    updated.Add(id);
                }
            }

            var payload = new
            {
                type = "messages_delivered",
                receiverId = connection.UserId,
                messageIds = updated,
                timestamp = DateTime.UtcNow,
            };

            await _connectionManager.SendToAsync(connection.UserId, payload);
        }

        private async Task HandleMarkNotificationsSeenAsync(
            WebSocketConnectionEntity connection,
            JsonElement root,
            CancellationToken cancellationToken)
        {
            var ids = GetIntArray(root, "notificationIds");
            if (ids.Count == 0)
            {
                await SendErrorAsync(
                    connection.ConnectionId,
                    "missing_ids",
                    "'notificationIds' must be a non-empty array");
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationDispatchService>();

            var success = await notificationService.MarkNotificationsAsSeenAsync(ids, connection.UserId);

            var payload = new
            {
                type = "notifications_seen",
                userId = connection.UserId,
                notificationIds = ids,
                success,
                timestamp = DateTime.UtcNow,
            };

            await _connectionManager.SendToAsync(connection.UserId, payload);
        }

        private async Task HandlePresenceUpdateAsync(
            WebSocketConnectionEntity connection,
            JsonElement root,
            CancellationToken cancellationToken)
        {
            var statusRaw = GetString(root, "status");
            if (string.IsNullOrWhiteSpace(statusRaw)
                || !Enum.TryParse<UserPresenceStatus>(statusRaw, ignoreCase: true, out var status))
            {
                await SendErrorAsync(
                    connection.ConnectionId,
                    "invalid_status",
                    $"'status' must be one of: {string.Join(", ", Enum.GetNames(typeof(UserPresenceStatus)))}");
                return;
            }

            var statusMessage = GetString(root, "statusMessage");

            using var scope = _scopeFactory.CreateScope();
            var presence = scope.ServiceProvider.GetRequiredService<IUserPresenceService>();

            var success = await presence.UpdateStatusAsync(connection.UserId, status, statusMessage);

            // Ack to the originator; the presence service itself fans out 'user_status_changed' to observers.
            await _connectionManager.SendToAsync(connection.UserId, new
            {
                type = "status_updated",
                userId = connection.UserId,
                status = status.ToString(),
                statusMessage,
                success,
                timestamp = DateTime.UtcNow,
            });
        }

        private async Task HandleSetAvailabilityAsync(
            WebSocketConnectionEntity connection,
            JsonElement root,
            CancellationToken cancellationToken)
        {
            var isAvailable = GetBool(root, "isAvailable");
            if (isAvailable is null)
            {
                await SendErrorAsync(
                    connection.ConnectionId,
                    "missing_availability",
                    "'isAvailable' is required for set_availability");
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var presence = scope.ServiceProvider.GetRequiredService<IUserPresenceService>();

            var success = await presence.SetAvailabilityForChatAsync(connection.UserId, isAvailable.Value);

            await _connectionManager.SendToAsync(connection.UserId, new
            {
                type = "availability_set",
                userId = connection.UserId,
                isAvailable = isAvailable.Value,
                success,
                timestamp = DateTime.UtcNow,
            });
        }

        // ----- JSON helpers --------------------------------------------------

        private Task SendErrorAsync(string connectionId, string code, string message)
        {
            return _connectionManager.SendToConnectionAsync(connectionId, new
            {
                type = ApiRoutes.WebSocket.MessageTypes.Error,
                code,
                message,
                timestamp = DateTime.UtcNow,
            });
        }

        private static string? GetString(JsonElement root, string propertyName)
        {
            return root.TryGetProperty(propertyName, out var element)
                   && element.ValueKind == JsonValueKind.String
                ? element.GetString()
                : null;
        }

        private static int? GetInt(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out var element)) return null;

            return element.ValueKind switch
            {
                JsonValueKind.Number when element.TryGetInt32(out var value) => value,
                JsonValueKind.String when int.TryParse(element.GetString(), out var parsed) => parsed,
                _ => null,
            };
        }

        private static bool? GetBool(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out var element)) return null;

            return element.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String when bool.TryParse(element.GetString(), out var parsed) => parsed,
                _ => null,
            };
        }

        private static IReadOnlyList<int> GetIntArray(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out var element)
                || element.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<int>();
            }

            var list = new List<int>(element.GetArrayLength());
            foreach (var item in element.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out var value))
                {
                    list.Add(value);
                }
                else if (item.ValueKind == JsonValueKind.String
                         && int.TryParse(item.GetString(), out var parsed))
                {
                    list.Add(parsed);
                }
            }

            return list;
        }
    }
}
