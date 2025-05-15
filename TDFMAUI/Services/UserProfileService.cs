using System.Threading.Tasks;
using TDFShared.DTOs.Auth;
using TDFShared.DTOs.Users;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Graphics;
using System;
using System.Collections.Generic;

namespace TDFMAUI.Services
{
    public class UserProfileService : IUserProfileService
    {
        private UserDetailsDto? _currentUser;
        private readonly ILogger<UserProfileService> _logger;
        private readonly ApiService _apiService;
        private readonly ILocalStorageService _localStorageService;

        public UserProfileService(ApiService apiService, ILocalStorageService localStorageService, ILogger<UserProfileService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            _localStorageService = localStorageService ?? throw new ArgumentNullException(nameof(localStorageService));
            _logger.LogInformation("UserProfileService constructor finished.");
        }

        public Task<UserDto?> GetUserProfileAsync(int userId) => throw new NotImplementedException();
        public Task<bool> UpdateUserProfileAsync(int userId, UpdateUserRequest updateUserDto) => throw new NotImplementedException();
        public Task<bool> ChangePasswordAsync(int userId, ChangePasswordDto changePasswordDto) => throw new NotImplementedException();
        public Task<ImageSource?> GetProfilePictureAsync(int userId) => throw new NotImplementedException();
        public Task<bool> UpdateProfilePictureAsync(int userId, byte[] newPictureData) => throw new NotImplementedException();

        public UserDetailsDto? CurrentUser => _currentUser;
        public bool IsLoggedIn => _currentUser != null;

        public void SetUserDetails(UserDetailsDto? userDetails)
        {
            _currentUser = userDetails;
            // Optionally raise an event here if other parts of the app need to react to login/logout
        }

        public void ClearUserDetails()
        {
            _currentUser = null;
            // Optionally raise event
        }

        public bool HasRole(string role)
        {
            if (_currentUser?.Roles == null || string.IsNullOrEmpty(role)) return false;
            return _currentUser.Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
        }
    }
}