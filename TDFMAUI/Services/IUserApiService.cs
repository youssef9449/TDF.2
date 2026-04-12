using System.Collections.Generic;
using System.Threading.Tasks;
using TDFShared.DTOs.Common;
using TDFShared.DTOs.Users;

namespace TDFMAUI.Services
{
    public interface IUserApiService
    {
        Task<ApiResponse<UserDto>> GetCurrentUserAsync();
        Task<ApiResponse<int>> GetCurrentUserIdAsync();
        Task<ApiResponse<UserProfileDto>> GetUserProfileAsync(int userId);
        Task<UserDto> GetUserByIdAsync(int userId);
        Task<PaginatedResult<UserDto>> GetAllUsersAsync(int page = 1, int pageSize = 10);
        Task<List<UserDto>> GetUsersByDepartmentAsync(string department);
        Task<UserDto> CreateUserAsync(CreateUserRequest userRequest);
        Task<UserDto> UpdateUserAsync(int userId, UpdateUserRequest userDto);
        Task DeleteUserAsync(int userId);
        Task<bool> ChangePasswordAsync(ChangePasswordDto changePasswordDto);
        Task<bool> UploadProfilePictureAsync(Stream imageStream, string fileName, string contentType);
        Task<bool> UpdateUserProfileAsync(UpdateMyProfileRequest profileData);
        Task<TDFShared.DTOs.Users.UserPresenceInfo> GetUserStatusAsync(int userId);
        Task<PaginatedResult<TDFShared.DTOs.Users.UserPresenceInfo>> GetOnlineUsersPresenceAsync(int page = 1, int pageSize = 100);
        Task UpdateUserConnectionStatusAsync(int userId, bool isConnected);
    }
}
