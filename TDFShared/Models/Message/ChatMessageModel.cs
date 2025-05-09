using System;
using System.Text.Json.Serialization;

namespace TDFShared.Models.Message
{
    /// <summary>
    /// UI model for chat messages in MAUI
    /// </summary>
    public class ChatMessageModel
    {
        /// <summary>
        /// Unique identifier for the message
        /// </summary>
        [JsonPropertyName("messageId")]
        public int MessageId { get; set; }
        
        /// <summary>
        /// User ID of the sender
        /// </summary>
        [JsonPropertyName("fromUserId")]
        public int SenderId { get; set; }
        
        /// <summary>
        /// Name of the sender
        /// </summary>
        [JsonPropertyName("fromUserName")]
        public string FromUserName { get; set; } = string.Empty;
        
        /// <summary>
        /// Message content
        /// </summary>
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
        
        /// <summary>
        /// When the message was sent
        /// </summary>
        [JsonPropertyName("sentAt")]
        public DateTime SentAt { get; set; }
        
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
        /// Indicates if the message is from the current user
        /// </summary>
        [JsonIgnore]
        public bool IsFromCurrentUser { get; set; }
        
        // Backwards compatibility properties
        [JsonIgnore]
        public int MessageID 
        { 
            get => MessageId; 
            set => MessageId = value; 
        }
        
        [JsonIgnore]
        public int SenderID 
        { 
            get => SenderId; 
            set => SenderId = value; 
        }
        
        [JsonIgnore]
        public string SenderName 
        { 
            get => FromUserName; 
            set => FromUserName = value; 
        }
        
        [JsonIgnore]
        public string MessageContent 
        { 
            get => Content; 
            set => Content = value; 
        }
        
        [JsonIgnore]
        public DateTime Timestamp 
        { 
            get => SentAt; 
            set => SentAt = value; 
        }
    }
} 