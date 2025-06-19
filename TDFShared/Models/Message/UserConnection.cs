using System;

namespace TDFShared.Models.Message
{
    /// <summary>
    /// Represents a connection state of a user for message delivery
    /// </summary>
    public class UserConnection
    {
        /// <summary>
        /// Gets the user ID associated with this connection.
        /// </summary>
        public int UserId { get; }
        /// <summary>
        /// Gets a value indicating whether the user is online.
        /// </summary>
        public bool IsOnline { get; private set; }
        /// <summary>
        /// Gets the device information for the user connection.
        /// </summary>
        public string? DeviceInfo { get; private set; }
        /// <summary>
        /// Gets the last activity timestamp for the user connection.
        /// </summary>
        public DateTime LastActivity { get; private set; }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="UserConnection"/> class.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <param name="isOnline">Whether the user is online.</param>
        /// <param name="deviceInfo">The device information (optional).</param>
        public UserConnection(int userId, bool isOnline, string? deviceInfo = null)
        {
            if (userId <= 0)
                throw new ArgumentException("UserId must be positive", nameof(userId));
                
            UserId = userId;
            IsOnline = isOnline;
            DeviceInfo = deviceInfo;
            LastActivity = DateTime.UtcNow;
        }
        
        /// <summary>
        /// Updates the online status and last activity timestamp.
        /// </summary>
        /// <param name="isOnline">Whether the user is online.</param>
        public void UpdateOnlineStatus(bool isOnline)
        {
            IsOnline = isOnline;
            LastActivity = DateTime.UtcNow;
        }
        
        /// <summary>
        /// Updates the device information and last activity timestamp.
        /// </summary>
        /// <param name="deviceInfo">The device information.</param>
        public void UpdateDeviceInfo(string deviceInfo)
        {
            DeviceInfo = deviceInfo;
            LastActivity = DateTime.UtcNow;
        }
        
        /// <summary>
        /// Records the current time as the last activity timestamp.
        /// </summary>
        public void RecordActivity()
        {
            LastActivity = DateTime.UtcNow;
        }
    }
} 