using TDFMAUI.Services;
using TDFShared.Enums;
using TDFMAUI.Helpers;

namespace TDFMAUI.Services
{
    public interface IUserPresenceService
    {
        Task<UserPresenceStatus> GetUserStatusAsync(int userId);
        Task<Dictionary<int, UserPresenceStatus>> GetUsersStatusAsync(IEnumerable<int> userIds);
        Task UpdateUserStatusAsync(int userId, UserPresenceStatus status);
        Task RecordUserActivityAsync(int userId);
        Task<Dictionary<int, UserPresenceInfo>> GetOnlineUsersAsync();
        Task UpdateStatusAsync(UserPresenceStatus status, string statusMessage = null);
        Task SetAvailabilityForChatAsync(bool isAvailable);
        UserPresenceStatus ParseStatus(string status);
        
        // Events
        event EventHandler<UserStatusChangedEventArgs> UserStatusChanged;
        event EventHandler<UserAvailabilityChangedEventArgs> UserAvailabilityChanged;
        event EventHandler<AvailabilitySetEventArgs> AvailabilityConfirmed;
        event EventHandler<StatusUpdateConfirmedEventArgs> StatusUpdateConfirmed;
        event EventHandler<WebSocketErrorEventArgs> PresenceErrorReceived;
    }
    
    public class UserPresenceInfo
    {
        public int UserId { get; set; }
        public string FullName { get; set; }
        public string Username { get; set; }
        public UserPresenceStatus Status { get; set; }
        public string StatusMessage { get; set; }
        public bool IsAvailableForChat { get; set; }
        public string Department { get; set; }
        public DateTime LastActivityTime { get; set; }
        public int Id => UserId; // ID property alias for backward compatibility
    }
    
    public class UserStatusChangedEventArgs : EventArgs
    {
        public int UserId { get; set; }
        public UserPresenceStatus Status { get; set; }
        public string Username { get; set; }
    }
    
    public class UserAvailabilityChangedEventArgs : EventArgs
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public bool IsAvailableForChat { get; set; }
        public DateTime Timestamp { get; set; }
    }
} 