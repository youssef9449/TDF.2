using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TDFShared.DTOs.Common;
using TDFShared.DTOs.Users;

namespace TDFMAUI.Services
{
    public interface IUserPresenceApiService
    {
        Task<UserPresenceInfo> GetUserStatusAsync(int userId);
        Task<PaginatedResult<UserPresenceInfo>> GetOnlineUsersAsync(int page = 1, int pageSize = 100);
        Task UpdateUserConnectionStatusAsync(int userId, bool isConnected);
    }
}
