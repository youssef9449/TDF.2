using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TDFShared.DTOs.Common;
using TDFShared.DTOs.Users;
using TDFShared.Enums;
using TDFShared.Constants;

namespace TDFMAUI.Services
{
    public class UserPresenceApiService : IUserPresenceApiService
    {
        private readonly ApiService _apiService;
        private readonly ILogger<UserPresenceApiService> _logger;

        public UserPresenceApiService(ApiService apiService, ILogger<UserPresenceApiService> logger)
        {
            _apiService = apiService;
            _logger = logger;
        }

        public async Task<UserPresenceInfo> GetUserStatusAsync(int userId)
        {
            try
            {
                var response = await _apiService.GetAsync<UserPresenceInfo>($"users/{userId}/status");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching user status for user {UserId}", userId);
                return null;
            }
        }

        public async Task<PaginatedResult<UserPresenceInfo>> GetOnlineUsersAsync(int page = 1, int pageSize = 100)
        {
            try
            {
                var apiResponse = await _apiService.GetAsync<ApiResponse<PaginatedResult<UserDto>>>(
                    $"{ApiRoutes.Users.GetAllWithStatus}?pageNumber={page}&pageSize={pageSize}");

                if (apiResponse?.Success == true && apiResponse.Data?.Items != null)
                {
                    var items = new List<UserPresenceInfo>();
                    foreach (var user in apiResponse.Data.Items)
                    {
                        items.Add(new UserPresenceInfo
                        {
                            UserId = user.UserID,
                            Username = user.UserName,
                            FullName = user.FullName,
                            Department = user.Department,
                            Status = user.PresenceStatus,
                            StatusMessage = user.StatusMessage,
                            IsAvailableForChat = user.IsAvailableForChat ?? false,
                            ProfilePictureData = user.Picture,
                            LastActivityTime = user.LastActivityTime ?? DateTime.MinValue
                        });
                    }
                    return new PaginatedResult<UserPresenceInfo>(items, page, pageSize, apiResponse.Data.TotalCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching online users from API");
            }
            return new PaginatedResult<UserPresenceInfo>(new List<UserPresenceInfo>(), page, pageSize, 0);
        }

        public async Task UpdateUserConnectionStatusAsync(int userId, bool isConnected)
        {
            try
            {
                var updateData = new { isConnected };
                await _apiService.PutAsync<object, object>(
                    string.Format(ApiRoutes.Users.UpdateConnection, userId),
                    updateData,
                    false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update connection status via API for user {UserId}", userId);
            }
        }
    }
}
