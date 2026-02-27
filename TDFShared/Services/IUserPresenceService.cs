using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TDFShared.DTOs.Common;
using TDFShared.DTOs.Users;
using TDFShared.Enums;

namespace TDFShared.Services
{
    /// <summary>
    /// Unified interface for user presence management
    /// </summary>
    public interface IUserPresenceService
    {
        /// <summary>
        /// Gets the current presence status of a user
        /// </summary>
        Task<UserPresenceStatus> GetUserStatusAsync(int userId);

        /// <summary>
        /// Gets presence information for multiple users
        /// </summary>
        Task<Dictionary<int, UserPresenceInfo>> GetUsersPresenceAsync(IEnumerable<int> userIds);

        /// <summary>
        /// Updates a user's presence status
        /// </summary>
        Task UpdateUserStatusAsync(int userId, UserPresenceStatus status, string? statusMessage = null);

        /// <summary>
        /// Records user activity to update the last activity timestamp
        /// </summary>
        Task RecordUserActivityAsync(int userId);

        /// <summary>
        /// Sets whether a user is available for chat
        /// </summary>
        Task SetAvailabilityForChatAsync(int userId, bool isAvailable);

        /// <summary>
        /// Gets a list of online users with pagination
        /// </summary>
        Task<PaginatedResult<UserPresenceInfo>> GetOnlineUsersAsync(int page = 1, int pageSize = 100);

        /// <summary>
        /// Fired when any user's status changes
        /// </summary>
        event EventHandler<UserStatusChangedEventArgs> UserStatusChanged;

        /// <summary>
        /// Fired when any user's chat availability changes
        /// </summary>
        event EventHandler<UserAvailabilityChangedEventArgs> UserAvailabilityChanged;
    }
}
