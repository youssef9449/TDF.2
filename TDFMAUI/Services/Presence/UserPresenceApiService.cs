using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TDFShared.DTOs.Common;
using TDFShared.DTOs.Users;
using TDFShared.Enums;
using TDFShared.Constants;

namespace TDFMAUI.Services.Presence
{
    public class UserPresenceApiService : IUserPresenceApiService
    {
        private readonly IUserApiService _userApiService;
        private readonly ILogger<UserPresenceApiService> _logger;

        public UserPresenceApiService(IUserApiService userApiService, ILogger<UserPresenceApiService> logger)
        {
            _userApiService = userApiService ?? throw new ArgumentNullException(nameof(userApiService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<UserPresenceInfo> GetUserStatusAsync(int userId)
        {
            try
            {
                return await _userApiService.GetUserStatusAsync(userId);
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
                return await _userApiService.GetOnlineUsersPresenceAsync(page, pageSize);
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
                await _userApiService.UpdateUserConnectionStatusAsync(userId, isConnected);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update connection status via API for user {UserId}", userId);
            }
        }
    }
}
