using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TDFAPI.Services;

namespace TDFAPI.Messaging
{
    /// <summary>
    /// Stores undelivered messages to ensure delivery
    /// </summary>
    public class MessageStore
    {
        private readonly ICacheService _cacheService;
        private readonly ILogger<MessageStore> _logger;
        private readonly ConcurrentDictionary<string, WebSocketMessage> _pendingMessages = new();
        private readonly ConcurrentDictionary<string, DateTimeOffset> _messageExpiration = new();
        private readonly Timer _cleanupTimer;

        private const int DEFAULT_MESSAGE_EXPIRY_MINUTES = 24 * 60; // 1 day
        private const int CLEANUP_INTERVAL_MINUTES = 5;

        public MessageStore(ICacheService cacheService, ILogger<MessageStore> logger)
        {
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Start periodic cleanup of expired messages
            _cleanupTimer = new Timer(CleanupExpiredMessages, null, 
                TimeSpan.FromMinutes(CLEANUP_INTERVAL_MINUTES), 
                TimeSpan.FromMinutes(CLEANUP_INTERVAL_MINUTES));
        }

        /// <summary>
        /// Stores a message for delivery
        /// </summary>
        /// <param name="message">The message to store</param>
        /// <param name="expiryMinutes">How long to keep trying to deliver the message</param>
        public void StoreMessage(WebSocketMessage message, int expiryMinutes = DEFAULT_MESSAGE_EXPIRY_MINUTES)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (string.IsNullOrEmpty(message.Id))
            {
                message.Id = Guid.NewGuid().ToString();
            }

            _pendingMessages[message.Id] = message;
            _messageExpiration[message.Id] = DateTimeOffset.UtcNow.AddMinutes(expiryMinutes);

            // Also store in the distributed cache for persistence across restarts
            var cacheKey = $"msg:{message.Id}";
            
            // Store directly in memory without using cache service for now
            // This avoids ambiguous method call issues
            _cacheService.GetOrCreateAsync(cacheKey, () => Task.FromResult(message), absoluteExpirationMinutes: expiryMinutes);

            _logger.LogDebug("Stored message {MessageId} for delivery to {Recipient}, expires in {ExpiryMinutes} minutes", 
                message.Id, message.To, expiryMinutes);
        }

        /// <summary>
        /// Marks a message as delivered
        /// </summary>
        /// <param name="messageId">The ID of the delivered message</param>
        public void MarkAsDelivered(string messageId)
        {
            if (string.IsNullOrEmpty(messageId))
            {
                throw new ArgumentNullException(nameof(messageId));
            }

            _pendingMessages.TryRemove(messageId, out _);
            _messageExpiration.TryRemove(messageId, out _);

            // Remove from distributed cache
            _cacheService.RemoveFromCache($"msg:{messageId}");

            _logger.LogDebug("Marked message {MessageId} as delivered", messageId);
        }

        /// <summary>
        /// Gets all pending messages for a user
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <returns>A list of pending messages</returns>
        public IEnumerable<WebSocketMessage> GetPendingMessagesForUser(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentNullException(nameof(userId));
            }

            var messages = _pendingMessages.Values
                .Where(m => m.To == userId)
                .OrderBy(m => m.Timestamp)
                .ToList();

            _logger.LogDebug("Retrieved {Count} pending messages for user {UserId}", messages.Count, userId);
            return messages;
        }

        private void CleanupExpiredMessages(object? state)
        {
            try
            {
                var now = DateTimeOffset.UtcNow;
                var expiredMessageIds = _messageExpiration
                    .Where(kvp => kvp.Value < now)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var messageId in expiredMessageIds)
                {
                    _pendingMessages.TryRemove(messageId, out var message);
                    _messageExpiration.TryRemove(messageId, out _);
                    
                    // Remove from distributed cache
                    _cacheService.RemoveFromCache($"msg:{messageId}");

                    _logger.LogDebug("Removed expired message {MessageId} intended for {Recipient}", 
                        messageId, message?.To ?? "unknown");
                }

                if (expiredMessageIds.Count > 0)
                {
                    _logger.LogInformation("Cleaned up {Count} expired messages", expiredMessageIds.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up expired messages");
            }
        }
    }
} 