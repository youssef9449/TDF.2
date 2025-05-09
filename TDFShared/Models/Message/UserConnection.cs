using System;

namespace TDFShared.Models.Message
{
    /// <summary>
    /// Represents a connection state of a user for message delivery
    /// </summary>
    public class UserConnection
    {
        public int UserId { get; }
        public bool IsOnline { get; private set; }
        public string? DeviceInfo { get; private set; }
        public DateTime LastActivity { get; private set; }
        
        public UserConnection(int userId, bool isOnline, string? deviceInfo = null)
        {
            if (userId <= 0)
                throw new ArgumentException("UserId must be positive", nameof(userId));
                
            UserId = userId;
            IsOnline = isOnline;
            DeviceInfo = deviceInfo;
            LastActivity = DateTime.UtcNow;
        }
        
        public void UpdateOnlineStatus(bool isOnline)
        {
            IsOnline = isOnline;
            LastActivity = DateTime.UtcNow;
        }
        
        public void UpdateDeviceInfo(string deviceInfo)
        {
            DeviceInfo = deviceInfo;
            LastActivity = DateTime.UtcNow;
        }
        
        public void RecordActivity()
        {
            LastActivity = DateTime.UtcNow;
        }
    }
} 