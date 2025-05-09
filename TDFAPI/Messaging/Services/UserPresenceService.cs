using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TDFShared.DTOs.Users;
using TDFAPI.Messaging.Interfaces;
using TDFAPI.Data;

namespace TDFAPI.Messaging.Services
{
    /// <summary>
    /// Service for tracking and managing user presence/online status
    /// </summary>
    public class UserPresenceService : IUserPresenceService
    {
        private readonly ILogger<UserPresenceService> _logger;
        private readonly ApplicationDbContext _dbContext;
        private readonly Dictionary<int, UserPresence> _userPresenceCache;

        public UserPresenceService(ILogger<UserPresenceService> logger, ApplicationDbContext dbContext)
        {
            _logger = logger;
            _dbContext = dbContext;
            _userPresenceCache = new Dictionary<int, UserPresence>();
        }

        /// <summary>
        /// Updates a user's connection status
        /// </summary>
        public async Task UpdateUserConnectionStatusAsync(int userId, bool isOnline, string connectionId = null, string deviceType = null)
        {
            try
            {
                var presence = await GetUserPresenceAsync(userId);
                
                if (presence == null)
                {
                    presence = new UserPresence
                    {
                        UserId = userId,
                        IsOnline = isOnline,
                        LastSeen = DateTime.UtcNow,
                        ConnectionId = connectionId,
                        DeviceType = deviceType
                    };
                }
                else
                {
                    presence.IsOnline = isOnline;
                    presence.LastSeen = DateTime.UtcNow;
                    
                    if (connectionId != null)
                    {
                        presence.ConnectionId = connectionId;
                    }
                    
                    if (deviceType != null)
                    {
                        presence.DeviceType = deviceType;
                    }
                }

                // Update the cache
                _userPresenceCache[userId] = presence;

                // In a real implementation, we might persist this to a database
                // or publish an event for other services to consume
                _logger.LogInformation($"Updated user {userId} presence: {(isOnline ? "Online" : "Offline")}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating user {userId} connection status");
                throw;
            }
        }

        /// <summary>
        /// Gets a user's current presence information
        /// </summary>
        public Task<UserPresence> GetUserPresenceAsync(int userId)
        {
            if (_userPresenceCache.TryGetValue(userId, out var presence))
            {
                return Task.FromResult(presence);
            }
            
            // Create default presence object for users not in cache
            var defaultPresence = new UserPresence
            {
                UserId = userId,
                IsOnline = false,
                LastSeen = DateTime.MinValue
            };
            
            return Task.FromResult(defaultPresence);
        }

        /// <summary>
        /// Gets presence information for multiple users
        /// </summary>
        public async Task<Dictionary<int, UserPresence>> GetUsersPresenceAsync(IEnumerable<int> userIds)
        {
            var result = new Dictionary<int, UserPresence>();
            
            foreach (var userId in userIds)
            {
                result[userId] = await GetUserPresenceAsync(userId);
            }
            
            return result;
        }

        /// <summary>
        /// Gets a list of all currently online users
        /// </summary>
        public Task<List<UserPresence>> GetOnlineUsersAsync()
        {
            var onlineUsers = _userPresenceCache.Values
                .Where(p => p.IsOnline)
                .ToList();
                
            return Task.FromResult(onlineUsers);
        }
    }
} 