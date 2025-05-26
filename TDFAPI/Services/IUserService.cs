using TDFShared.DTOs.Users;
using TDFShared.DTOs.Common;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TDFAPI.Services
{
    /// <summary>
    /// Service interface for user management operations
    /// </summary>
    public interface IUserService
    {
        /// <summary>
        /// Gets a user by their ID
        /// </summary>
        Task<UserDto?> GetUserByIdAsync(int userId);

        /// <summary>
        /// Gets a user by their username
        /// </summary>
        Task<UserDto?> GetByUsernameAsync(string username);

        /// <summary>
        /// Gets a user DTO by their ID
        /// </summary>
        Task<UserDto?> GetUserDtoByIdAsync(int userId);

        /// <summary>
        /// Gets a paginated list of users
        /// </summary>
        Task<PaginatedResult<UserDto>> GetPaginatedAsync(int page, int pageSize);

        /// <summary>
        /// Creates a new user
        /// </summary>
        /// <exception cref="ValidationException">Thrown when validation fails</exception>
        Task<int> CreateAsync(CreateUserRequest userDto);

        /// <summary>
        /// Updates a user's information
        /// </summary>
        Task<bool> UpdateAsync(int userId, UpdateUserRequest userDto);

        /// <summary>
        /// Updates the current user's own profile
        /// </summary>
        Task<bool> UpdateSelfAsync(int userId, UpdateMyProfileRequest dto);

        /// <summary>
        /// Deletes a user
        /// </summary>
        Task<bool> DeleteAsync(int userId);

        /// <summary>
        /// Changes a user's password
        /// </summary>
        Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword);

        /// <summary>
        /// Invalidates a user's refresh token
        /// </summary>
        Task<bool> InvalidateRefreshTokenAsync(int userId);

        /// <summary>
        /// Updates a user's presence status
        /// </summary>
        Task<bool> UpdateUserPresenceAsync(int userId, bool isOnline, string? deviceInfo = null);

        /// <summary>
        /// Updates a user's profile picture
        /// </summary>
        Task<bool> UpdateProfilePictureAsync(int userId, Stream imageStream, string contentType);

        /// <summary>
        /// Gets users by department
        /// </summary>
        Task<IEnumerable<UserDto>> GetUsersByDepartmentAsync(string department);

        /// <summary>
        /// Gets all online users
        /// </summary>
        Task<IEnumerable<UserDto>> GetOnlineUsersAsync();

        /// <summary>
        /// Gets all users with their presence information
        /// </summary>
        Task<IEnumerable<UserDto>> GetAllUsersWithPresenceAsync();

        /// <summary>
        /// Updates the device information for a user
        /// </summary>
        /// <param name="userId">The ID of the user</param>
        /// <param name="deviceId">The unique identifier of the device</param>
        /// <param name="userAgent">The user agent string of the device</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task UpdateUserDeviceInfoAsync(int userId, string deviceId, string userAgent);
        
        /// <summary>
        /// Checks if a full name is already taken by another user
        /// </summary>
        /// <param name="fullName">The full name to check</param>
        /// <param name="excludeUserId">Optional user ID to exclude from the check (for updates)</param>
        /// <returns>True if the full name is already taken, false otherwise</returns>
        Task<bool> IsFullNameTakenAsync(string fullName, int? excludeUserId = null);
    }
}