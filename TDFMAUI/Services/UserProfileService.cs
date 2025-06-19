using System.Threading.Tasks;
using TDFShared.DTOs.Auth;
using TDFShared.DTOs.Users;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Graphics;
using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;

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

        public Task<UserDto?> GetUserProfileAsync(int userId) => throw new NotImplementedException();
        public Task<bool> UpdateUserProfileAsync(int userId, UpdateUserRequest updateUserDto) => throw new NotImplementedException();
        public Task<bool> ChangePasswordAsync(int userId, ChangePasswordDto changePasswordDto) => throw new NotImplementedException();
        public Task<ImageSource?> GetProfilePictureAsync(int userId) => throw new NotImplementedException();
        public Task<bool> UpdateProfilePictureAsync(int userId, byte[] newPictureData) => throw new NotImplementedException();

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