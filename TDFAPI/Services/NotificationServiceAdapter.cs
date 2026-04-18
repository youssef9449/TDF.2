using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TDFAPI.Services.Realtime;
using TDFShared.DTOs.Messages;
using TDFShared.Models.Message;
using TDFShared.Models.Notification;

namespace TDFAPI.Services
{
    /// <summary>
    /// Server-side implementation of <see cref="TDFShared.Services.INotificationService"/>.
    /// Thin facade over the API's own <see cref="INotificationDispatchService"/>, the
    /// <see cref="WebSocketConnectionManager"/>, and <see cref="IServerWebSocketRouter"/>
    /// — every method on this class maps to a real server behaviour.
    /// </summary>
    public class NotificationServiceAdapter : TDFShared.Services.INotificationService
    {
        private readonly INotificationDispatchService _notificationService;
        private readonly WebSocketConnectionManager _webSocketManager;
        private readonly IServerWebSocketRouter _router;
        private readonly ILogger<NotificationServiceAdapter> _logger;

        /// <summary>Buffer used while reading a single WebSocket frame.</summary>
        private const int ReceiveBufferSize = 4 * 1024;

        public NotificationServiceAdapter(
            INotificationDispatchService notificationService,
            WebSocketConnectionManager webSocketManager,
            IServerWebSocketRouter router,
            ILogger<NotificationServiceAdapter> logger)
        {
            _notificationService = notificationService;
            _webSocketManager = webSocketManager;
            _router = router;
            _logger = logger;
        }

        /// <inheritdoc />
        /// <remarks>
        /// No server-side publisher currently raises this event; it exists purely to
        /// satisfy the shared interface that clients also consume. The compiler warning
        /// about an unused event is expected until a server-side publisher is added.
        /// </remarks>
#pragma warning disable CS0067
        public event EventHandler<NotificationDto>? NotificationReceived;
#pragma warning restore CS0067

        public void Dispose()
        {
            // No owned resources; WebSocketConnectionManager and INotificationDispatchService
            // are owned by the DI container.
        }

        // ----- Notification CRUD (delegated) ---------------------------------

        public async Task<IEnumerable<NotificationEntity>> GetUnreadNotificationsAsync(int? userId = null)
        {
            if (userId is null)
            {
                _logger.LogWarning("GetUnreadNotificationsAsync called without a userId; returning empty result.");
                return Array.Empty<NotificationEntity>();
            }

            return await _notificationService.GetUnreadNotificationsAsync(userId.Value);
        }

        public async Task<bool> MarkAsSeenAsync(int notificationId, int? userId = null)
        {
            if (userId is null)
            {
                _logger.LogWarning("MarkAsSeenAsync called without a userId; ignoring.");
                return false;
            }

            return await _notificationService.MarkAsSeenAsync(notificationId, userId.Value);
        }

        public async Task<bool> MarkNotificationsAsSeenAsync(IEnumerable<int> notificationIds, int? userId = null)
        {
            if (userId is null)
            {
                _logger.LogWarning("MarkNotificationsAsSeenAsync called without a userId; ignoring.");
                return false;
            }

            return await _notificationService.MarkNotificationsAsSeenAsync(notificationIds, userId.Value);
        }

        public Task<bool> CreateNotificationAsync(int receiverId, string message, int? senderId = null)
            => _notificationService.CreateNotificationAsync(receiverId, message, senderId);

        public async Task<bool> BroadcastNotificationAsync(string message, int? senderId = null, string? department = null)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(department))
                {
                    await _notificationService.SendDepartmentNotificationAsync(
                        department!, "Broadcast", message);
                }
                else
                {
                    // No department specified — fan out across every live WebSocket.
                    await _webSocketManager.SendToAllAsync(new
                    {
                        type = "broadcast",
                        senderId,
                        message,
                        timestamp = DateTime.UtcNow
                    });
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting notification");
                return false;
            }
        }

        public async Task<bool> SendNotificationAsync(int receiverId, string message)
        {
            try
            {
                await _notificationService.SendNotificationAsync(
                    receiverId,
                    "Notification",
                    message,
                    TDFShared.Enums.NotificationType.Info);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SendNotificationAsync for user {UserId}", receiverId);
                return false;
            }
        }

        // ----- WebSocket transport (delegated to WebSocketConnectionManager) --

        public Task SendToUserAsync(int userId, object message)
            => _webSocketManager.SendToAsync(userId, message);

        public Task SendToGroupAsync(string group, object message)
            => _webSocketManager.SendToGroupAsync(group, message);

        public Task SendToAllAsync(object message, IEnumerable<string>? excludedConnections = null)
            => _webSocketManager.SendToAllAsync(message, excludedConnections);

        public Task<bool> IsUserOnline(int userId)
            => Task.FromResult(_webSocketManager.GetUserConnections(userId).Any());

        /// <summary>
        /// Registers the connection with <see cref="WebSocketConnectionManager"/>, pumps
        /// frames until the peer disconnects, and dispatches each inbound text frame
        /// through <see cref="IServerWebSocketRouter"/>.
        /// </summary>
        public async Task HandleUserConnectionAsync(WebSocketConnectionEntity connection, WebSocket socket)
        {
            if (connection is null) throw new ArgumentNullException(nameof(connection));
            if (socket is null) throw new ArgumentNullException(nameof(socket));

            await _webSocketManager.AddConnectionAsync(connection, socket);

            var buffer = new byte[ReceiveBufferSize];
            try
            {
                while (socket.State == WebSocketState.Open)
                {
                    var received = await ReceiveFullMessageAsync(socket, buffer, CancellationToken.None);

                    if (received is null)
                    {
                        // Peer initiated close.
                        break;
                    }

                    if (received.Value.MessageType == WebSocketMessageType.Close)
                    {
                        await socket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "Client requested close",
                            CancellationToken.None);
                        break;
                    }

                    if (received.Value.MessageType != WebSocketMessageType.Text)
                    {
                        _logger.LogDebug(
                            "Ignoring non-text frame ({MessageType}, {Length} bytes) on connection {ConnectionId}",
                            received.Value.MessageType,
                            received.Value.Payload.Count,
                            connection.ConnectionId);
                        continue;
                    }

                    var json = Encoding.UTF8.GetString(
                        received.Value.Payload.Array!,
                        received.Value.Payload.Offset,
                        received.Value.Payload.Count);

                    await _router.RouteAsync(connection, json, CancellationToken.None);
                }
            }
            catch (WebSocketException ex)
            {
                _logger.LogInformation(ex,
                    "WebSocket {ConnectionId} for user {UserId} ended: {Error}",
                    connection.ConnectionId, connection.UserId, ex.Message);
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown or client abort.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unhandled error while pumping WebSocket {ConnectionId} for user {UserId}",
                    connection.ConnectionId, connection.UserId);
            }
            finally
            {
                await _webSocketManager.RemoveConnectionAsync(connection.ConnectionId);
            }
        }

        /// <summary>
        /// Reads one logical WebSocket message, re-assembling it across continuation frames.
        /// Returns <c>null</c> when the peer has closed the connection.
        /// </summary>
        private static async Task<(WebSocketMessageType MessageType, ArraySegment<byte> Payload)?> ReceiveFullMessageAsync(
            WebSocket socket, byte[] buffer, CancellationToken cancellationToken)
        {
            using var ms = new System.IO.MemoryStream();
            WebSocketReceiveResult result;

            do
            {
                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return (WebSocketMessageType.Close, new ArraySegment<byte>(Array.Empty<byte>()));
                }

                if (result.Count > 0)
                {
                    ms.Write(buffer, 0, result.Count);
                }
            }
            while (!result.EndOfMessage);

            if (result.CloseStatus.HasValue)
            {
                return null;
            }

            return (result.MessageType, new ArraySegment<byte>(ms.ToArray()));
        }
    }
}
