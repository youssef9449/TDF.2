using System.Threading.Tasks;
using TDFShared.Enums;
using System.Collections.Generic;
using TDFShared.DTOs.Users;

namespace TDFAPI.Services
{
    public interface IUserPresenceService
    {
        /// <summary>
        /// Gets the current presence status of a user
        /// </summary>
        Task<UserPresenceStatus> GetStatusAsync(int userId);
        
        /// <summary>
        /// Updates a user's presence status and broadcasts the change to relevant users
        /// </summary>
        Task<bool> UpdateStatusAsync(int userId, UserPresenceStatus status, string? statusMessage = null);
        
        /// <summary>
        /// Records user activity to update the last activity timestamp and potentially change status from Away to Online
        /// </summary>
        Task RecordUserActivityAsync(int userId);
        
        /// <summary>
        /// Checks for inactive users and updates their status accordingly (e.g., Online -> Away -> Offline)
        /// </summary>
        Task CheckInactiveUsersAsync();
        
        /// <summary>
        /// Sets whether a user is available for chat and broadcasts the change
        /// </summary>
        Task<bool> SetAvailabilityForChatAsync(int userId, bool isAvailable);
        
        /// <summary>
        /// Gets a list of all currently online users
        /// </summary>
        Task<IEnumerable<UserPresenceInfo>> GetOnlineUsersAsync();

        /// <summary>
        /// Updates a user's connection status
        /// </summary>
        Task UpdateUserConnectionStatusAsync(int userId, bool isOnline, string? connectionId = null, string? deviceType = null);

        /// <summary>
        /// Gets a user's current presence information
        /// </summary>
        Task<UserPresenceInfo> GetUserPresenceAsync(int userId);

        /// <summary>
        /// Gets presence information for multiple users
        /// </summary>
        Task<Dictionary<int, UserPresenceInfo>> GetUsersPresenceAsync(IEnumerable<int> userIds);
    }
}
