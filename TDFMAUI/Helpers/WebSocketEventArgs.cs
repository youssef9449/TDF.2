using System;
using System.Collections.Generic;
using TDFShared.Enums;

namespace TDFMAUI.Helpers // Changed namespace
{
    public class ChatMessageEventArgs : EventArgs
    {
        public int MessageId { get; set; }
        public int SenderId { get; set; }
        public string SenderName { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsPending { get; set; }
    }

    public class MessageStatusEventArgs : EventArgs
    {
        public int RecipientId { get; set; }
        public List<int> MessageIds { get; set; } = new List<int>();
        public MessageStatus Status { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class ConnectionStatusEventArgs : EventArgs
    {
        public bool IsConnected { get; set; }
        public bool WasClean { get; set; }
        public DateTime Timestamp { get; set; }
        public bool ReconnectionFailed { get; set; }
    }

    public class UserStatusEventArgs : EventArgs
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public bool IsConnected { get; set; }
        public string MachineName { get; set; }
        public string PresenceStatus { get; set; }
        public string StatusMessage { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class UserAvailabilityEventArgs : EventArgs
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public bool IsAvailableForChat { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class AvailabilitySetEventArgs : EventArgs
    {
        public bool IsAvailable { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class StatusUpdateConfirmedEventArgs : EventArgs
    {
        public string Status { get; set; }
        public string StatusMessage { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class WebSocketErrorEventArgs : EventArgs
    {
        public string ErrorMessage { get; set; }
        public string ErrorCode { get; set; }
        public DateTime Timestamp { get; set; }
    }
} 