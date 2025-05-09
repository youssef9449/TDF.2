using System;
using TDFShared.Enums;
using TDFAPI.Messaging.Interfaces;

namespace TDFAPI.Messaging
{
    /// <summary>
    /// Event raised when a user's status changes
    /// </summary>
    public class UserStatusChangedEvent : IEvent
    {
        public int UserId { get; }
        public string Username { get; }
        public string FullName { get; }
        public UserPresenceStatus Status { get; }
        public string StatusMessage { get; }
        public DateTime Timestamp { get; }

        public UserStatusChangedEvent(int userId, string username, string fullName, 
            UserPresenceStatus status, string statusMessage)
        {
            UserId = userId;
            Username = username;
            FullName = fullName;
            Status = status;
            StatusMessage = statusMessage;
            Timestamp = DateTime.UtcNow;
        }
        
        /// <summary>
        /// Simplified constructor for when only user ID and status are known
        /// </summary>
        public UserStatusChangedEvent(int userId, UserPresenceStatus status, string statusMessage = null)
        {
            UserId = userId;
            Status = status;
            StatusMessage = statusMessage;
            Username = string.Empty;
            FullName = string.Empty;
            Timestamp = DateTime.UtcNow;
        }
    }
    
    /// <summary>
    /// Event raised when a user's availability for chat changes
    /// </summary>
    public class UserAvailabilityChangedEvent : IEvent
    {
        public int UserId { get; }
        public string Username { get; }
        public string FullName { get; }
        public bool IsAvailableForChat { get; }
        public DateTime Timestamp { get; }

        public UserAvailabilityChangedEvent(int userId, string username, string fullName, 
            bool isAvailableForChat)
        {
            UserId = userId;
            Username = username;
            FullName = fullName;
            IsAvailableForChat = isAvailableForChat;
            Timestamp = DateTime.UtcNow;
        }
        
        /// <summary>
        /// Simplified constructor for when only user ID and availability are known
        /// </summary>
        public UserAvailabilityChangedEvent(int userId, bool isAvailableForChat)
        {
            UserId = userId;
            IsAvailableForChat = isAvailableForChat;
            Username = string.Empty;
            FullName = string.Empty;
            Timestamp = DateTime.UtcNow;
        }
    }
    
    /// <summary>
    /// Event raised when a user activity ping is received
    /// </summary>
    public class UserActivityPingEvent : IEvent
    {
        public int UserId { get; }
        public DateTime Timestamp { get; }
        
        public UserActivityPingEvent(int userId)
        {
            UserId = userId;
            Timestamp = DateTime.UtcNow;
        }
    }
} 