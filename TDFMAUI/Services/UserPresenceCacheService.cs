using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using TDFShared.DTOs.Users;

namespace TDFMAUI.Services
{
    public class UserPresenceCacheService : IUserPresenceCacheService
    {
        private readonly ConcurrentDictionary<int, UserPresenceInfo> _userStatuses = new();

        public void UpdateUserStatus(int userId, UserPresenceInfo userInfo)
        {
            _userStatuses[userId] = userInfo;
        }

        public bool TryGetUserStatus(int userId, out UserPresenceInfo userInfo)
        {
            return _userStatuses.TryGetValue(userId, out userInfo);
        }

        public Dictionary<int, UserPresenceInfo> GetAllCachedUsers()
        {
            return _userStatuses.ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        public void Clear()
        {
            _userStatuses.Clear();
        }

        public void UpdateBatch(Dictionary<int, UserPresenceInfo> users)
        {
            foreach (var kvp in users)
            {
                _userStatuses[kvp.Key] = kvp.Value;
            }
        }
    }
}
