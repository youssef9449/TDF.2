using System.Collections.Generic;
using TDFShared.DTOs.Auth; // Use the new DTO
using TDFShared.DTOs.Users;
using Microsoft.Extensions.Logging;

namespace TDFMAUI.Services
{
    public interface IUserProfileService
    {
        UserDetailsDto? CurrentUser { get; }
        bool IsLoggedIn { get; }
        void SetUserDetails(UserDetailsDto? userDetails);
        void ClearUserDetails();
        // Add helper methods if needed (e.g., HasRole(string role))
        bool HasRole(string role);
    }

    public class UserProfileService : IUserProfileService
    {
        private UserDetailsDto? _currentUser;
        private readonly ILogger<UserProfileService> _logger;

        public event EventHandler<UserDetailsDto> UserDetailsChanged;

        public UserProfileService(ILogger<UserProfileService> logger)
        {
            // Log constructor entry
            logger?.LogInformation("UserProfileService constructor started.");

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Log constructor exit
            logger?.LogInformation("UserProfileService constructor finished.");
        }

        public UserDetailsDto? CurrentUser => _currentUser;

        public bool IsLoggedIn => _currentUser != null;

        public void SetUserDetails(UserDetailsDto? userDetails)
        {
            _currentUser = userDetails;

            // Convert UserDetailsDto to UserDto for App.CurrentUser
            if (userDetails != null)
            {
                var userDto = new UserDto
                {
                    UserID = userDetails.UserId,
                    Username = userDetails.UserName,
                    FullName = userDetails.FullName,
                    Department = userDetails.Department,
                    // Set other properties as needed
                };

                // Set App.CurrentUser
                App.CurrentUser = userDto;
                _logger.LogInformation("Set App.CurrentUser to {UserId} ({FullName})", userDto.UserID, userDto.FullName);
            }

            // Raise the event
            UserDetailsChanged?.Invoke(this, userDetails);
        }

        public void ClearUserDetails()
        {
            _currentUser = null;

            // Clear App.CurrentUser
            App.CurrentUser = null;
            _logger.LogInformation("Cleared App.CurrentUser");

            // Raise event
            UserDetailsChanged?.Invoke(this, null);
        }

        public bool HasRole(string role)
        {
            if (_currentUser?.Roles == null || string.IsNullOrEmpty(role))
            {
                return false;
            }
            // Case-insensitive role check
            return _currentUser.Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
        }
    }
}