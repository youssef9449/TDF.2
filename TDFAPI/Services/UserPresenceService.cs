using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TDFShared.Enums;
using TDFShared.DTOs.Users;
using TDFAPI.Repositories;
using TDFAPI.Messaging.Interfaces;
using TDFShared.Models.Message;
using TDFShared.Models.User;

namespace TDFAPI.Services
{
    /// <summary>
    /// Service for managing user presence information
    /// </summary>
    public class UserPresenceService : IUserPresenceService
    {
        private readonly IUserRepository _userRepository;
        private readonly ILogger<UserPresenceService> _logger;
        private readonly IEventMediator _eventMediator;
        private readonly ConcurrentDictionary<int, UserPresenceInfo> _userPresence = new();
        
        // Constants for inactivity timeouts
        private const int AWAY_TIMEOUT_MINUTES = 10;
        private const int OFFLINE_TIMEOUT_MINUTES = 30;
        
        public UserPresenceService(
            IUserRepository userRepository,
            ILogger<UserPresenceService> logger,
            IEventMediator eventMediator)
        {
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _eventMediator = eventMediator ?? throw new ArgumentNullException(nameof(eventMediator));
        }
        
        /// <summary>
        /// Gets the current presence status of a user
        /// </summary>
        public async Task<UserPresenceStatus> GetStatusAsync(int userId)
        {
            try
            {
                if (_userPresence.TryGetValue(userId, out var presence))
                {
                    return presence.Status;
                }

                // Load from database for first access
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    throw new KeyNotFoundException($"User with ID {userId} not found");
                }

                var info = new UserPresenceInfo
                {
                    UserId = userId,
                    Username = user.UserName ?? string.Empty,
                    FullName = user.FullName ?? string.Empty,
                    Status = user.PresenceStatus,
                    StatusMessage = user.StatusMessage ?? string.Empty,
                    IsAvailableForChat = user.IsAvailableForChat ?? false,
                    LastActivity = DateTime.UtcNow,
                    Department = user.Department,
                    IsOnline = user.IsConnected ?? false,
                    LastSeen = user.LastActivityTime
                };

                _userPresence[userId] = info;

                return user.PresenceStatus;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting status for user {UserId}", userId);
                return UserPresenceStatus.Offline; // Default to offline on error
            }
        }
        
        /// <summary>
        /// Updates a user's presence status and broadcasts the change to relevant users
        /// </summary>
        public async Task<bool> UpdateStatusAsync(int userId, UserPresenceStatus status, string? statusMessage = null)
        {
            try
            {
                _logger.LogInformation("Updating status for user {UserId} to {Status}", userId, status);

                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("Cannot update status for non-existent user ID {UserId}", userId);
                    return false;
                }

                // Update user object
                user.PresenceStatus = status;
                if (statusMessage != null)
                {
                    user.StatusMessage = statusMessage;
                }
                
                // Create custom update request
                var updateRequest = new UpdateUserStatusRequest
                {
                    PresenceStatus = status,
                    StatusMessage = statusMessage
                };
                
                // Update repository
                await _userRepository.UpdateStatusAsync(userId, updateRequest);

                // Update in-memory cache
                _userPresence.AddOrUpdate(
                    userId,
                    id => new UserPresenceInfo
                    {
                        UserId = id,
                        Username = user.UserName ?? string.Empty,
                        FullName = user.FullName ?? string.Empty,
                        Status = status,
                        StatusMessage = statusMessage ?? user.StatusMessage ?? string.Empty,
                        IsAvailableForChat = user.IsAvailableForChat ?? false,
                        LastActivity = DateTime.UtcNow,
                        Department = user.Department,
                        IsOnline = user.IsConnected ?? false,
                        LastSeen = user.LastActivityTime
                    },
                    (id, existing) =>
                    {
                        existing.Status = status;
                        if (statusMessage != null)
                        {
                            existing.StatusMessage = statusMessage;
                        }
                        existing.LastActivity = DateTime.UtcNow;
                        return existing;
                    });

                // Publish status changed event via the event mediator
                _eventMediator.Publish(new Messaging.UserStatusChangedEvent(
                    userId, 
                    user.UserName ?? string.Empty, 
                    user.FullName ?? string.Empty, 
                    status, 
                    statusMessage ?? user.StatusMessage ?? string.Empty
                ));

                _logger.LogInformation("User {UserId} status updated to {Status}", userId, status);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating status for user {UserId}", userId);
                return false;
            }
        }
        
        /// <summary>
        /// Records user activity to update the last activity timestamp and potentially change status from Away to Online
        /// </summary>
        public async Task RecordUserActivityAsync(int userId)
        {
            try
            {
                if (_userPresence.TryGetValue(userId, out var presence))
                {
                    // Update last activity timestamp
                    presence.LastActivity = DateTime.UtcNow;
                    
                    // If user is away, set them back to online
                    if (presence.Status == UserPresenceStatus.Away)
                    {
                        await UpdateStatusAsync(userId, UserPresenceStatus.Online);
                    }
                }
                
                // Update last activity time in the database
                await _userRepository.UpdateLastActivityAsync(userId, DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording activity for user {UserId}", userId);
            }
        }
        
        /// <summary>
        /// Checks for inactive users and updates their status accordingly (e.g., Online -> Away -> Offline)
        /// </summary>
        public async Task CheckInactiveUsersAsync()
        {
            try
            {
                var now = DateTime.UtcNow;
                var usersToUpdate = new List<(int UserId, UserPresenceStatus NewStatus)>();
                
                foreach (var presence in _userPresence.Values)
                {
                    if (presence.Status == UserPresenceStatus.Online)
                    {
                        var inactiveMinutes = (now - presence.LastActivity).TotalMinutes;
                        
                        if (inactiveMinutes > OFFLINE_TIMEOUT_MINUTES)
                        {
                            usersToUpdate.Add((presence.UserId, UserPresenceStatus.Offline));
                        }
                        else if (inactiveMinutes > AWAY_TIMEOUT_MINUTES)
                        {
                            usersToUpdate.Add((presence.UserId, UserPresenceStatus.Away));
                        }
                    }
                }
                
                // Process status updates
                foreach (var update in usersToUpdate)
                {
                    await UpdateStatusAsync(update.UserId, update.NewStatus);
                }
                
                if (usersToUpdate.Count > 0)
                {
                    _logger.LogInformation("Updated status for {Count} inactive users", usersToUpdate.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking inactive users");
            }
        }
        
        /// <summary>
        /// Sets whether a user is available for chat and broadcasts the change
        /// </summary>
        public async Task<bool> SetAvailabilityForChatAsync(int userId, bool isAvailable)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("Cannot update availability for non-existent user ID {UserId}", userId);
                    return false;
                }

                // Create update request
                var updateRequest = new UpdateUserStatusRequest
                {
                    IsAvailableForChat = isAvailable
                };
                
                // Update repository
                await _userRepository.UpdateAvailabilityAsync(userId, updateRequest);

                // Update in-memory cache
                _userPresence.AddOrUpdate(
                    userId,
                    id => new UserPresenceInfo
                    {
                        UserId = id,
                        Username = user.UserName ?? string.Empty,
                        FullName = user.FullName ?? string.Empty,
                        Status = user.PresenceStatus,
                        StatusMessage = user.StatusMessage ?? string.Empty,
                        IsAvailableForChat = isAvailable,
                        LastActivity = DateTime.UtcNow,
                        Department = user.Department,
                        IsOnline = user.IsConnected ?? false,
                        LastSeen = user.LastActivityTime
                    },
                    (id, existing) =>
                    {
                        existing.IsAvailableForChat = isAvailable;
                        existing.LastActivity = DateTime.UtcNow;
                        return existing;
                    });

                // Publish availability changed event using the event mediator
                _eventMediator.Publish(new Messaging.UserAvailabilityChangedEvent(
                    userId,
                    user.UserName ?? string.Empty,
                    user.FullName ?? string.Empty,
                    isAvailable
                ));

                _logger.LogInformation("User {UserId} availability updated to {IsAvailable}", userId, isAvailable);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating chat availability for user {UserId}", userId);
                return false;
            }
        }
        
        /// <summary>
        /// Gets a list of all currently online users
        /// </summary>
        public Task<IEnumerable<UserPresenceInfo>> GetOnlineUsersAsync()
        {
            return Task.FromResult<IEnumerable<UserPresenceInfo>>(_userPresence.Values
                .Where(u => u.Status != UserPresenceStatus.Offline)
                .ToList());
        }

        /// <summary>
        /// Updates a user's connection status
        /// </summary>
        public async Task UpdateUserConnectionStatusAsync(int userId, bool isOnline, string? connectionId = null, string? deviceType = null)
        {
            try
            {
                var presence = await GetUserPresenceAsync(userId);

                presence.IsOnline = isOnline;
                presence.LastSeen = DateTime.UtcNow;
                presence.Status = isOnline ? UserPresenceStatus.Online : UserPresenceStatus.Offline;

                if (connectionId != null)
                {
                    presence.ConnectionId = connectionId;
                }

                if (deviceType != null)
                {
                    presence.DeviceType = deviceType;
                }

                _userPresence[userId] = presence;

                _logger.LogInformation("Updated user {UserId} connection: {Status}", userId, isOnline ? "Online" : "Offline");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user {UserId} connection status", userId);
                throw;
            }
        }

        /// <summary>
        /// Gets a user's current presence information
        /// </summary>
        public async Task<UserPresenceInfo> GetUserPresenceAsync(int userId)
        {
            if (_userPresence.TryGetValue(userId, out var presence))
            {
                return presence;
            }

            // Try to load from repository
            var user = await _userRepository.GetByIdAsync(userId);
            if (user != null)
            {
                var info = new UserPresenceInfo
                {
                    UserId = userId,
                    Username = user.UserName ?? string.Empty,
                    FullName = user.FullName ?? string.Empty,
                    Status = user.PresenceStatus,
                    StatusMessage = user.StatusMessage ?? string.Empty,
                    IsAvailableForChat = user.IsAvailableForChat ?? false,
                    LastActivity = DateTime.UtcNow,
                    Department = user.Department,
                    IsOnline = user.IsConnected ?? false,
                    LastSeen = user.LastActivityTime
                };
                _userPresence[userId] = info;
                return info;
            }

            return new UserPresenceInfo
            {
                UserId = userId,
                IsOnline = false,
                Status = UserPresenceStatus.Offline,
                LastSeen = DateTime.MinValue
            };
        }

        /// <summary>
        /// Gets presence information for multiple users
        /// </summary>
        public async Task<Dictionary<int, UserPresenceInfo>> GetUsersPresenceAsync(IEnumerable<int> userIds)
        {
            var result = new Dictionary<int, UserPresenceInfo>();

            foreach (var userId in userIds)
            {
                result[userId] = await GetUserPresenceAsync(userId);
            }

            return result;
        }
    }
    
    // Custom request class for user presence updates
    public class UpdateUserStatusRequest
    {
        public UserPresenceStatus? PresenceStatus { get; set; }
        public string? StatusMessage { get; set; }
        public bool? IsAvailableForChat { get; set; }
    }
}
