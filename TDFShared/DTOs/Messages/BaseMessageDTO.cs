using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using TDFShared.Enums;

namespace TDFShared.DTOs.Messages
{
    /// <summary>
    /// Base message DTO that all WebSocket messages inherit from
    /// </summary>
    public abstract class BaseMessageDTO
    {
        /// <summary>
        /// The type of message (chat_message, notification, user_presence, etc.)
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
    /// Chat message DTO for sending and receiving chat messages via WebSocket
    /// </summary>
    public class ChatMessageDto : BaseMessageDTO
    {
        public ChatMessageDto()
        {
            Type = "chat_message";
        }
        
        [JsonPropertyName("messageId")]
        public int MessageId { get; set; }
        
        [JsonPropertyName("senderId")]
        public int SenderId { get; set; }

        [JsonPropertyName("senderName")]
        public string SenderName { get; set; } = string.Empty;

        [JsonPropertyName("receiverId")]
        public int ReceiverId { get; set; }
        
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
        
        [JsonPropertyName("messageType")]
        public MessageType MessageType { get; set; } = MessageType.Chat;
        
        [JsonPropertyName("isGlobal")]
        public bool IsGlobal { get; set; }

        [JsonPropertyName("isRead")]
        public bool IsRead { get; set; }

        [JsonPropertyName("isDelivered")]
        public bool IsDelivered { get; set; }

        [JsonPropertyName("department")]
        public string? Department { get; set; }

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
