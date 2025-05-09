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
            // Optionally raise an event here if other parts of the app need to react to login/logout
        }

        public void ClearUserDetails()
        {
            _currentUser = null;
            // Optionally raise event
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