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
using TDFShared.DTOs.Messages;
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

                _userPresence[userId] = new UserPresenceInfo
                {
                    UserId = userId,
                    Username = user.Username,
                    FullName = user.FullName,
                    Status = user.PresenceStatus,
                    StatusMessage = user.StatusMessage,
                    IsAvailableForChat = user.IsAvailableForChat,
                    LastActivity = DateTime.UtcNow
                };

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
                        Username = user.Username,
                        FullName = user.FullName,
                        Status = status,
                        StatusMessage = statusMessage ?? user.StatusMessage,
                        IsAvailableForChat = user.IsAvailableForChat,
                        LastActivity = DateTime.UtcNow
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
                    user.Username, 
                    user.FullName, 
                    status, 
                    statusMessage ?? user.StatusMessage
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
                        Username = user.Username,
                        FullName = user.FullName,
                        Status = user.PresenceStatus,
                        StatusMessage = user.StatusMessage,
                        IsAvailableForChat = isAvailable,
                        LastActivity = DateTime.UtcNow
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
                    user.Username,
                    user.FullName,
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
        public IEnumerable<UserPresenceInfo> GetOnlineUsers()
        {
            return _userPresence.Values
                .Where(u => u.Status != UserPresenceStatus.Offline)
                .ToList();
        }
    }
    
    // Custom request class for user presence updates
    public class UpdateUserStatusRequest
    {
        public UserPresenceStatus? PresenceStatus { get; set; }
        public string? StatusMessage { get; set; }
        public bool? IsAvailableForChat { get; set; }
    }
    
    /// <summary>
    /// Represents a user's presence information
    /// </summary>
    public class UserPresenceInfo
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public string FullName { get; set; }
        public UserPresenceStatus Status { get; set; }
        public string StatusMessage { get; set; }
        public bool IsAvailableForChat { get; set; }
        public DateTime LastActivity { get; set; }
    }
} 