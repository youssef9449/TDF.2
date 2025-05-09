using System;
using System.Text.Json.Serialization;
using TDFShared.Enums;

namespace TDFShared.Models.Message
{
    /// <summary>
    /// Represents a message entity in the system for data persistence and domain logic
    /// </summary>
    public class MessageEntity
    {
        /// <summary>
        /// Unique identifier for the message
        /// </summary>
        public int MessageID { get; set; }
        
        /// <summary>
        /// User ID of the sender
        /// </summary>
        public int SenderID { get; set; }
        
        /// <summary>
        /// User ID of the recipient
        /// </summary>
        public int ReceiverID { get; set; }
        
        /// <summary>
        /// Message content
        /// </summary>
        public string MessageText { get; set; } = string.Empty;
        
        /// <summary>
        /// When the message was sent
        /// </summary>
        public DateTime Timestamp { get; set; }
        
        /// <summary>
        /// Whether the message has been read
        /// </summary>
        public bool IsRead { get; set; }
        
        /// <summary>
        /// Whether the message has been delivered
        /// </summary>
        public bool IsDelivered { get; set; }
        
        /// <summary>
        /// Department the message is associated with (optional)
        /// </summary>
        public string? Department { get; set; }
        
        /// <summary>
        /// The type of message (chat, system, notification)
        /// </summary>
        public MessageType MessageType { get; set; } = MessageType.Chat;
        
        /// <summary>
        /// The delivery status of the message
        /// </summary>
        public MessageStatus Status { get; set; } = MessageStatus.Sent;
        
        /// <summary>
        /// Indicates if the message is a global broadcast (ReceiverID = 0)
        /// </summary>
        public bool IsGlobal { get; set; }
        
        /// <summary>
        /// Idempotency key to prevent duplicate messages
        /// </summary>
        public string? IdempotencyKey { get; set; }
        
        /// <summary>
        /// Creates a new chat message
        /// </summary>
        public static MessageEntity CreateChatMessage(int senderId, int receiverId, string content, bool isDelivered = false, string? idempotencyKey = null)
        {
            if (senderId <= 0) throw new ArgumentException("SenderId must be positive", nameof(senderId));
            if (receiverId < 0) throw new ArgumentException("ReceiverId must be non-negative", nameof(receiverId));
            if (string.IsNullOrWhiteSpace(content)) throw new ArgumentException("Content cannot be empty", nameof(content));
            
            return new MessageEntity
            {
                SenderID = senderId,
                ReceiverID = receiverId,
                MessageText = content,
                Timestamp = DateTime.UtcNow,
                IsDelivered = isDelivered,
                Status = isDelivered ? MessageStatus.Delivered : MessageStatus.Sent,
                MessageType = MessageType.Chat,
                IsGlobal = receiverId == 0, // If receiverId is 0, it's a global message
                IdempotencyKey = idempotencyKey
            };
        }
        
        /// <summary>
        /// Creates a new system message
        /// </summary>
        public static MessageEntity CreateSystemMessage(int receiverId, string content, string? idempotencyKey = null)
        {
            if (receiverId < 0) throw new ArgumentException("ReceiverId must be non-negative", nameof(receiverId));
            if (string.IsNullOrWhiteSpace(content)) throw new ArgumentException("Content cannot be empty", nameof(content));
            
            return new MessageEntity
            {
                SenderID = 0, // System messages have senderId = 0
                ReceiverID = receiverId,
                MessageText = content,
                Timestamp = DateTime.UtcNow,
                Status = MessageStatus.Sent,
                MessageType = MessageType.System,
                IsGlobal = receiverId == 0, // System messages can be global too
                IdempotencyKey = idempotencyKey
            };
        }
        
        /// <summary>
        /// Marks the message as delivered
        /// </summary>
        public MessageEntity MarkAsDelivered()
        {
            if (IsRead)
                return this; // Already in a terminal state
                
            IsDelivered = true;
            Status = MessageStatus.Delivered;
            return this;
        }
        
        /// <summary>
        /// Marks the message as read
        /// </summary>
        public MessageEntity MarkAsRead()
        {
            IsRead = true;
            IsDelivered = true;
            Status = MessageStatus.Read;
            return this;
        }
    }
} 