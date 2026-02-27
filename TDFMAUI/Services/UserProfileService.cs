using System.Threading.Tasks;
using TDFShared.DTOs.Auth;
using TDFShared.DTOs.Users;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Graphics;
using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using System.IO;

namespace TDFMAUI.Services
{
    public class UserProfileService : IUserProfileService
    {
        private readonly ILogger<UserProfileService> _logger;
        private readonly ApiService _apiService;
        private readonly ILocalStorageService _localStorageService;
        private readonly IUserSessionService _userSessionService;

        public UserProfileService(ApiService apiService, ILocalStorageService localStorageService, ILogger<UserProfileService> logger, IUserSessionService userSessionService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            _localStorageService = localStorageService ?? throw new ArgumentNullException(nameof(localStorageService));
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));
            _logger.LogInformation("UserProfileService constructor finished.");
        }

        public async Task<UserDto?> GetUserProfileAsync(int userId)
        {
            try
            {
                return await _apiService.GetUserAsync(userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user profile for user {UserId}", userId);
                return null;
            }
        }

        public async Task<bool> UpdateUserProfileAsync(int userId, UpdateUserRequest updateUserDto)
        {
            try
            {
                var result = await _apiService.UpdateUserAsync(userId, updateUserDto);
                return result != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user profile for user {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> ChangePasswordAsync(int userId, ChangePasswordDto changePasswordDto)
        {
            try
            {
                return await _apiService.ChangePasswordAsync(changePasswordDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password for user {UserId}", userId);
                return false;
            }
        }

        public async Task<ImageSource?> GetProfilePictureAsync(int userId)
        {
            try
            {
                var response = await _apiService.GetUserProfileAsync(userId);
                if (response?.Success == true && response.Data?.ProfilePictureData != null && response.Data.ProfilePictureData.Length > 0)
                {
                    return ImageSource.FromStream(() => new MemoryStream(response.Data.ProfilePictureData));
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting profile picture for user {UserId}", userId);
                return null;
            }
        }

        public async Task<bool> UpdateProfilePictureAsync(int userId, byte[] newPictureData)
        {
            try
            {
                if (newPictureData == null || newPictureData.Length == 0) return false;

                using var stream = new MemoryStream(newPictureData);
                // Currently ApiService.UploadProfilePictureAsync handles the current user's profile picture
                return await _apiService.UploadProfilePictureAsync(stream, $"profile_{userId}.jpg", "image/jpeg");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile picture for user {UserId}", userId);
                return false;
            }
        }

        public UserDetailsDto? CurrentUser => _userSessionService.CurrentUserDetails;
        public bool IsLoggedIn => _userSessionService.IsLoggedIn;

        public void SetUserDetails(UserDetailsDto? userDetails)
        {
            _userSessionService.SetCurrentUserDetails(userDetails);
            _logger.LogInformation("User details set via UserSessionService: {UserName}", userDetails?.UserName);
        }

        public void ClearUserDetails()
        {
            _userSessionService.ClearUserData();
            _logger.LogInformation("User details cleared via UserSessionService");
        }

        public bool HasRole(string role)
        {
            return _userSessionService.HasRole(role);
        }
    }
}