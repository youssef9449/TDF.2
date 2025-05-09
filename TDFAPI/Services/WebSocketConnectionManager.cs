using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TDFShared.Models.Message;

namespace TDFAPI.Services
{
    public class WebSocketConnectionManager
    {
        private readonly ConcurrentDictionary<string, WebSocket> _sockets = new();
        private readonly ConcurrentDictionary<string, WebSocketConnectionEntity> _connections = new();
        private readonly ConcurrentDictionary<int, HashSet<string>> _userConnections = new();
        private readonly ConcurrentDictionary<string, HashSet<string>> _groups = new();
        private readonly ILogger<WebSocketConnectionManager> _logger;

        public WebSocketConnectionManager(ILogger<WebSocketConnectionManager> logger)
        {
            _logger = logger;
        }

        public async Task AddConnectionAsync(WebSocketConnectionEntity connection, WebSocket socket)
        {
            if (_sockets.TryAdd(connection.ConnectionId, socket))
            {
                _connections.TryAdd(connection.ConnectionId, connection);
                
                // Add to user connections collection
                _userConnections.AddOrUpdate(
                    connection.UserId,
                    new HashSet<string> { connection.ConnectionId },
                    (key, existingConnections) =>
                    {
                        existingConnections.Add(connection.ConnectionId);
                        return existingConnections;
                    });
                
                _logger.LogInformation("Connection {ConnectionId} added for user {UserId}", 
                    connection.ConnectionId, connection.UserId);
                
                // Notify about new connection
                await SendToAsync(connection.UserId, new
                {
                    type = "connection_established",
                    connectionId = connection.ConnectionId,
                    userId = connection.UserId,
                    timestamp = DateTime.UtcNow
                });
            }
            else
            {
                _logger.LogWarning("Failed to add connection {ConnectionId} for user {UserId}", 
                    connection.ConnectionId, connection.UserId);
            }
        }

        public async Task RemoveConnectionAsync(string connectionId)
        {
            if (_connections.TryRemove(connectionId, out var connection))
            {
                // Remove from user connections
                if (_userConnections.TryGetValue(connection.UserId, out var userConnections))
                {
                    userConnections.Remove(connectionId);
                    
                    // If this was the last connection for this user, remove the entry
                    if (userConnections.Count == 0)
                    {
                        _userConnections.TryRemove(connection.UserId, out _);
                    }
                }
                
                // Remove from all groups
                foreach (var group in _groups.Values)
                {
                    group.Remove(connectionId);
                }
                
                // Remove socket
                if (_sockets.TryRemove(connectionId, out var socket))
                {
                    try
                    {
                        if (socket.State == WebSocketState.Open)
                        {
                            await socket.CloseAsync(
                                WebSocketCloseStatus.NormalClosure,
                                "Connection closed by the server",
                                CancellationToken.None);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error closing WebSocket for connection {ConnectionId}", connectionId);
                    }
                }
                
                _logger.LogInformation("Connection {ConnectionId} removed for user {UserId}", 
                    connectionId, connection.UserId);
            }
        }

        public WebSocketConnectionEntity GetConnection(string connectionId)
        {
            return _connections.TryGetValue(connectionId, out var connection) ? connection : null;
        }

        public IEnumerable<string> GetAllConnections()
        {
            return _sockets.Keys;
        }

        public IEnumerable<string> GetUserConnections(int userId)
        {
            return _userConnections.TryGetValue(userId, out var connections) 
                ? connections 
                : Enumerable.Empty<string>();
        }

        public async Task AddToGroupAsync(string connectionId, string groupName)
        {
            _groups.AddOrUpdate(
                groupName,
                new HashSet<string> { connectionId },
                (key, existingConnections) =>
                {
                    existingConnections.Add(connectionId);
                    return existingConnections;
                });
            
            _logger.LogDebug("Connection {ConnectionId} added to group {Group}", connectionId, groupName);
            
            // Notify the user they joined the group
            await SendToConnectionAsync(connectionId, new
            {
                type = "group_joined",
                group = groupName,
                timestamp = DateTime.UtcNow
            });
        }

        public async Task RemoveFromGroupAsync(string connectionId, string groupName)
        {
            if (_groups.TryGetValue(groupName, out var connections))
            {
                connections.Remove(connectionId);
                
                // If group is now empty, remove it
                if (connections.Count == 0)
                {
                    _groups.TryRemove(groupName, out _);
                }
                
                _logger.LogDebug("Connection {ConnectionId} removed from group {Group}", connectionId, groupName);
                
                // Notify the user they left the group
                await SendToConnectionAsync(connectionId, new
                {
                    type = "group_left",
                    group = groupName,
                    timestamp = DateTime.UtcNow
                });
            }
        }

        public async Task SendToConnectionAsync(string connectionId, object message)
        {
            if (_sockets.TryGetValue(connectionId, out var socket))
            {
                if (socket.State == WebSocketState.Open)
                {
                    try
                    {
                        var messageJson = JsonSerializer.Serialize(message);
                        var messageBytes = Encoding.UTF8.GetBytes(messageJson);
                        await socket.SendAsync(
                            new ArraySegment<byte>(messageBytes), 
                            WebSocketMessageType.Text, 
                            true, 
                            CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error sending message to connection {ConnectionId}", connectionId);
                        // Consider cleanup if the socket is faulted
                        if (socket.State != WebSocketState.Open)
                        {
                            await RemoveConnectionAsync(connectionId);
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("Cannot send to connection {ConnectionId}: Socket is not open", connectionId);
                    await RemoveConnectionAsync(connectionId);
                }
            }
        }

        public async Task SendToAsync(int userId, object message)
        {
            var userConnectionIds = GetUserConnections(userId).ToList();
            
            if (userConnectionIds.Any())
            {
                var tasks = userConnectionIds.Select(connectionId => SendToConnectionAsync(connectionId, message));
                await Task.WhenAll(tasks);
            }
            else
            {
                _logger.LogDebug("No active connections found for user {UserId}", userId);
            }
        }

        public async Task SendToGroupAsync(string groupName, object message)
        {
            if (_groups.TryGetValue(groupName, out var connections))
            {
                var tasks = connections.Select(connectionId => SendToConnectionAsync(connectionId, message));
                await Task.WhenAll(tasks);
                
                _logger.LogDebug("Message sent to {Count} connections in group {Group}", 
                    connections.Count, groupName);
            }
            else
            {
                _logger.LogDebug("No connections found in group {Group}", groupName);
            }
        }

        public async Task SendToAllAsync(object message, IEnumerable<string> excludedConnections = null)
        {
            var allConnections = _sockets.Keys.ToList();
            var tasks = new List<Task>();
            
            foreach (var connectionId in allConnections)
            {
                if (excludedConnections?.Contains(connectionId) != true)
                {
                    tasks.Add(SendToConnectionAsync(connectionId, message));
                }
            }
            
            await Task.WhenAll(tasks);
            
            _logger.LogDebug("Message sent to {Count} connections", tasks.Count);
        }
    }
}