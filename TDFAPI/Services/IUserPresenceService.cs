using System.Collections.Generic;
using System.Threading.Tasks;
using TDFShared.DTOs.Users;
using TDFShared.Enums;
using TDFShared.Services;

namespace TDFAPI.Services
{
    public interface IUserPresenceService : TDFShared.Services.IUserPresenceService
    {
        /// <summary>
        /// Updates a user's presence status and broadcasts the change to relevant users
        /// </summary>
        Task<bool> UpdateStatusAsync(int userId, UserPresenceStatus status, string? statusMessage = null);
        
        /// <summary>
        /// Checks for inactive users and updates their status accordingly (e.g., Online -> Away -> Offline)
        /// </summary>
        Task CheckInactiveUsersAsync();
        
        /// <summary>
        /// Updates a user's connection status
        /// </summary>
        Task UpdateUserConnectionStatusAsync(int userId, bool isOnline, string? connectionId = null, string? deviceType = null);

        /// <summary>
        /// Gets a user's current presence information
        /// </summary>
        Task<UserPresenceInfo> GetUserPresenceAsync(int userId);

        /// <summary>
        /// Sets whether a user is available for chat and broadcasts the change
        /// </summary>
        new Task<bool> SetAvailabilityForChatAsync(int userId, bool isAvailable);
    }
}
