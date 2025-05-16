using TDFMAUI.Services;
using TDFShared.Enums;
using TDFMAUI.Helpers;

namespace TDFMAUI.Services
{
    /// <summary>
    /// Service to manage user presence status and availability
    /// </summary>
    public interface IUserPresenceService
    {
        /// <summary>
        /// Gets the current presence status of a user
        /// </summary>
        Task<UserPresenceStatus> GetUserStatusAsync(int userId);
        
        /// <summary>
        /// Gets current status of multiple users
        /// </summary>
        Task<Dictionary<int, UserPresenceStatus>> GetUsersStatusAsync(IEnumerable<int> userIds);
        
        /// <summary>
        /// Updates a user's presence status
        /// </summary>
        Task UpdateUserStatusAsync(int userId, UserPresenceStatus status);
        
        /// <summary>
        /// Records user activity to update last activity timestamp
        /// </summary>
        Task RecordUserActivityAsync(int userId);
        
        /// <summary>
        /// Gets information about all currently online users
        /// </summary>
        Task<Dictionary<int, UserPresenceInfo>> GetOnlineUsersAsync();
        
        /// <summary>
        /// Gets cached information about online users for offline usage
        /// </summary>
        Dictionary<int, UserPresenceInfo> GetCachedOnlineUsers();
        
        /// <summary>
        /// Updates the current user's status and optional status message
        /// </summary>
        Task UpdateStatusAsync(UserPresenceStatus status, string statusMessage = null);
        
        /// <summary>
        /// Sets whether the current user is available for chat
        /// </summary>
        Task SetAvailabilityForChatAsync(bool isAvailable);
        
        /// <summary>
        /// Parses a string status name to the enum value
        /// </summary>
        UserPresenceStatus ParseStatus(string status);

        // Events
        /// <summary>
        /// Fired when any user's status changes
        /// </summary>
        event EventHandler<UserStatusChangedEventArgs> UserStatusChanged;
        
        /// <summary>
        /// Fired when any user's chat availability changes
        /// </summary>
        event EventHandler<UserAvailabilityChangedEventArgs> UserAvailabilityChanged;
        
        /// <summary>
        /// Fired when current user's availability change is confirmed
        /// </summary>
        event EventHandler<AvailabilitySetEventArgs> AvailabilityConfirmed;
        
        /// <summary>
        /// Fired when current user's status update is confirmed
        /// </summary>
        event EventHandler<StatusUpdateConfirmedEventArgs> StatusUpdateConfirmed;
        
        /// <summary>
        /// Fired when a presence-related error occurs
        /// </summary>
        event EventHandler<WebSocketErrorEventArgs> PresenceErrorReceived;
    }

    /// <summary>
    /// Information about a user's presence in the system
    /// </summary>
    public class UserPresenceInfo
    {
        /// <summary>
        /// Unique identifier of the user
        /// </summary>
        public int UserId { get; set; }
        
        /// <summary>
        /// User's full display name
        /// </summary>
        public string FullName { get; set; }
        
        /// <summary>
        /// User's login username
        /// </summary>
        public string Username { get; set; }
        
        /// <summary>
        /// Current presence status
        /// </summary>
        public UserPresenceStatus Status { get; set; }
        
        /// <summary>
        /// Optional custom status message set by the user
        /// </summary>
        public string StatusMessage { get; set; }
        
        /// <summary>
        /// Whether the user is available for chat
        /// </summary>
        public bool IsAvailableForChat { get; set; }
        
        /// <summary>
        /// User's department or team
        /// </summary>
        public string Department { get; set; }
        
        /// <summary>
        /// Timestamp of the user's last activity
        /// </summary>
        public DateTime LastActivityTime { get; set; }
        
        /// <summary>
        /// Binary data of the user's profile picture
        /// </summary>
        public byte[] ProfilePictureData { get; set; }
        
        /// <summary>
        /// ID property alias for backward compatibility
        /// </summary>
        [Obsolete("Use UserId property instead")]
        public int Id => UserId;
    }

    /// <summary>
    /// Event args for user status changes
    /// </summary>
    public class UserStatusChangedEventArgs : EventArgs
    {
        /// <summary>
        /// ID of the user whose status changed
        /// </summary>
        public int UserId { get; set; }
        
        /// <summary>
        /// New status of the user
        /// </summary>
        public UserPresenceStatus Status { get; set; }
        
        /// <summary>
        /// Username of the user
        /// </summary>
        public string Username { get; set; }
    }

    /// <summary>
    /// Event args for user chat availability changes
    /// </summary>
    public class UserAvailabilityChangedEventArgs : EventArgs
    {
        /// <summary>
        /// ID of the user whose availability changed
        /// </summary>
        public int UserId { get; set; }
        
        /// <summary>
        /// Username of the user
        /// </summary>
        public string Username { get; set; }
        
        /// <summary>
        /// Whether the user is now available for chat
        /// </summary>
        public bool IsAvailableForChat { get; set; }
        
        /// <summary>
        /// Timestamp of the change
        /// </summary>
        public DateTime Timestamp { get; set; }
    }
}