using System;
using System.Text.Json.Serialization;
using TDFShared.Enums;

namespace TDFShared.DTOs.Messages
{
    /// <summary>
    /// Base DTO for all message types
    /// </summary>
    public class MessageDto
    {
        /// <summary>
        /// Unique identifier for the message
        /// </summary>
        [JsonPropertyName("id")]
        public int Id { get; set; }
        
        /// <summary>
        /// ID of the user who sent the message
        /// </summary>
        [JsonPropertyName("senderId")]
        public int SenderId { get; set; }

        /// <summary>
        /// ID of the user who receives the message
        /// </summary>
        [JsonPropertyName("receiverId")]
        public int ReceiverId { get; set; }
        
        /// <summary>
        /// Username of the user who sent the message
        /// </summary>
        [JsonPropertyName("senderUsername")]
        public string SenderUsername { get; set; } = string.Empty;

        /// <summary>
        /// Full name of the user who sent the message
        /// </summary>
        [JsonPropertyName("senderFullName")]
        public string SenderFullName { get; set; } = string.Empty;

        /// <summary>
        /// Content of the message
        /// </summary>
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Whether the message has been read
        /// </summary>
        [JsonPropertyName("isRead")]
        public bool IsRead { get; set; }

        /// <summary>
        /// Whether the message has been delivered
        /// </summary>
        [JsonPropertyName("isDelivered")]
        public bool IsDelivered { get; set; }

        /// <summary>
        /// When the message was sent
        /// </summary>
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }
        
        /// <summary>
        /// Type of message (Chat, Private, System, etc.)
        /// </summary>
        [JsonPropertyName("messageType")]
        public MessageType MessageType { get; set; }
    }
}
