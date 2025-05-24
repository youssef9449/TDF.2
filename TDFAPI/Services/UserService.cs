using TDFAPI.Repositories;
using TDFShared.Enums;
using System;
using TDFShared.DTOs.Users;
using TDFShared.DTOs.Common;
using System.ComponentModel.DataAnnotations;
using TDFShared.Services;
using TDFShared.Validation;
using TDFShared.DTOs.Auth;
using TDFShared.Models.User;

namespace TDFAPI.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly IAuthService _authService;
        private readonly ILogger<UserService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IBusinessRulesService _businessRulesService;

        public UserService(
            IUserRepository userRepository, 
            IAuthService authService, 
            ILogger<UserService> logger, 
            IServiceProvider serviceProvider,
            IBusinessRulesService businessRulesService)
        {
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _businessRulesService = businessRulesService ?? throw new ArgumentNullException(nameof(businessRulesService));
        }

        public async Task<UserDto?> GetUserByIdAsync(int userId)
        {
            try
            {
                return await _userRepository.GetByIdAsync(userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user with ID {UserId}", userId);
                throw;
            }
        }

        public async Task<UserDto?> GetByUsernameAsync(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentException("Username cannot be null or empty", nameof(username));
            }

            try
            {
                return await _userRepository.GetByUsernameAsync(username);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user with username {Username}", username);
                throw;
            }
        }

        public async Task<PaginatedResult<UserDto>> GetPaginatedAsync(int page, int pageSize)
        {
            if (page < 1)
            {
                throw new ArgumentException("Page number must be greater than 0", nameof(page));
            }
            if (pageSize < 1)
            {
                throw new ArgumentException("Page size must be greater than 0", nameof(pageSize));
            }

            try
            {
                return await _userRepository.GetPaginatedAsync(page, pageSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving paginated users. Page: {Page}, PageSize: {PageSize}", page, pageSize);
                throw;
            }
        }

        public async Task<int> CreateAsync(CreateUserRequest userDto)
        {
            if (userDto == null)
            {
                throw new ArgumentNullException(nameof(userDto));
            }

            try
            {
                // Basic validation
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

                // Check if full name already exists
                var isFullNameTaken = await _userRepository.IsFullNameTakenAsync(userDto.FullName);
                if (isFullNameTaken)
                {
                    _logger.LogWarning("User creation failed: Full name {FullName} already exists", userDto.FullName);
                    throw new ValidationException($"Full name '{userDto.FullName}' is already taken.");
                }

                // Create business rule context
                var context = new BusinessRuleContext
                {
                    UsernameExistsAsync = async (username) => 
                    {
                        var user = await _userRepository.GetByUsernameAsync(username);
                        return user != null;
                    }
                };

                // Validate business rules
                var businessRuleResult = await _businessRulesService.ValidateUserCreationAsync(userDto, context);
                if (!businessRuleResult.IsValid)
                {
                    throw new ValidationException(string.Join("; ", businessRuleResult.Errors));
                }

                // Hash password
                var passwordHash = _authService.HashPassword(userDto.Password, out string salt);

                // Create user
                return await _userRepository.CreateAsync(userDto, passwordHash, salt);
            }
            catch (ValidationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user {Username}", userDto.Username);
                throw new InvalidOperationException("Failed to create user. Please try again later.", ex);
            }
        }

        public async Task<bool> UpdateAsync(int userId, UpdateUserRequest userDto)
        {
            if (userDto == null)
            {
                throw new ArgumentNullException(nameof(userDto));
            }

            try
            {
                // If full name is being updated, check if it's already taken
                if (!string.IsNullOrWhiteSpace(userDto.FullName))
                {
                    var isFullNameTaken = await _userRepository.IsFullNameTakenAsync(userDto.FullName, userId);
                    if (isFullNameTaken)
                    {
                        _logger.LogWarning("User update failed: Full name {FullName} already exists", userDto.FullName);
                        throw new ValidationException($"Full name '{userDto.FullName}' is already taken.");
                    }
                }

                return await _userRepository.UpdateAsync(userId, userDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> UpdateSelfAsync(int userId, UpdateMyProfileRequest dto)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            try
            {
                // If full name is being updated, check if it's already taken
                if (!string.IsNullOrWhiteSpace(dto.FullName))
                {
                    var isFullNameTaken = await _userRepository.IsFullNameTakenAsync(dto.FullName, userId);
                    if (isFullNameTaken)
                    {
                        _logger.LogWarning("User self-update failed: Full name {FullName} already exists", dto.FullName);
                        throw new ValidationException($"Full name '{dto.FullName}' is already taken.");
                    }
                }

                return await _userRepository.UpdateSelfAsync(userId, dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating self profile for user {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> DeleteAsync(int userId)
        {
            try
            {
                return await _userRepository.DeleteAsync(userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(currentPassword))
            {
                throw new ArgumentException("Current password cannot be null or empty", nameof(currentPassword));
            }
            if (string.IsNullOrWhiteSpace(newPassword))
            {
                throw new ArgumentException("New password cannot be null or empty", nameof(newPassword));
            }

            try
            {
                var user = await _userRepository.GetUserAuthDataAsync(userId);
                if (user == null || string.IsNullOrEmpty(user.PasswordHash) || string.IsNullOrEmpty(user.PasswordSalt))
                {
                    _logger.LogWarning("Password change failed: User {UserId} not found or has invalid auth data", userId);
                    return false;
                }

                if (!_authService.VerifyPassword(currentPassword, user.PasswordHash, user.PasswordSalt))
                {
                    _logger.LogWarning("Password change failed: Invalid current password for user {UserId}", userId);
                    return false;
                }

                var newPasswordHash = _authService.HashPassword(newPassword, out string newSalt);
                return await _userRepository.ChangePasswordAsync(userId, newPasswordHash, newSalt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password for user {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> InvalidateRefreshTokenAsync(int userId)
        {
            try
            {
                return await _userRepository.UpdateRefreshTokenAsync(userId, null, DateTime.MinValue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to invalidate refresh token for user {UserId}", userId);
                throw;
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
                var status = isOnline ? UserPresenceStatus.Online : UserPresenceStatus.Offline;
                return await _userRepository.UpdatePresenceStatusAsync(userId, status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user presence for {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> UpdateProfilePictureAsync(int userId, Stream imageStream, string contentType)
        {
            if (imageStream == null)
            {
                throw new ArgumentNullException(nameof(imageStream));
            }
            if (string.IsNullOrWhiteSpace(contentType))
            {
                throw new ArgumentException("Content type cannot be null or empty", nameof(contentType));
            }

            try
            {
                using var memoryStream = new MemoryStream();
                await imageStream.CopyToAsync(memoryStream);
                var imageData = memoryStream.ToArray();

                // Validate image size (e.g., max 5MB)
                if (imageData.Length > 5 * 1024 * 1024)
                {
                    throw new ValidationException("Profile picture cannot exceed 5MB");
                }

                return await _userRepository.UpdateProfilePictureAsync(userId, imageData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile picture for user {UserId}", userId);
                throw;
            }
        }

        public async Task<IEnumerable<UserDto>> GetUsersByDepartmentAsync(string department)
        {
            if (string.IsNullOrWhiteSpace(department))
            {
                throw new ArgumentException("Department cannot be null or empty", nameof(department));
            }

            try
            {
                return await _userRepository.GetUsersByDepartmentAsync(department);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving users for department {Department}", department);
                throw;
            }
        }

        public async Task<IEnumerable<UserDto>> GetOnlineUsersAsync()
        {
            try
            {
                return await _userRepository.GetOnlineUsersAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving online users");
                throw;
            }
        }

        public async Task<IEnumerable<UserDto>> GetAllUsersWithPresenceAsync()
        {
            try
            {
                return await _userRepository.GetAllAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all users with presence");
                throw;
            }
        }

        public async Task UpdateUserDeviceInfoAsync(int userId, string deviceId, string userAgent)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                throw new ArgumentException("Device ID cannot be null or empty", nameof(deviceId));
            }
            if (string.IsNullOrWhiteSpace(userAgent))
            {
                throw new ArgumentException("User agent cannot be null or empty", nameof(userAgent));
            }

            try
            {
                await _userRepository.UpdateUserDeviceInfoAsync(userId, deviceId, userAgent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating device info for user {UserId}", userId);
                throw;
            }
        }
    }
}
