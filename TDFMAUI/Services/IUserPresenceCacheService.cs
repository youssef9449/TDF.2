using System.Collections.Generic;
using TDFShared.DTOs.Users;

namespace TDFMAUI.Services
{
    public interface IUserPresenceCacheService
    {
        void UpdateUserStatus(int userId, UserPresenceInfo userInfo);
        bool TryGetUserStatus(int userId, out UserPresenceInfo userInfo);
        Dictionary<int, UserPresenceInfo> GetAllCachedUsers();
        void Clear();
        void UpdateBatch(Dictionary<int, UserPresenceInfo> users);
    }
}
