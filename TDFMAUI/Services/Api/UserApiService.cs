using TDFShared.Constants;
using TDFShared.DTOs.Common;
using TDFShared.DTOs.Users;
using TDFShared.Services;

namespace TDFMAUI.Services.Api
{
    public class UserApiService : IUserApiService
    {
        private readonly IHttpClientService _httpClientService;
        private readonly ILogger<UserApiService> _logger;

        public UserApiService(
            IHttpClientService httpClientService,
            ILogger<UserApiService> logger)
        {
            _httpClientService = httpClientService;
            _logger = logger;
        }

        public async Task<ApiResponse<UserDto>> GetCurrentUserAsync()
        {
            try
            {
                var response = await _httpClientService.GetAsync<ApiResponse<UserDto>>(ApiRoutes.Users.GetCurrent);
                return response ?? new ApiResponse<UserDto> { Success = false, Message = "Failed to get current user" };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "UserApiService: Failed to get current user: {Message}", ex.Message);
                return new ApiResponse<UserDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<UserDto>> GetCurrentAsync() => await GetCurrentUserAsync();

        public async Task<ApiResponse<int>> GetCurrentUserIdAsync()
        {
            var userResponse = await GetCurrentUserAsync();
            if (userResponse.Success && userResponse.Data != null)
                return new ApiResponse<int> { Success = true, Data = userResponse.Data.UserID };
            return new ApiResponse<int> { Success = false, Message = userResponse.Message };
        }

        public async Task<ApiResponse<UserProfileDto>> GetUserProfileAsync(int userId)
        {
            try
            {
                string endpoint = string.Format(ApiRoutes.Users.GetById, userId);
                var response = await _httpClientService.GetAsync<ApiResponse<UserProfileDto>>(endpoint);
                return response ?? new ApiResponse<UserProfileDto> { Success = false, Message = "Failed to get user profile" };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "UserApiService: Failed to get user profile: {Message}", ex.Message);
                return new ApiResponse<UserProfileDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<UserDto> GetUserByIdAsync(int userId)
        {
            string endpoint = string.Format(ApiRoutes.Users.GetById, userId);
            var response = await _httpClientService.GetAsync<ApiResponse<UserDto>>(endpoint);
            if (response == null || !response.Success)
                throw new Exception(response?.Message ?? "User not found");
            return response.Data!;
        }

        public async Task<PaginatedResult<UserDto>> GetAllUsersAsync(int page = 1, int pageSize = 10)
        {
            string endpoint = $"{ApiRoutes.Users.Base}?pageNumber={page}&pageSize={pageSize}";
            var response = await _httpClientService.GetAsync<ApiResponse<PaginatedResult<UserDto>>>(endpoint);
            if (response == null || !response.Success)
                throw new Exception(response?.Message ?? "Failed to get users");
            return response.Data!;
        }

        public async Task<List<UserDto>> GetUsersByDepartmentAsync(string department)
        {
            string endpoint = string.Format(ApiRoutes.Users.GetByDepartment, Uri.EscapeDataString(department));
            var response = await _httpClientService.GetAsync<ApiResponse<IEnumerable<UserDto>>>(endpoint);
            if (response == null || !response.Success)
                throw new Exception(response?.Message ?? "Failed to get users by department");
            return response.Data?.ToList() ?? new List<UserDto>();
        }

        public async Task<UserDto> CreateUserAsync(CreateUserRequest userRequest)
        {
            var response = await _httpClientService.PostAsync<CreateUserRequest, ApiResponse<UserDto>>(ApiRoutes.Users.Base, userRequest);
            if (response == null || !response.Success)
                throw new Exception(response?.Message ?? "Failed to create user");
            return response.Data!;
        }

        public async Task<UserDto> UpdateUserAsync(int userId, UpdateUserRequest userDto)
        {
            string endpoint = string.Format(ApiRoutes.Users.GetById, userId);
            var response = await _httpClientService.PutAsync<UpdateUserRequest, ApiResponse<UserDto>>(endpoint, userDto);
            if (response == null || !response.Success)
                throw new Exception(response?.Message ?? "Failed to update user");
            return response.Data!;
        }

        public async Task DeleteUserAsync(int userId)
        {
            string endpoint = string.Format(ApiRoutes.Users.GetById, userId);
            var response = await _httpClientService.DeleteAsync(endpoint);
            if (!response.IsSuccessStatusCode)
                throw new Exception($"Failed to delete user: {response.ReasonPhrase}");
        }

        public async Task<bool> ChangePasswordAsync(ChangePasswordDto changePasswordDto)
        {
            var response = await _httpClientService.PostAsync<ChangePasswordDto, ApiResponse<bool>>(ApiRoutes.Users.ChangePassword, changePasswordDto);
            return response?.Success ?? false;
        }

        public async Task<bool> UploadProfilePictureAsync(Stream imageStream, string fileName, string contentType)
        {
            // Placeholder for multipart upload - usually handled in ApiService facade or specialized helper
            return false;
        }

        public async Task<bool> UpdateUserProfileAsync(UpdateMyProfileRequest profileData)
        {
            var response = await _httpClientService.PutAsync<UpdateMyProfileRequest, ApiResponse<bool>>(ApiRoutes.Users.UpdateMyProfile, profileData);
            return response?.Success ?? false;
        }

        public async Task<UserPresenceInfo> GetUserStatusAsync(int userId)
        {
            string endpoint = string.Format(ApiRoutes.Users.GetStatus, userId);
            return await _httpClientService.GetAsync<UserPresenceInfo>(endpoint);
        }

        public async Task<PaginatedResult<UserPresenceInfo>> GetOnlineUsersPresenceAsync(int page = 1, int pageSize = 100)
        {
            string endpoint = $"{ApiRoutes.Users.GetOnline}?pageNumber={page}&pageSize={pageSize}";
            return await _httpClientService.GetAsync<PaginatedResult<UserPresenceInfo>>(endpoint);
        }

        public async Task UpdateUserConnectionStatusAsync(int userId, bool isConnected)
        {
            string endpoint = string.Format(ApiRoutes.Users.UpdateConnection, userId);
            await _httpClientService.PutAsync<object, object>(endpoint, new { isConnected });
        }
    }
}
