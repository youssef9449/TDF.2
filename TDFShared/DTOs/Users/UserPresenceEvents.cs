using System;
using TDFShared.Enums;

namespace TDFShared.DTOs.Users
{
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
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Optional status message
        /// </summary>
        public string? StatusMessage { get; set; }
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
        public string Username { get; set; } = string.Empty;

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
