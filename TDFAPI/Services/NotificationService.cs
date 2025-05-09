using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TDFShared.DTOs.Messages;
using TDFAPI.Repositories;
using TDFAPI.Messaging;
using TDFShared.Models.Message;
using TDFShared.Enums;
using TDFShared.Models.Notification;
using TDFAPI.Messaging.Interfaces;

namespace TDFAPI.Services
{
    public class NotificationService : INotificationService
    {
        private readonly INotificationRepository _notificationRepository;
        private readonly IUserRepository _userRepository;
        private readonly WebSocketConnectionManager _webSocketManager;
        private readonly ILogger<NotificationService> _logger;
        private readonly IMessageRepository _messageRepository;
        private readonly IServiceProvider _serviceProvider;
        private readonly IEventMediator _eventMediator;
        private readonly MessageStore _messageStore;
        private readonly ConcurrentDictionary<int, (DateTime LastMessage, int MessageCount)> _messageRateLimits = 
            new ConcurrentDictionary<int, (DateTime LastMessage, int MessageCount)>();
        private readonly ConcurrentDictionary<string, DateTime> _lastMessageTime = new ConcurrentDictionary<string, DateTime>();

        // Dictionary cleanup timer
        private readonly Timer _dictionaryCleanupTimer;

        // Configuration for WebSocket behavior
        private const int DefaultBufferSize = 4096;  // 4KB default
        private const int MaxBufferSize = 65536;     // 64KB max
        private const int HeartbeatIntervalSeconds = 30; // Heartbeat interval
        private const int ConnectionTimeoutSeconds = 120; // Connection timeout

        // Rate limiting parameters
        private const int MaxMessagesPerMinute = 60;
        private const int DictionaryCleanupIntervalMinutes = 10; // How often to clean up dictionaries

        public NotificationService(
            INotificationRepository notificationRepository,
            IUserRepository userRepository,
            WebSocketConnectionManager webSocketManager,
            IMessageRepository messageRepository,
            MessageStore messageStore,
            ILogger<NotificationService> logger,
            IServiceProvider serviceProvider,
            IEventMediator eventMediator)
        {
            _notificationRepository = notificationRepository;
            _userRepository = userRepository;
            _webSocketManager = webSocketManager;
            _messageRepository = messageRepository;
            _messageStore = messageStore;
            _logger = logger;
            _serviceProvider = serviceProvider;
            _eventMediator = eventMediator;
            
            // Initialize the dictionary cleanup timer
            _dictionaryCleanupTimer = new Timer(CleanupDictionaries, null, 
                TimeSpan.FromMinutes(DictionaryCleanupIntervalMinutes), 
                TimeSpan.FromMinutes(DictionaryCleanupIntervalMinutes));

            // Subscribe to user status and availability events
            _eventMediator.Subscribe<Messaging.UserStatusChangedEvent>(HandleUserStatusChanged);
            _eventMediator.Subscribe<Messaging.UserAvailabilityChangedEvent>(HandleUserAvailabilityChanged);
        }

        /// <summary>
        /// Handles the user status changed event
        /// </summary>
        private void HandleUserStatusChanged(Messaging.UserStatusChangedEvent eventData)
        {
            try
            {
                // Fire and forget - we don't want to block the event processing
                _ = SendToAllAsync(new
                {
                    type = "user_status_changed",
                    userId = eventData.UserId,
                    username = eventData.Username,
                    fullName = eventData.FullName,
                    status = eventData.Status.ToString(),
                    statusMessage = eventData.StatusMessage,
                    timestamp = eventData.Timestamp
                });
                
                _logger.LogInformation("Broadcasting status change for user {UserId} to {Status}", 
                    eventData.UserId, eventData.Status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling user status changed event");
            }
        }
        
        /// <summary>
        /// Handles the user availability changed event
        /// </summary>
        private void HandleUserAvailabilityChanged(Messaging.UserAvailabilityChangedEvent eventData)
        {
            try
            {
                // Fire and forget - we don't want to block the event processing
                _ = SendToAllAsync(new
                {
                    type = "user_availability_changed",
                    userId = eventData.UserId,
                    username = eventData.Username,
                    fullName = eventData.FullName,
                    isAvailableForChat = eventData.IsAvailableForChat,
                    timestamp = eventData.Timestamp
                });
                
                _logger.LogInformation("Broadcasting availability change for user {UserId} to {IsAvailable}", 
                    eventData.UserId, eventData.IsAvailableForChat);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling user availability changed event");
            }
        }

        // Method to periodically clean up dictionaries to prevent memory leaks
        private void CleanupDictionaries(object? state)
        {
            try
            {
                var cutoffTime = DateTime.UtcNow.AddHours(-1); // Remove entries older than 1 hour
                
                // Clean up rate limiting dictionary
                foreach (var userId in _messageRateLimits.Keys)
                {
                    if (_messageRateLimits.TryGetValue(userId, out var info) && 
                        info.LastMessage < cutoffTime)
                    {
                        _messageRateLimits.TryRemove(userId, out _);
                        _logger.LogDebug("Removed stale rate limit entry for user {UserId}", userId);
                    }
                }
                
                // Clean up message time dictionary
                foreach (var connectionId in _lastMessageTime.Keys)
                {
                    if (_lastMessageTime.TryGetValue(connectionId, out var time) && 
                        time < cutoffTime)
                    {
                        _lastMessageTime.TryRemove(connectionId, out _);
                        _logger.LogDebug("Removed stale message time entry for connection {ConnectionId}", connectionId);
                    }
                }
                
                _logger.LogDebug("Dictionary cleanup completed. Rate limits: {RateLimitCount}, Message times: {MessageTimeCount}", 
                    _messageRateLimits.Count, _lastMessageTime.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during dictionary cleanup");
            }
        }

        // Add resource cleanup for proper disposal
        public void Dispose()
        {
            _dictionaryCleanupTimer?.Dispose();
        }

        public async Task<IEnumerable<NotificationDto>> GetUnreadNotificationsAsync(int userId)
        {
            // Fetch entities from repository
            var notificationEntities = await _notificationRepository.GetUnreadNotificationsAsync(userId);
            
            // Map entities to DTOs
            return notificationEntities.Select(entity => new NotificationDto
            {
                NotificationID = entity.NotificationID,
                ReceiverID = entity.ReceiverID,
                SenderID = entity.SenderID,
                MessageID = entity.MessageID,
                IsSeen = entity.IsSeen,
                Timestamp = entity.Timestamp,
                Message = entity.Message // Assuming entity has a Message property
            });
        }

        public async Task<bool> MarkAsSeenAsync(int notificationId, int userId)
        {
            try
            {
                var result = await _notificationRepository.MarkNotificationAsSeenAsync(notificationId, userId);
                if (result)
                {
                    await SendToUserAsync(userId, new 
                    { 
                        type = "notification_seen",
                        notificationId = notificationId,
                        timestamp = DateTime.UtcNow
                    });
                }
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking notification {NotificationId} as seen for user {UserId}", 
                    notificationId, userId);
                return false;
            }
        }

        public async Task<bool> MarkNotificationsAsSeenAsync(int userId, IEnumerable<int> notificationIds)
        {
            try
            {
                if (notificationIds == null || !notificationIds.Any())
                {
                    return true;
                }
                
                // Create a list of tasks to mark each notification as seen
                var tasks = notificationIds.Select(id => 
                    _notificationRepository.MarkNotificationAsSeenAsync(id, userId)).ToList();
                
                // Wait for all operations to complete
                var results = await Task.WhenAll(tasks);
                
                // Check if all operations were successful
                bool allSuccessful = results.All(r => r);
                
                if (allSuccessful)
                {
                    // Send a single notification for all the IDs that were marked as seen
                    await SendToUserAsync(userId, new 
                    { 
                        type = "notifications_seen",
                        notificationIds = notificationIds.ToList(),
                        timestamp = DateTime.UtcNow
                    });
                }
                
                return allSuccessful;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking multiple notifications as seen for user {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> CreateNotificationAsync(int receiverId, string message)
        {
            var notification = new NotificationEntity
            {
                ReceiverID = receiverId,
                MessageText = message,
                Timestamp = DateTime.UtcNow,
                IsSeen = false
            };

            var id = await _notificationRepository.CreateNotificationAsync(notification);
            if (id > 0)
            {
                notification.NotificationID = id;
                
                // Send real-time notification via WebSocket if user is online
                if (await _messageRepository.IsUserOnlineAsync(receiverId))
                {
                    await SendToUserAsync(receiverId, new 
                    { 
                        type = "new_notification",
                        notification = notification,
                    });
                }
                
                // Track delivery status
                var userDevices = await _messageRepository.GetUserDevicesAsync(receiverId);
                if (!userDevices.Any())
                {
                    // Store for delivery when user connects
                    await _messageRepository.AddNotificationAsync(receiverId, message);
                }

                return true;
            }

            return false;
        }

        public async Task<bool> BroadcastNotificationAsync(string message, int senderId, string? department = null)
        {
            try
            {
                // Get all users in a single database call
                var allUsers = await _userRepository.GetAllAsync();
                var sender = allUsers.FirstOrDefault(u => u.UserID == senderId);
                
                if (sender == null)
                {
                    _logger.LogWarning("Cannot broadcast notification: Sender with ID {SenderId} not found", senderId);
                    return false;
                }
                
                // Filter users based on department and exclude sender
                var targetUsers = allUsers
                    .Where(u => u.UserID != senderId && 
                               (string.IsNullOrEmpty(department) || 
                                u.Department?.Equals(department, StringComparison.OrdinalIgnoreCase) == true))
                    .ToList();
                
                if (!targetUsers.Any())
                {
                    _logger.LogInformation("No recipients found for broadcast notification from user {SenderId} to department {Department}", 
                        senderId, department ?? "all");
                    return true; // No error, just no recipients
                }
                
                // Get online users in a single call
                var onlineUsers = await _messageRepository.GetOnlineUsersAsync();
                var onlineTargetUsers = targetUsers.Where(u => onlineUsers.Contains(u.UserID)).ToList();
                
                // Process notifications in batches if needed
                var tasks = new List<Task>();
                
                // Create persistent notifications for all recipients
                foreach (var user in targetUsers)
                {
                    tasks.Add(CreateNotificationAsync(user.UserID, message));
                }
                
                // Prepare real-time message once
                var broadcastMessage = new 
                { 
                    type = "broadcast_notification",
                    message = message,
                    sender = new {
                        id = sender.UserID,
                        name = sender.FullName,
                        department = sender.Department,
                        role = sender.Role
                    },
                    department = department,
                    timestamp = DateTime.UtcNow
                };
                
                // Group users by department for more efficient group messaging
                var departmentGroups = onlineTargetUsers
                    .Where(u => !string.IsNullOrEmpty(u.Department))
                    .GroupBy(u => u.Department);
                
                foreach (var deptGroup in departmentGroups)
                {
                    if (!string.IsNullOrEmpty(deptGroup.Key))
                    {
                        tasks.Add(SendToGroupAsync(deptGroup.Key, broadcastMessage));
                    }
                }
                
                // Wait for all tasks to complete
                await Task.WhenAll(tasks);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting notification from user {SenderId} to department {Department}", 
                    senderId, department ?? "all");
                return false;
            }
        }

        public async Task<bool> HandleUserConnectionAsync(int userId, bool isConnected, string? machineName = null)
        {
            try
            {
                if (userId <= 0)
                {
                    _logger.LogWarning("Invalid userId: {UserId}", userId);
                    return false;
                }
                
                // Get user info first to validate user exists
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("Cannot handle connection: User with ID {UserId} not found", userId);
                    return false;
                }
                
                // Update user connection status after validating user exists
                bool updateResult = await _messageRepository.UpdateUserConnectionStatusAsync(userId, isConnected, machineName);
                if (!updateResult)
                {
                    _logger.LogWarning("Failed to update connection status for user {UserId}", userId);
                    return false;
                }
                
                // Tasks to run in parallel
                var tasks = new List<Task>();
                
                // Prepare status message
                var statusMessage = new 
                {
                    type = "user_connection_status",
                    userId = userId,
                    userName = user.FullName,
                    isConnected = isConnected,
                    department = user.Department,
                    timestamp = DateTime.UtcNow
                };
                
                // Send to department if specified
                if (!string.IsNullOrEmpty(user.Department))
                {
                    tasks.Add(SendToGroupAsync(user.Department, statusMessage));
                }
                
                // If user is connecting, deliver any pending notifications
                if (isConnected)
                {
                    tasks.Add(DeliverPendingNotificationsAsync(userId));
                }
                
                // Wait for all notification tasks to complete with timeout protection
                if (tasks.Any())
                {
                    try 
                    {
                        await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(10));
                    }
                    catch (TimeoutException)
                    {
                        _logger.LogWarning("Timed out while processing connection tasks for user {UserId}", userId);
                        // Continue anyway since the core connection update was successful
                    }
                }
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling connection status for user {UserId}", userId);
                return false;
            }
        }
        
        // Helper method to deliver pending notifications
        private async Task DeliverPendingNotificationsAsync(int userId)
        {
            try
            {
                var pendingMessages = await _messageRepository.GetPendingMessagesAsync(userId);
                if (!pendingMessages.Any()) return;
                
                var batchSize = 10; // Process in small batches to avoid overwhelming the client
                
                for (int i = 0; i < pendingMessages.Count(); i += batchSize)
                {
                    var batch = pendingMessages.Skip(i).Take(batchSize);
                    var batchTasks = batch.Select(message => 
                        SendToUserAsync(userId, new 
                        {
                            type = "pending_notification",
                            message = message,
                            timestamp = DateTime.UtcNow
                        }));
                    
                    await Task.WhenAll(batchTasks);
                    
                    // Small delay between batches
                    if (i + batchSize < pendingMessages.Count())
                        await Task.Delay(100);
                }
                
                _logger.LogInformation("Delivered {Count} pending notifications to user {UserId}", 
                    pendingMessages.Count(), userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error delivering pending notifications to user {UserId}", userId);
                // No rethrow - we don't want to fail the whole connection process
            }
        }

        public async Task SendToUserAsync(int userId, object message)
        {
            // Get all connections for this user
            var userConnections = _webSocketManager.GetUserConnections(userId).ToList();
            
            if (userConnections.Any())
            {
                // If there are managed connections, send to each
                foreach (var connectionId in userConnections)
                {
                    await _webSocketManager.SendToConnectionAsync(connectionId, message);
                }
            }
            else
            {
                // If no managed connections, try direct socket approach
                await _webSocketManager.SendToAsync(userId, message);
            }
        }

        public async Task SendToGroupAsync(string group, object message)
        {
            // Use WebSocketConnectionManager's method directly
            await _webSocketManager.SendToGroupAsync(group, message);
        }

        public async Task SendToAllAsync(object message, IEnumerable<string>? excludedConnections = null)
        {
            var allConnections = _webSocketManager.GetAllConnections();
            foreach (var connectionId in allConnections)
            {
                if (excludedConnections?.Contains(connectionId) != true)
                {
                    await _webSocketManager.SendToConnectionAsync(connectionId, message);
                }
            }
        }

        public async Task HandleUserConnectionAsync(WebSocketConnectionEntity connection, WebSocket socket)
        {
            // Track last activity time for timeout detection
            var lastActivity = DateTime.UtcNow;
            
            // Get buffer size from configuration or use default
            int bufferSize = DefaultBufferSize;
            try
            {
                // Try to get buffer size from configuration
                if (_serviceProvider.GetService<IConfiguration>() is IConfiguration config)
                {
                    bufferSize = config.GetValue<int>("WebSockets:BufferSize", DefaultBufferSize);
                    
                    // Ensure buffer size doesn't exceed maximum
                    bufferSize = Math.Min(bufferSize, MaxBufferSize);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get WebSocket buffer size from configuration. Using default.");
            }
            
            await _webSocketManager.AddConnectionAsync(connection, socket);

            // Update the user's device information in the database
            if (connection.UserId > 0)
            {
                var userRepo = _serviceProvider.GetRequiredService<IUserRepository>();
                string deviceInfo = $"WebSocket Client";
                
                try
                {
                    // Update the user's device information
                    await userRepo.UpdateCurrentDeviceAsync(
                        connection.UserId, 
                        deviceInfo, 
                        connection.MachineName ?? "Unknown"
                    );
                    
                    _logger.LogInformation("Updated device info for user {UserId}: {Device} on {Machine}", 
                        connection.UserId, deviceInfo, connection.MachineName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to update device info for user {UserId}", connection.UserId);
                }
                
                // Start heartbeat for this connection
                _ = StartHeartbeatAsync(connection, socket);
            }

            var buffer = new byte[bufferSize];
            var cancellationTokenSource = new CancellationTokenSource();
            
            // Start a background task to monitor connection timeout
            _ = MonitorConnectionTimeoutAsync(connection, socket, cancellationTokenSource, lastActivity);
            
            try
            {
                while (socket.State == WebSocketState.Open)
                {
                    var result = await socket.ReceiveAsync(
                        new ArraySegment<byte>(buffer), 
                        cancellationTokenSource.Token);

                    // Update last activity timestamp
                    lastActivity = DateTime.UtcNow;

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await HandleDisconnectionAsync(connection.ConnectionId);
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        // Process message if needed
                        await HandleWebSocketMessageAsync(connection, message);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Connection timed out, close it
                _logger.LogWarning("WebSocket connection {ConnectionId} for user {UserId} timed out", 
                    connection.ConnectionId, connection.UserId);
                await HandleDisconnectionAsync(connection.ConnectionId);
            }
            catch (WebSocketException wsEx)
            {
                // WebSocket connection error
                _logger.LogWarning(wsEx, "WebSocket error for user {UserId}: {Error}", 
                    connection.UserId, wsEx.Message);
                await HandleDisconnectionAsync(connection.ConnectionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling WebSocket connection for user {UserId}", connection.UserId);
                await HandleDisconnectionAsync(connection.ConnectionId);
            }
            finally
            {
                // Clean up resources
                cancellationTokenSource.Dispose();
            }
        }

        private async Task HandleDisconnectionAsync(string connectionId)
        {
            var connection = _webSocketManager.GetConnection(connectionId);
            if (connection != null)
            {
                // Run these tasks in parallel for better performance
                var disconnectionTasks = new List<Task>
                {
                    // Update database status
                    HandleUserConnectionAsync(connection.UserId, false),
                    
                    // Notify others about disconnection
                    SendToAllAsync(new
                    {
                        type = "UserDisconnected",
                        userId = connection.UserId,
                        username = connection.Username,
                        timestamp = DateTime.UtcNow
                    }, new[] { connectionId })
                };
                
                // Execute all tasks first, before removing the connection
                await Task.WhenAll(disconnectionTasks);
                
                // Finally remove the connection
                await _webSocketManager.RemoveConnectionAsync(connectionId);
                
                _logger.LogInformation("WebSocket connection {ConnectionId} for user {UserId} disconnected successfully", 
                    connectionId, connection.UserId);
            }
        }

        public async Task HandleWebSocketMessageAsync(WebSocketConnectionEntity connection, string messageJson)
        {
            try
            {
                // Apply rate limiting before processing the message
                if (!await CheckRateLimitAsync(connection))
                {
                    // Rate limit exceeded, message processing skipped
                    return;
                }
                
                // Try parsing the message as a WebSocketMessage first (for delivery guarantees)
                WebSocketMessage webSocketMessage = null;
                try 
                {
                    webSocketMessage = JsonSerializer.Deserialize<WebSocketMessage>(messageJson);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("Not a WebSocketMessage format: {Error}", ex.Message);
                    // Not a WebSocketMessage, continue with normal processing
                }
                
                // Enhanced handling for message acknowledgments and receipts
                if (webSocketMessage != null)
                {
                    string msgType = webSocketMessage.Type?.ToLower();
                    
                    // Handle message acknowledgments (delivery confirmation)
                    if (msgType == "ack" || msgType == "message_ack")
                    {
                        // This is an acknowledgment for a message
                        var acknowledgedMessageId = webSocketMessage.Content;
                        _messageStore.MarkAsDelivered(acknowledgedMessageId);
                        
                        // Check if we have a JSON array or a single ID
                        if (acknowledgedMessageId.StartsWith("[") && acknowledgedMessageId.EndsWith("]"))
                        {
                            try
                            {
                                // Parse as a JSON array of IDs
                                var messageIds = JsonSerializer.Deserialize<List<int>>(acknowledgedMessageId);
                                if (messageIds != null && messageIds.Count > 0)
                                {
                                    // Update the status of all messages
                                    foreach (var msgId in messageIds)
                                    {
                                        await _messageRepository.MarkMessageAsDeliveredAsync(msgId, connection.UserId);
                                    }
                                    
                                    _logger.LogInformation("Updated status of {Count} messages to Delivered for user {UserId}", 
                                        messageIds.Count, connection.UserId);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error processing message acknowledgment array from user {UserId}", connection.UserId);
                            }
                        }
                        else if (int.TryParse(acknowledgedMessageId, out int messageId))
                        {
                            // Single message ID
                            await _messageRepository.MarkMessageAsDeliveredAsync(messageId, connection.UserId);
                            _logger.LogDebug("Marked message {MessageId} as delivered from user {UserId}", 
                                messageId, connection.UserId);
                        }
                        
                        // No further processing needed for acknowledgments
                        return;
                    }
                    
                    // Handle message receipts (read confirmations)
                    if (msgType == "receipt" || msgType == "message_receipt")
                    {
                        // Check for content format: could be a message ID or an array of IDs
                        if (webSocketMessage.Content.StartsWith("[") && webSocketMessage.Content.EndsWith("]"))
                        {
                            try
                            {
                                // Parse as a JSON array of IDs
                                var messageIds = JsonSerializer.Deserialize<List<int>>(webSocketMessage.Content);
                                if (messageIds != null && messageIds.Count > 0)
                                {
                                    // Update the status of all messages to Read
                                    foreach (var msgId in messageIds)
                                    {
                                        await _messageRepository.MarkMessageAsReadAsync(msgId, connection.UserId);
                                    }
                                    
                                    _logger.LogInformation("Updated status of {Count} messages to Read for user {UserId}", 
                                        messageIds.Count, connection.UserId);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error processing message receipt array from user {UserId}", connection.UserId);
                            }
                        }
                        // Check custom format: status:messageId
                        else if (webSocketMessage.Content.Contains(':'))
                        {
                            var parts = webSocketMessage.Content.Split(':');
                            if (parts.Length == 2)
                            {
                                var statusStr = parts[0]; // delivered, read, etc.
                                var messageIdStr = parts[1];
                                
                                if (Enum.TryParse<MessageStatus>(statusStr, true, out var status) && 
                                    int.TryParse(messageIdStr, out int parsedMessageId))
                                {
                                    _logger.LogDebug("Received receipt ({Status}) for message {MessageId} from user {UserId}", 
                                        status, parsedMessageId, connection.UserId);
                                    
                                    // Update message status in the database
                                    switch (status)
                                    {
                                        case MessageStatus.Delivered:
                                            await _messageRepository.MarkMessageAsDeliveredAsync(parsedMessageId, connection.UserId);
                                            break;
                                        case MessageStatus.Read:
                                            await _messageRepository.MarkMessageAsReadAsync(parsedMessageId, connection.UserId);
                                            break;
                                    }
                                }
                            }
                        }
                        // Simple message ID for read status
                        else if (int.TryParse(webSocketMessage.Content, out int messageId))
                        {
                            await _messageRepository.MarkMessageAsReadAsync(messageId, connection.UserId);
                            _logger.LogDebug("Marked message {MessageId} as read from user {UserId}", 
                                messageId, connection.UserId);
                        }
                        
                        // No further processing needed for receipts
                        return;
                    }
                }
                
                // Continue with existing message processing...
                var message = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(messageJson);
                if (message == null)
                {
                    _logger.LogWarning("Received invalid JSON message from user {UserId}", connection.UserId);
                    return;
                }
                
                if (!message.TryGetValue("type", out var typeElement) || typeElement.GetString() is not string messageType)
                {
                    await _webSocketManager.SendToConnectionAsync(connection.ConnectionId, new ErrorMessageDTO 
                    { 
                        Message = "Invalid message format: 'type' field is required",
                    });
                    return;
                }
                
                // Log incoming message type for auditing and debugging
                _logger.LogDebug("WebSocket message of type '{MessageType}' received from user {UserId}", 
                    messageType, connection.UserId);
                
                switch (messageType.ToLower())
                {
                    case "ping":
                        // Simple ping-pong for connection testing
                        await _webSocketManager.SendToConnectionAsync(connection.ConnectionId, new { 
                            type = "pong",
                            timestamp = DateTime.UtcNow
                        });
                        break;

                    case "join_group":
                        if (message.TryGetValue("group", out var groupElement) && groupElement.GetString() is string group)
                        {
                            await _webSocketManager.AddToGroupAsync(connection.ConnectionId, group);
                            
                            // Log join event
                            _logger.LogInformation("User {UserId} joined group {Group}", connection.UserId, group);
                            
                            // Acknowledge the join
                            await _webSocketManager.SendToConnectionAsync(connection.ConnectionId, new { 
                                type = "group_joined", 
                                group = group,
                                timestamp = DateTime.UtcNow
                            });
                        }
                        else
                        {
                            await _webSocketManager.SendToConnectionAsync(connection.ConnectionId, new { 
                                type = "error", 
                                message = "Missing 'group' field in join_group request",
                                timestamp = DateTime.UtcNow
                            });
                        }
                        break;

                    case "leave_group":
                        if (message.TryGetValue("group", out var leaveGroupElement) && leaveGroupElement.GetString() is string leaveGroup)
                        {
                            await _webSocketManager.RemoveFromGroupAsync(connection.ConnectionId, leaveGroup);
                            
                            // Log leave event
                            _logger.LogInformation("User {UserId} left group {Group}", connection.UserId, leaveGroup);
                            
                            // Acknowledge the leave
                            await _webSocketManager.SendToConnectionAsync(connection.ConnectionId, new { 
                                type = "group_left", 
                                group = leaveGroup,
                                timestamp = DateTime.UtcNow
                            });
                        }
                        else
                        {
                            await _webSocketManager.SendToConnectionAsync(connection.ConnectionId, new { 
                                type = "error", 
                                message = "Missing 'group' field in leave_group request",
                                timestamp = DateTime.UtcNow
                            });
                        }
                        break;
                    
                    case "update_presence":
                        // Update presence status
                        var presenceStatus = UserPresenceStatus.Online;

                        try
                        {
                            if (message.TryGetValue("status", out var statusObj) && statusObj.ValueKind != JsonValueKind.Null)
                            {
                                if (Enum.TryParse<UserPresenceStatus>(statusObj.GetString(), out var status))
                                {
                                    presenceStatus = status;
                                }
                            }
                        }
                        catch (Exception)
                        {
                            // Default to Online if parsing fails
                        }

                        string statusMessage = null;
                        if (message.TryGetValue("statusMessage", out var msgObj) && msgObj.ValueKind != JsonValueKind.Null)
                        {
                            statusMessage = msgObj.GetString();
                        }

                        // Update user status
                        await _eventMediator.PublishAsync(new Messaging.UserStatusChangedEvent(connection.UserId, presenceStatus, statusMessage));
                        break;
                        
                    case "set_chat_availability":
                        if (message.TryGetValue("isAvailable", out var availableElement) && 
                            (availableElement.ValueKind == JsonValueKind.True || 
                             availableElement.ValueKind == JsonValueKind.False))
                        {
                            bool isAvailable = availableElement.GetBoolean();
                            await _eventMediator.PublishAsync(new Messaging.UserAvailabilityChangedEvent(connection.UserId, isAvailable));
                            
                            await _webSocketManager.SendToConnectionAsync(connection.ConnectionId, new { 
                                type = "chat_availability_updated", 
                                isAvailable = isAvailable,
                                timestamp = DateTime.UtcNow
                            });
                        }
                        break;
                        
                    case "activity_ping":
                        await _eventMediator.PublishAsync(new Messaging.UserActivityPingEvent(connection.UserId));
                        break;
                    
                    case "update_status":
                        await HandleUpdateStatusAsync(connection, message);
                        break;
                    case "set_availability":
                        await HandleSetAvailabilityAsync(connection, message);
                        break;
                    
                    // Handle unknown message types
                    default:
                        _logger.LogWarning("Received unknown message type '{MessageType}' from user {UserId}", 
                            messageType, connection.UserId);
                        await _webSocketManager.SendToConnectionAsync(connection.ConnectionId, new { 
                            type = "error", 
                            message = $"Unknown message type: {messageType}",
                            timestamp = DateTime.UtcNow
                        });
                        break;
                }
            }
            catch (JsonException jsonEx)
            {
                _logger.LogWarning(jsonEx, "Error parsing WebSocket message from user {UserId}: {Message}", 
                    connection.UserId, jsonEx.Message);
                
                await _webSocketManager.SendToConnectionAsync(connection.ConnectionId, new { 
                    type = "error", 
                    message = "Invalid JSON format", 
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling WebSocket message from user {UserId}: {Message}", 
                    connection.UserId, ex.Message);
            }
        }

        private async Task HandleUpdateStatusAsync(WebSocketConnectionEntity connection, Dictionary<string, JsonElement> message)
        {
            try
            {
                UserPresenceStatus presenceStatus = UserPresenceStatus.Online; // Default
                string? statusMessage = null;

                if (message.TryGetValue("status", out var statusElement) && statusElement.ValueKind == JsonValueKind.String)
                {
                    if (!Enum.TryParse<UserPresenceStatus>(statusElement.GetString(), true, out presenceStatus))
                    {
                        _logger.LogWarning("Invalid presence status received from user {UserId}: {Status}", connection.UserId, statusElement.GetString());
                        // Optionally send an error back to the client
                        await _webSocketManager.SendToConnectionAsync(connection.ConnectionId, new { 
                            type = "error", 
                            message = $"Invalid status value: {statusElement.GetString()}",
                            timestamp = DateTime.UtcNow
                        });
                        return; // Stop processing if status is invalid
                    }
                }
                else
                {
                     _logger.LogWarning("Missing 'status' field in update_status message from user {UserId}", connection.UserId);
                     // Optionally send an error back
                     await _webSocketManager.SendToConnectionAsync(connection.ConnectionId, new { type = "error", message = "Missing 'status' field."});
                     return;
                }

                if (message.TryGetValue("statusMessage", out var msgElement) && msgElement.ValueKind == JsonValueKind.String)
                {
                    statusMessage = msgElement.GetString();
                }

                // Call the presence service to update the status
                await _eventMediator.PublishAsync(new Messaging.UserStatusChangedEvent(connection.UserId, presenceStatus, statusMessage));
                _logger.LogDebug("Handled update_status for user {UserId} to {Status} ({StatusMessage})", connection.UserId, presenceStatus, statusMessage ?? "null");

                // Optionally send confirmation back
                // await _webSocketManager.SendToConnectionAsync(connection.ConnectionId, new { type = "status_updated", status = presenceStatus.ToString(), statusMessage = statusMessage });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling update_status for user {UserId}", connection.UserId);
                // Optionally notify the client about the error
                await _webSocketManager.SendToConnectionAsync(connection.ConnectionId, new { type = "error", message = "Failed to update status." });
            }
        }

        private async Task HandleSetAvailabilityAsync(WebSocketConnectionEntity connection, Dictionary<string, JsonElement> message)
        {
            try
            {
                bool isAvailable;

                if (message.TryGetValue("isAvailable", out var availableElement) &&
                    (availableElement.ValueKind == JsonValueKind.True || availableElement.ValueKind == JsonValueKind.False))
                {
                    isAvailable = availableElement.GetBoolean();
                }
                else
                {
                    _logger.LogWarning("Missing or invalid 'isAvailable' field in set_availability message from user {UserId}", connection.UserId);
                    await _webSocketManager.SendToConnectionAsync(connection.ConnectionId, new { 
                        type = "error", 
                        message = "Missing or invalid 'isAvailable' boolean field.",
                        timestamp = DateTime.UtcNow
                    });
                    return; // Stop processing
                }

                // Call the presence service
                await _eventMediator.PublishAsync(new Messaging.UserAvailabilityChangedEvent(connection.UserId, isAvailable));
                 _logger.LogDebug("Handled set_availability for user {UserId} to {IsAvailable}", connection.UserId, isAvailable);


                // Send confirmation back to the user
                await _webSocketManager.SendToConnectionAsync(connection.ConnectionId, new { 
                    type = "availability_set", 
                    isAvailable = isAvailable,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling set_availability for user {UserId}", connection.UserId);
                 // Optionally notify the client about the error
                await _webSocketManager.SendToConnectionAsync(connection.ConnectionId, new { type = "error", message = "Failed to set availability." });
            }
        }

        // Monitor connection for timeout
        private async Task MonitorConnectionTimeoutAsync(WebSocketConnectionEntity connection, WebSocket socket, 
                                        CancellationTokenSource cancellationTokenSource, DateTime lastActivity)
        {
            try
            {
                while (socket.State == WebSocketState.Open)
                {
                    // Check if connection has timed out
                    var inactiveTime = DateTime.UtcNow - lastActivity;
                    if (inactiveTime.TotalSeconds > ConnectionTimeoutSeconds)
                    {
                        _logger.LogWarning("WebSocket connection {ConnectionId} for user {UserId} inactive for {Seconds}s, timing out", 
                            connection.ConnectionId, connection.UserId, inactiveTime.TotalSeconds);
                        
                        // Cancel any pending receive operations
                        cancellationTokenSource.Cancel();
                        return;
                    }
                    
                    // Sleep and check again
                    await Task.Delay(TimeSpan.FromSeconds(HeartbeatIntervalSeconds / 2));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error monitoring connection timeout for user {UserId}", connection.UserId);
            }
        }
        
        // Send periodic heartbeats to keep the connection alive
        private async Task StartHeartbeatAsync(WebSocketConnectionEntity connection, WebSocket socket)
        {
            try
            {
                // Keep sending heartbeats until the connection is closed
                while (socket.State == WebSocketState.Open)
                {
                    await Task.Delay(TimeSpan.FromSeconds(HeartbeatIntervalSeconds));
                    
                    // Skip heartbeat if connection is already closed
                    if (socket.State != WebSocketState.Open) break;
                    
                    try
                    {
                        await _webSocketManager.SendToConnectionAsync(connection.ConnectionId, new HeartbeatDTO());
                        
                        _logger.LogTrace("Sent heartbeat to user {UserId}", connection.UserId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to send heartbeat to user {UserId}", connection.UserId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in heartbeat task for user {UserId}", connection.UserId);
            }
        }

        // Chat functionality migrated from NotificationHubLibrary
        public async Task<bool> SendChatMessageAsync(int receiverId, string message, int senderId, bool queueIfOffline = true)
        {
            try
            {
                // Validate input parameters
                if (receiverId <= 0)
                {
                    _logger.LogWarning("Invalid receiverId: {ReceiverId}", receiverId);
                    return false;
                }

                if (string.IsNullOrWhiteSpace(message))
                {
                    _logger.LogWarning("Empty message from {SenderId} to {ReceiverId}", senderId, receiverId);
                    return false;
                }
                
                // Validate message size to prevent abuse
                const int maxMessageLength = 4000; // 4KB limit
                if (message.Length > maxMessageLength)
                {
                    _logger.LogWarning("Message too large ({Length} chars) from {SenderId}", message.Length, senderId);
                    return false;
                }
                
                // Sanitize message content to prevent XSS attacks
                message = SanitizeMessage(message);
                
                // Validate that receiver exists
                var receiver = await _userRepository.GetByIdAsync(receiverId);
                if (receiver == null)
                {
                    _logger.LogWarning("Cannot send chat message: Receiver with ID {ReceiverId} not found", receiverId);
                    return false;
                }
                
                // Try to send message in real-time if receiver is online
                var isReceiverOnline = await _messageRepository.IsUserOnlineAsync(receiverId);
                
                // Get sender info for notification display
                var sender = await _userRepository.GetByIdAsync(senderId);
                if (sender == null)
                {
                    _logger.LogWarning("Cannot send chat message: Sender with ID {SenderId} not found", senderId);
                    return false;
                }
                
                // Create the chat message data
                var messageEntity = MessageEntity.CreateChatMessage(
                    senderId,
                    receiverId,
                    message,
                    isReceiverOnline,
                    null
                );
                
                int messageId = 0;
                
                // Use a transaction scope to ensure both operations succeed or fail together
                using (var scope = new System.Transactions.TransactionScope(
                    System.Transactions.TransactionScopeAsyncFlowOption.Enabled))
                {
                    try
                    {
                        // Store the message in the database
                        messageId = await _messageRepository.CreateAsync(messageEntity);
                        messageEntity.MessageID = messageId;
                        
                        // Create notification if user is offline
                        if (!isReceiverOnline && queueIfOffline)
                        {
                            // Create a notification for offline user
                            var notification = new NotificationEntity
                            { 
                                SenderID = senderId, 
                                ReceiverID = receiverId, 
                                Message = message,
                                Timestamp = DateTime.UtcNow,
                                IsSeen = false,
                                MessageID = messageId
                            };
                            
                            // Store in notification repository
                            await _notificationRepository.CreateNotificationAsync(notification);
                            
                            _logger.LogInformation("Queued chat message for offline user {ReceiverId} from {SenderId}", 
                                receiverId, senderId);
                        }
                        
                        // Complete the transaction
                        scope.Complete();
                    }
                    catch (Exception ex)
                    {
                        // Transaction will automatically be aborted
                        _logger.LogError(ex, "Database transaction failed for chat message from {SenderId} to {ReceiverId}", 
                            senderId, receiverId);
                        return false;
                    }
                }
                
                // If user is online, send real-time notification after transaction is completed
                if (isReceiverOnline && messageId > 0)
                {
                    var chatMessageDto = new ChatMessageDto
                    {
                        MessageId = messageId,
                        SenderId = senderId,
                        SenderName = sender.FullName,
                        MessageText = message,
                        SentAt = DateTime.UtcNow
                    };
                    
                    await SendToUserAsync(receiverId, chatMessageDto);
                }
                
                return messageId > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending chat message from user {SenderId} to {ReceiverId}", 
                    senderId, receiverId);
                return false;
            }
        }

        // Helper method to sanitize message content
        private string SanitizeMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return message;
            
            // Basic HTML encoding to prevent script injection
            message = System.Web.HttpUtility.HtmlEncode(message);
            
            // Additional custom sanitization if needed
            // e.g., limiting special characters, removing suspicious patterns
            
            return message;
        }
        
        // Add rate limiting for WebSocket messages
        private async Task<bool> CheckRateLimitAsync(WebSocketConnectionEntity connection)
        {
            var userId = connection.UserId;
            var now = DateTime.UtcNow;
            
            const int rateLimitWindowSeconds = 60;  // Rate limit window in seconds
            
            if (_messageRateLimits.TryGetValue(userId, out var rateInfo))
            {
                // Check if the window has reset
                if ((now - rateInfo.LastMessage).TotalSeconds > rateLimitWindowSeconds)
                {
                    // Reset counter for new window
                    _messageRateLimits[userId] = (now, 1);
                    return true;
                }
                
                // Check if the user has exceeded the rate limit
                if (rateInfo.MessageCount >= MaxMessagesPerMinute)
                {
                    // Rate limit exceeded
                    _logger.LogWarning("Rate limit exceeded for user {UserId}: {Count} messages in {Seconds}s", 
                        userId, rateInfo.MessageCount, rateLimitWindowSeconds);
                    
                    // Notify user about rate limiting with backoff suggestion
                    await _webSocketManager.SendToConnectionAsync(connection.ConnectionId, new ErrorMessageDTO
                    {
                        Message = $"Message rate limit exceeded. Please wait {Math.Max(5, rateInfo.MessageCount / 10)} seconds before sending more messages.",
                        Code = "RATE_LIMIT_EXCEEDED"
                    });
                    
                    // Add progressive backoff for repeated offenders
                    if (rateInfo.MessageCount > MaxMessagesPerMinute * 2)
                    {
                        // Consider temporary blocking for extreme cases
                        _logger.LogWarning("User {UserId} has sent {Count} messages, considering temporary block", 
                            userId, rateInfo.MessageCount);
                    }
                    
                    return false;
                }
                
                // Increment counter
                _messageRateLimits[userId] = (rateInfo.LastMessage, rateInfo.MessageCount + 1);
                return true;
            }
            
            // First message from this user
            _messageRateLimits[userId] = (now, 1);
            return true;
        }

        public async Task<Dictionary<int, PendingMessageInfo>> GetPendingMessageCountsAsync(int receiverId)
        {
            try
            {
                var result = new Dictionary<int, PendingMessageInfo>();
                var pendingMessages = await _messageRepository.GetPendingMessagesAsync(receiverId);
                
                // Group messages by sender
                var groupedMessages = pendingMessages
                    .GroupBy(m => m.SenderID)
                    .ToDictionary(
                        g => g.Key,
                        g => new PendingMessageInfo
                        {
                            Count = g.Count(),
                            Messages = g.Select(m => m.MessageText).ToList(),
                            MessageIds = g.Select(m => m.MessageID).ToList()
                        });
                
                return groupedMessages;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pending message counts for user {ReceiverId}", receiverId);
                return new Dictionary<int, PendingMessageInfo>();
            }
        }

        public async Task<bool> MarkMessagesAsReadAsync(int senderId, int receiverId)
        {
            try
            {
                if (senderId <= 0 || receiverId <= 0)
                {
                    _logger.LogWarning("Invalid parameters: senderId={SenderId}, receiverId={ReceiverId}", 
                        senderId, receiverId);
                    return false;
                }
                
                // Get unread messages between these users
                var conversation = await _messageRepository.GetConversationAsync(senderId, receiverId);
                var unreadMessageIds = conversation
                    .Where(m => m.SenderID == senderId && m.ReceiverID == receiverId && !m.IsRead)
                    .Select(m => m.MessageID)
                    .ToList();
                
                if (!unreadMessageIds.Any())
                {
                    // No unread messages, nothing to do
                    _logger.LogDebug("No unread messages from {SenderId} to {ReceiverId}", senderId, receiverId);
                    return true;
                }
                
                // Use bulk update instead of individual updates (if supported by the repository)
                bool success = await _messageRepository.MarkMessagesAsReadBulkAsync(unreadMessageIds, receiverId);
                if (!success)
                {
                    // Fall back to individual updates
                    _logger.LogDebug("Bulk update failed, falling back to individual updates for {Count} messages", 
                        unreadMessageIds.Count);
                    
                    // Split into smaller batches for better performance
                    const int batchSize = 25;
                    var allSuccess = true;
                    
                    for (int i = 0; i < unreadMessageIds.Count; i += batchSize)
                    {
                        var batch = unreadMessageIds.Skip(i).Take(batchSize).ToList();
                        var tasks = batch.Select(id => _messageRepository.MarkMessageAsReadAsync(id, receiverId));
                        var results = await Task.WhenAll(tasks);
                        
                        if (results.Any(r => !r))
                        {
                            allSuccess = false;
                            _logger.LogWarning("Failed to mark some messages as read in batch {BatchIndex}", i / batchSize);
                        }
                    }
                    
                    if (!allSuccess)
                    {
                        _logger.LogWarning("Not all messages were successfully marked as read");
                        return false;
                    }
                }
                
                // Notify sender that messages have been read
                try
                {
                    await SendToUserAsync(senderId, new 
                    { 
                        type = "messages_read",
                        receiverId = receiverId,
                        messageIds = unreadMessageIds,
                        timestamp = DateTime.UtcNow
                    });
                }
                catch (Exception ex)
                {
                    // Log but don't fail the operation
                    _logger.LogWarning(ex, "Failed to notify sender {SenderId} about read messages", senderId);
                }
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking messages as read from {SenderId} to {ReceiverId}", 
                    senderId, receiverId);
                return false;
            }
        }

        public async Task<bool> MarkMessagesAsDeliveredAsync(int senderId, int receiverId)
        {
            try
            {
                if (senderId <= 0 || receiverId <= 0)
                {
                    _logger.LogWarning("Invalid parameters: senderId={SenderId}, receiverId={ReceiverId}", 
                        senderId, receiverId);
                    return false;
                }
                
                // Get undelivered messages between these users
                var conversation = await _messageRepository.GetConversationAsync(senderId, receiverId);
                var undeliveredMessageIds = conversation
                    .Where(m => m.SenderID == senderId && m.ReceiverID == receiverId && !m.IsDelivered)
                    .Select(m => m.MessageID)
                    .ToList();
                
                if (!undeliveredMessageIds.Any())
                {
                    // No undelivered messages, nothing to do
                    _logger.LogDebug("No undelivered messages from {SenderId} to {ReceiverId}", senderId, receiverId);
                    return true;
                }
                
                // Try to use bulk operation first for better performance
                bool success = await _messageRepository.MarkMessagesAsDeliveredBulkAsync(undeliveredMessageIds, receiverId);
                if (!success)
                {
                    // Fall back to individual updates
                    _logger.LogDebug("Bulk update failed, falling back to individual updates for {Count} messages", 
                        undeliveredMessageIds.Count);
                    
                    // Process in batches for better performance
                    const int batchSize = 25;
                    var allSuccess = true;
                    
                    for (int i = 0; i < undeliveredMessageIds.Count; i += batchSize)
                    {
                        var batch = undeliveredMessageIds.Skip(i).Take(batchSize).ToList();
                        var tasks = batch.Select(id => _messageRepository.MarkMessageAsDeliveredAsync(id, receiverId));
                        var results = await Task.WhenAll(tasks);
                        
                        if (results.Any(r => !r))
                        {
                            allSuccess = false;
                            _logger.LogWarning("Failed to mark some messages as delivered in batch {BatchIndex}", i / batchSize);
                        }
                    }
                    
                    if (!allSuccess)
                    {
                        _logger.LogWarning("Not all messages were successfully marked as delivered");
                        return false;
                    }
                }
                
                // Notify sender that messages have been delivered
                try
                {
                    await SendToUserAsync(senderId, new 
                    { 
                        type = "messages_delivered",
                        receiverId = receiverId,
                        messageIds = undeliveredMessageIds,
                        timestamp = DateTime.UtcNow
                    });
                }
                catch (Exception ex)
                {
                    // Log but don't fail the operation
                    _logger.LogWarning(ex, "Failed to notify sender {SenderId} about delivered messages", senderId);
                }
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking messages as delivered from {SenderId} to {ReceiverId}", 
                    senderId, receiverId);
                return false;
            }
        }
    }

    // Helper class for pending message info
    public class PendingMessageInfo
    {
        public int Count { get; set; }
        public List<string> Messages { get; set; } = new List<string>();
        public List<int> MessageIds { get; set; } = new List<int>();
    }
}