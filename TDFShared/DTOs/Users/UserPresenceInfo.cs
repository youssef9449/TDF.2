using System;
using TDFShared.Enums;

namespace TDFShared.DTOs.Users
{
    /// <summary>
    /// Unified DTO representing a user's presence information across the system
    /// </summary>
    public class UserPresenceInfo
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public UserPresenceStatus Status { get; set; }
        public string? StatusMessage { get; set; }
        public bool IsAvailableForChat { get; set; }
        public DateTime LastActivity { get; set; }
        public string? Department { get; set; }
        public byte[]? ProfilePictureData { get; set; }

        // Connection details (from messaging service)
        public bool IsOnline { get; set; }
        public DateTime? LastSeen { get; set; }
        public string? ConnectionId { get; set; }
        public string? DeviceType { get; set; }

        // Compatibility for MAUI
        public DateTime LastActivityTime
        {
            get => LastActivity;
            set => LastActivity = value;
        }

        [Obsolete("Use UserId property instead")]
        public int Id => UserId;
    }
}
