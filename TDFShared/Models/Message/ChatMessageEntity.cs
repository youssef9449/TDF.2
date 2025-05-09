using System;
using TDFShared.Enums;

namespace TDFShared.Models.Message
{
    /// <summary>
    /// Represents a chat message with sender and receiver details
    /// </summary>
    public class ChatMessageEntity
    {
        public int MessageID { get; set; }
        public int SenderID { get; set; }
        public int ReceiverID { get; set; }
        public string MessageText { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public bool IsRead { get; set; }
        public bool IsDelivered { get; set; }
        public string? Department { get; set; }
        public MessageType MessageType { get; set; } = MessageType.Chat;
        public string SenderFullName { get; set; } = string.Empty;
        public string ReceiverFullName { get; set; } = string.Empty;
        
        /// <summary>
        /// Indicates if the message is a global broadcast (ReceiverID = 0)
        /// </summary>
        public bool IsGlobal { get; set; }
    }
} 