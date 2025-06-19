using System.Threading.Tasks;
using TDFShared.DTOs.Users; // For UpdateUserRequest, ChangePasswordDto
using TDFShared.DTOs.Auth; // Added for UserDetailsDto
using Microsoft.Maui.Graphics; // For ImageSource
using System;
using Microsoft.Maui.Controls;

namespace TDFMAUI.Services
{
    public class UserDetailsChangedEventArgs : EventArgs
    {
        public UserDto? UserDetails { get; }
        public UserDetailsChangedEventArgs(UserDto? userDetails)
        {
            UserDetails = userDetails;
        }
    }

    public interface IUserProfileService
    {
        UserDetailsDto? CurrentUser { get; }
        bool IsLoggedIn { get; }

        Task<UserDto?> GetUserProfileAsync(int userId);
        Task<bool> UpdateUserProfileAsync(int userId, UpdateUserRequest updateUserDto); // Corrected DTO
        Task<bool> ChangePasswordAsync(int userId, ChangePasswordDto changePasswordDto);
        Task<ImageSource?> GetProfilePictureAsync(int userId);
        Task<bool> UpdateProfilePictureAsync(int userId, byte[] newPictureData);
        
        void SetUserDetails(UserDetailsDto? userDetails);
        void ClearUserDetails();

        // event EventHandler<UserDetailsChangedEventArgs>? UserDetailsChanged; // Kept commented as it was unused
    }
} 