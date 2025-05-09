using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using TDFShared.Enums;

namespace TDFShared.DTOs.Messages
{
    /// <summary>
    /// Base message DTO that all messages inherit from
    /// </summary>
    public abstract class BaseMessageDTO
    {
        /// <summary>
        /// The type of message
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;
        
        /// <summary>
        /// When the message was created/sent
        /// </summary>
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Chat message DTO for sending and receiving chat messages
    /// </summary>
    public class ChatMessageDto : BaseMessageDTO
    {
        /// <summary>
        /// Default constructor, sets type to "chat_message"
        /// </summary>
        public ChatMessageDto()
        {
            Type = "chat_message";
        }
        
        /// <summary>
        /// Unique identifier for the message
        /// </summary>
        [JsonPropertyName("messageId")]
        public int MessageId { get; set; }
        
        /// <summary>
        /// ID of the sender
        /// </summary>
        [JsonPropertyName("senderId")]
        public int SenderId { get; set; }

        public int Id { get; set; }

        public int ReceiverId { get; set; }
        public DateTime SentAt { get; set; } = DateTime.UtcNow;



        /// <summary>
        /// Name of the sender
        /// </summary>
        [JsonPropertyName("senderName")]
        public string SenderName { get; set; } = string.Empty;
        
        /// <summary>
        /// The message content
        /// </summary>
        [JsonPropertyName("message")]
        public string MessageText { get; set; } = string.Empty;
        
        /// <summary>
        /// Type of the message (chat, system, notification)
        /// </summary>
        [JsonPropertyName("messageType")]
        public MessageType MessageType { get; set; } = MessageType.Chat;
        
        /// <summary>
        /// Whether this is a global/broadcast message
        /// </summary>
        [JsonPropertyName("isGlobal")]
        public bool IsGlobal { get; set; }

        public bool IsRead { get; set; }
        public bool IsDelivered { get; set; }
        public string Department { get; set; }

        /// <summary>
        /// Optional idempotency key for message tracking
        /// </summary>
        [JsonPropertyName("idempotencyKey")]
        public string? IdempotencyKey { get; set; }
    }
    public class HeartbeatDTO : BaseMessageDTO
    {
        public HeartbeatDTO()
        {
            Type = "heartbeat";
        }
    }

    // Pending message counts
    public class PendingMessageInfoDTO
    {
        public int Count { get; set; }
        public List<string> Messages { get; set; } = new List<string>();
        public List<int> MessageIds { get; set; } = new List<int>();
    }

    // Message response DTO
    public class MessageResponseDto
    {
        public int MessageID { get; set; }
        public string Status { get; set; } = "sent";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
    public class ErrorMessageDTO : BaseMessageDTO
    {
        public ErrorMessageDTO()
        {
            Type = "error";
        }

        public string Message { get; set; } = string.Empty;
        public string? Code { get; set; }
    }
    public class UserConnectionStatusDTO : BaseMessageDTO
    {
        public UserConnectionStatusDTO()
        {
            Type = "user_connection_status";
        }

        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public bool IsConnected { get; set; }
        public string? Department { get; set; }
    }
} 