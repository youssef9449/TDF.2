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
        [JsonPropertyName("fromUserId")]
        public int SenderId { get; set; }

        [JsonPropertyName("toUserId")]
        public int ReceiverId { get; set; }
        
        /// <summary>
        /// Username of the user who sent the message
        /// </summary>
        [JsonPropertyName("fromUsername")]
        public string FromUsername { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public bool IsRead { get; set; }
        public bool IsDelivered { get; set; }


        /// <summary>
        /// Full name of the user who sent the message
        /// </summary>
        [JsonPropertyName("fromUserFullName")]
        public string FromUserFullName { get; set; } = string.Empty;
        
        /// <summary>
        /// Profile image URL of the user who sent the message
        /// </summary>
        [JsonPropertyName("fromUserProfileImage")]
        public required byte[] FromUserProfileImage { get; set; }
        
        /// <summary>
        /// When the message was sent
        /// </summary>
        [JsonPropertyName("sentAt")]
        public DateTime Timestamp { get; set; }
        
        /// <summary>
        /// When the message was delivered to recipients
        /// </summary>
        [JsonPropertyName("deliveredAt")]
        public DateTime? DeliveredAt { get; set; }
        
        /// <summary>
        /// When the message was read by the recipient
        /// </summary>
        [JsonPropertyName("readAt")]
        public DateTime? ReadAt { get; set; }
        
        /// <summary>
        /// Type of message (Chat, System, Notification, etc.)
        /// </summary>
        [JsonPropertyName("messageType")]
        public MessageType MessageType { get; set; }
        
        /// <summary>
        /// Current status of the message
        /// </summary>
        [JsonPropertyName("status")]
        public MessageStatus Status { get; set; }
    }
} 