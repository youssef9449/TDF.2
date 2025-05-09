using TDFAPI.Repositories;
using TDFShared.Enums;
using System;
using TDFShared.DTOs.Users;
using TDFShared.DTOs.Common;
using System.ComponentModel.DataAnnotations;

namespace TDFAPI.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly IAuthService _authService;
        private readonly ILogger<UserService> _logger;
        private readonly IServiceProvider _serviceProvider;
        
        public UserService(IUserRepository userRepository, IAuthService authService, ILogger<UserService> logger, IServiceProvider serviceProvider)
        {
            _userRepository = userRepository;
            _authService = authService;
            _logger = logger;
            _serviceProvider = serviceProvider;
        }
        
        public async Task<UserDto?> GetUserByIdAsync(int userId)
        {
            return await _userRepository.GetByIdAsync(userId);
        }
        
        public async Task<UserDto?> GetByUsernameAsync(string username)
        {
            return await _userRepository.GetByUsernameAsync(username);
        }
        
        public async Task<PaginatedResult<UserDto>> GetPaginatedAsync(int page, int pageSize)
        {
            return await _userRepository.GetPaginatedAsync(page, pageSize);
        }
        
        public async Task<int> CreateAsync(CreateUserRequest userDto)
        {
            if (string.IsNullOrWhiteSpace(userDto.Username))
            {
                throw new ValidationException("Username is required");
            }

            if (string.IsNullOrWhiteSpace(userDto.Password))
            {
                throw new ValidationException("Password is required");
            }

            if (string.IsNullOrWhiteSpace(userDto.FullName))
            {
                throw new ValidationException("Full name is required");
            }

            // Check if username already exists
            var existingUser = await _userRepository.GetByUsernameAsync(userDto.Username);
            if (existingUser != null)
            {
                _logger.LogWarning("User creation failed: Username {Username} already exists", userDto.Username);
                throw new ValidationException($"Username '{userDto.Username}' is already taken.");
            }

            // Check password strength before hashing
            if (!AuthService.IsPasswordStrong(userDto.Password))
            {
                _logger.LogWarning("User creation failed: Password does not meet strength requirements");
                throw new ValidationException("Password does not meet strength requirements. It should be at least 8 characters long and include uppercase, lowercase, numbers, and special characters.");
            }

            var passwordHash = _authService.HashPassword(userDto.Password, out string salt);
            
            // Pass the DTO and auth data to repository
            try
            {
                return await _userRepository.CreateAsync(userDto, passwordHash, salt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user {Username}: {Message}", userDto.Username, ex.Message);
                throw new InvalidOperationException("Failed to create user. Please try again later.", ex);
            }
        }
        
        public async Task<bool> UpdateAsync(int userId, UpdateUserRequest userDto)
        {
            // This method is likely called by an Admin, repository handles the update.
            return await _userRepository.UpdateAsync(userId, userDto);
        }
        
        public async Task<bool> UpdateSelfAsync(int userId, UpdateMyProfileRequest dto)
        {
            // Add specific validation if needed, though DTO has attributes.
            // Call the dedicated repository method for self-updates.
            return await _userRepository.UpdateSelfAsync(userId, dto);
        }
        
        public async Task<bool> DeleteAsync(int userId)
        {
            return await _userRepository.DeleteAsync(userId);
        }
        
        public async Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
        {
            // Check new password strength
            if (!AuthService.IsPasswordStrong(newPassword))
            {
                _logger.LogWarning("Password change failed for user {UserId}: New password does not meet strength requirements.", userId);
                return false;
            }

            var user = await _userRepository.GetUserAuthDataAsync(userId);
            if (user == null || string.IsNullOrEmpty(user.PasswordHash) || string.IsNullOrEmpty(user.PasswordSalt))
            {
                return false;
            }
            
            if (!_authService.VerifyPassword(currentPassword, user.PasswordHash, user.PasswordSalt))
            {
                return false;
            }
            
            var newPasswordHash = _authService.HashPassword(newPassword, out string newSalt);
            return await _userRepository.ChangePasswordAsync(userId, newPasswordHash, newSalt);
        }

        public async Task<bool> InvalidateRefreshTokenAsync(int userId)
        {
            try
            {
                return await _userRepository.UpdateRefreshTokenAsync(userId, null, DateTime.MinValue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to invalidate refresh token for user {UserId}: {Message}", userId, ex.Message);
                return false;
            }
        }

        public async Task<UserDto?> GetUserDtoByIdAsync(int userId)
        {
            return await GetUserByIdAsync(userId);
        }
        
        public async Task<bool> UpdateUserPresenceAsync(int userId, bool isOnline, string? deviceInfo = null)
        {
            try
            {
                var user = await GetUserByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("Cannot update presence for non-existent user ID {UserId}", userId);
                    return false;
                }
                
                // Use the UserPresenceService to update status which will broadcast the change
                using var scope = _serviceProvider.CreateScope();
                var userPresenceService = scope.ServiceProvider.GetRequiredService<IUserPresenceService>();
                
                // This will update the database AND broadcast to all connected clients
                var status = isOnline ? UserPresenceStatus.Online : UserPresenceStatus.Offline;
                await userPresenceService.UpdateStatusAsync(userId, status);
                
                // Update last activity time
                await _userRepository.UpdateLastActivityAsync(userId, DateTime.UtcNow);
                
                // Update device info if provided
                if (isOnline && !string.IsNullOrEmpty(deviceInfo))
                {
                    string deviceName = "Unknown";
                    // Extract device name from user agent if possible
                    if (deviceInfo.Contains("Windows"))
                        deviceName = "Windows";
                    else if (deviceInfo.Contains("Android"))
                        deviceName = "Android";
                    else if (deviceInfo.Contains("iPhone") || deviceInfo.Contains("iPad"))
                        deviceName = "iOS";
                    else if (deviceInfo.Contains("Mac"))
                        deviceName = "Mac";
                    else if (deviceInfo.Contains("Linux"))
                        deviceName = "Linux";
                        
                    await _userRepository.UpdateCurrentDeviceAsync(userId, deviceInfo, deviceName);
                }
                
                // Set chat availability based on online status
                await _userRepository.SetAvailabilityForChatAsync(userId, isOnline);
                
                _logger.LogInformation("Updated user {UserId} presence status to {Status}", userId, status);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating presence for user {UserId}: {Message}", userId, ex.Message);
                return false;
            }
        }

        public async Task<bool> UpdateProfilePictureAsync(int userId, Stream imageStream, string contentType)
        {
            // Basic validation (optional, controller might do more)
            if (imageStream == null || imageStream.Length == 0)
            {
                _logger.LogWarning("UpdateProfilePictureAsync called with null or empty stream for user {UserId}.", userId);
                return false; // Or throw argument exception
            }

            // Consider adding content type validation here if needed (e.g., allow only jpeg/png)

            // Read stream into byte array
            byte[] pictureData;
            using (var memoryStream = new MemoryStream())
            {
                await imageStream.CopyToAsync(memoryStream);
                // Ensure stream is at the beginning if it was read elsewhere
                if (imageStream.CanSeek) 
                {
                    imageStream.Position = 0;
                }
                pictureData = memoryStream.ToArray();
            }

            // Consider size validation here
            // Example: Max 5MB
            if (pictureData.Length > 5 * 1024 * 1024) 
            {
                _logger.LogWarning("Profile picture too large for user {UserId}. Size: {Size} bytes.", userId, pictureData.Length);
                 throw new ValidationException("Profile picture cannot exceed 5MB.");
            }

            // Call repository to update the picture data
            return await _userRepository.UpdateProfilePictureAsync(userId, pictureData);
        }

        public async Task<IEnumerable<UserDto>> GetUsersByDepartmentAsync(string department)
        {
            if (string.IsNullOrWhiteSpace(department))
            {
                throw new ArgumentException("Department cannot be null or empty", nameof(department));
            }

            try
            {
                // Assuming the repository has or will have this method
                return await _userRepository.GetUsersByDepartmentAsync(department);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving users for department {Department}: {Message}", department, ex.Message);
                throw;
            }
        }
    }
}
