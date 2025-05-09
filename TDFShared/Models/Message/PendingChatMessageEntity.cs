using System;
using TDFShared.Enums;

namespace TDFShared.Models.Message
{
    /// <summary>
    /// Represents a pending message that has not yet been delivered
    /// </summary>
    public class PendingChatMessageEntity
    {
        public int NotificationID { get; set; }
        public int MessageID { get; set; }
        public int SenderID { get; set; }
        public int ReceiverID { get; set; }
        public string MessageText { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public bool IsRead { get; set; }
        public bool IsDelivered { get; set; }
        public string? Department { get; set; }
        public MessageType MessageType { get; set; } = MessageType.Chat;
        public DateTime CreatedAt { get; set; }
        public DateTime? DeliveredAt { get; set; }
    }
} 