using System.Collections.Generic;
using System.Threading.Tasks;
using TDFAPI.Models;
using TDFShared.DTOs.Users;

namespace TDFAPI.Services
{
    /// <summary>
    /// Service for managing push notification tokens
    /// </summary>
    public interface IPushTokenService
    {
        /// <summary>
        /// Register a new push notification token for a user
        /// </summary>
        /// <param name="userId">The ID of the user</param>
        /// <param name="registration">The token registration details</param>
        Task RegisterTokenAsync(int userId, PushTokenRegistrationDto registration);

        /// <summary>
        /// Unregister a push notification token for a user
        /// </summary>
        /// <param name="userId">The ID of the user</param>
        /// <param name="token">The token to unregister</param>
        Task UnregisterTokenAsync(int userId, string token);

        /// <summary>
        /// Get all active push tokens for a user
        /// </summary>
        /// <param name="userId">The ID of the user</param>
        /// <returns>A list of active push tokens</returns>
        Task<IEnumerable<PushToken>> GetUserTokensAsync(int userId);

        /// <summary>
        /// Get all active push tokens for a list of users
        /// </summary>
        /// <param name="userIds">The IDs of the users</param>
        /// <returns>A dictionary mapping user IDs to their active push tokens</returns>
        Task<IDictionary<int, IEnumerable<PushToken>>> GetUsersTokensAsync(IEnumerable<int> userIds);

        /// <summary>
        /// Deactivate all tokens for a user
        /// </summary>
        /// <param name="userId">The ID of the user</param>
        Task DeactivateAllTokensAsync(int userId);

        /// <summary>
        /// Update the last used timestamp for a token
        /// </summary>
        /// <param name="token">The token to update</param>
        Task UpdateTokenLastUsedAsync(string token);
    }
} 