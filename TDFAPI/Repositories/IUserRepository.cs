using TDFShared.Enums;
using TDFShared.DTOs.Common;
using TDFShared.DTOs.Users;
using TDFShared.Models.User;
using System;
using TDFAPI.Services;

namespace TDFAPI.Repositories
{
    public interface IUserRepository
    {
        Task<UserDto?> GetByIdAsync(int userId);
        Task<UserDto?> GetByUsernameAsync(string username);
        Task<List<UserDto>> GetAllAsync();
        Task<PaginatedResult<UserDto>> GetPaginatedAsync(int page, int pageSize);
        Task<int> CreateAsync(CreateUserRequest userDto, string passwordHash, string salt);
        Task<bool> UpdateAsync(int userId, UpdateUserRequest userDto);
        Task<bool> UpdateSelfAsync(int userId, UpdateMyProfileRequest dto);
        Task<bool> DeleteAsync(int userId);
        Task<bool> ChangePasswordAsync(int userId, string passwordHash, string salt);
        Task<bool> UpdateRefreshTokenAsync(int userId, string? refreshToken, DateTime refreshTokenExpiryTime);
        Task<bool> UpdateLoginAttemptsAsync(int userId, int failedAttempts, bool isLocked, DateTime? lockoutEnd);
        Task<bool> UpdateAfterLoginAsync(int userId, string refreshToken, DateTime refreshTokenExpiryTime, DateTime lastLoginDate, string lastLoginIp);
        Task<bool> UpdateProfilePictureAsync(int userId, byte[] pictureData);

        // User auth data retrieval - new method
        Task<UserAuthData?> GetUserAuthDataAsync(int userId);
        
        // User presence methods
        Task<bool> UpdatePresenceStatusAsync(int userId, UserPresenceStatus status, string? statusMessage = null);
        Task<bool> UpdateLastActivityAsync(int userId, DateTime activityTime);
        Task<bool> UpdateCurrentDeviceAsync(int userId, string device, string machineName);
        Task<bool> SetAvailabilityForChatAsync(int userId, bool isAvailable);
        Task<List<UserDto>> GetUsersByDepartmentAsync(string department);
        Task<List<UserDto>> GetUsersByIdsAsync(IEnumerable<int> userIds);
        Task<List<UserDto>> GetOnlineUsersAsync();
        
        // Additional methods
        Task<List<UserDto>> GetByDepartmentAndRoleAsync(string department);
        Task<List<UserDto>> GetUsersByRoleAsync(string role);
        Task<bool> IsKnownIpAddressAsync(int userId, string ipAddress);
        
        // New methods for handling status and availability with UpdateUserStatusRequest
        Task<bool> UpdateStatusAsync(int userId, UpdateUserStatusRequest request);
        Task<bool> UpdateAvailabilityAsync(int userId, UpdateUserStatusRequest request);

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

    // UserAuthData used for password operations
    public class UserAuthData
    {
        public int UserId { get; set; }
        public int FailedLoginAttempts { get; set; }
        public string PasswordHash { get; set; } = string.Empty;
        public string PasswordSalt { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime? RefreshTokenExpiryTime { get; set; }
        public bool IsLocked { get; set; }
        public DateTime? LockoutEnd { get; set; }
    }
} 