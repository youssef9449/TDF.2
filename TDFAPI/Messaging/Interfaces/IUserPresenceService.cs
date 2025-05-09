using System;
using System.Collections.Generic;
using System.Threading;
using TDFShared.DTOs.Users;

namespace TDFAPI.Messaging.Interfaces
{
    /// <summary>
    /// Service for tracking and managing user presence/online status
    /// </summary>
    public interface IUserPresenceService
    {
        /// <summary>
        /// Updates a user's connection status
        /// </summary>
        /// <param name="userId">The user's ID</param>
        /// <param name="isOnline">Whether the user is online</param>
        /// <param name="connectionId">Optional connection identifier</param>
        /// <param name="deviceType">Type of device used</param>
        /// <returns>Task representing the asynchronous operation</returns>
        Task UpdateUserConnectionStatusAsync(int userId, bool isOnline, string connectionId = null, string deviceType = null);
        
        /// <summary>
        /// Gets a user's current presence information
        /// </summary>
        /// <param name="userId">The user's ID</param>
        /// <returns>The user's presence information</returns>
        Task<UserPresence> GetUserPresenceAsync(int userId);
        
        /// <summary>
        /// Gets presence information for multiple users
        /// </summary>
        /// <param name="userIds">List of user IDs</param>
        /// <returns>Dictionary mapping user IDs to their presence information</returns>
        Task<Dictionary<int, UserPresence>> GetUsersPresenceAsync(IEnumerable<int> userIds);
        
        /// <summary>
        /// Gets a list of all currently online users
        /// </summary>
        /// <returns>List of presence information for online users</returns>
        Task<List<UserPresence>> GetOnlineUsersAsync();
    }
} 