using System;
using TDFShared.Enums;

namespace TDFShared.Models.Message
{
    /// <summary>
    /// Represents a pending message that has not yet been delivered
    /// </summary>
    public class PendingChatMessageEntity
    {
        /// <summary>
        /// Gets or sets the notification ID associated with the pending message.
        /// </summary>
        public int NotificationID { get; set; }
        /// <summary>
        /// Gets or sets the message ID.
        /// </summary>
        public int MessageID { get; set; }
        /// <summary>
        /// Gets or sets the sender's user ID.
        /// </summary>
        public int SenderID { get; set; }
        /// <summary>
        /// Gets or sets the receiver's user ID.
        /// </summary>
        public int ReceiverID { get; set; }
        /// <summary>
        /// Gets or sets the message text.
        /// </summary>
        public string MessageText { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the timestamp when the message was created.
        /// </summary>
        public DateTime Timestamp { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether the message has been read.
        /// </summary>
        public bool IsRead { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether the message has been delivered.
        /// </summary>
        public bool IsDelivered { get; set; }
        /// <summary>
        /// Gets or sets the department associated with the message.
        /// </summary>
        public string? Department { get; set; }
        /// <summary>
        /// Gets or sets the type of the message.
        /// </summary>
        public MessageType MessageType { get; set; } = MessageType.Chat;
        /// <summary>
        /// Gets or sets the creation date and time of the message.
        /// </summary>
        public DateTime CreatedAt { get; set; }
        /// <summary>
        /// Gets or sets the date and time when the message was delivered.
        /// </summary>
        public DateTime? DeliveredAt { get; set; }
    }
} 