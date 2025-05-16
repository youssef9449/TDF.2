using TDFShared.DTOs.Users;
using TDFShared.DTOs.Common;
using System.Collections.Generic;

namespace TDFAPI.Services
{
    public interface IUserService
    {
        Task<UserDto?> GetUserByIdAsync(int userId);
        Task<UserDto?> GetByUsernameAsync(string username);
        Task<UserDto?> GetUserDtoByIdAsync(int userId);
        Task<PaginatedResult<UserDto>> GetPaginatedAsync(int page, int pageSize);
        Task<int> CreateAsync(CreateUserRequest userDto);
        Task<bool> UpdateAsync(int userId, UpdateUserRequest userDto);
        Task<bool> UpdateSelfAsync(int userId, UpdateMyProfileRequest dto);
        Task<bool> DeleteAsync(int userId);
        Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword);
        Task<bool> InvalidateRefreshTokenAsync(int userId);
        Task<bool> UpdateUserPresenceAsync(int userId, bool isOnline, string? deviceInfo = null);
        Task<bool> UpdateProfilePictureAsync(int userId, Stream imageStream, string contentType);
        Task<IEnumerable<UserDto>> GetUsersByDepartmentAsync(string department);
        Task<IEnumerable<UserDto>> GetOnlineUsersAsync(); // New method for online users
        Task<IEnumerable<UserDto>> GetAllUsersWithPresenceAsync(); // Method to get all users with their presence
    }
}