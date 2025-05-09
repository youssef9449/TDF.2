using System;
using System.Text.Json.Serialization;
using TDFShared.Enums;
using System.Drawing;

namespace TDFShared.Models.Message
{
    /// <summary>
    /// UI model for messages in MAUI
    /// </summary>
    public class MessageModel
    {
        /// <summary>
        /// Unique identifier for the message
        /// </summary>
        [JsonPropertyName("id")]
        public int Id { get; set; }
        
        /// <summary>
        /// User ID of the sender
        /// </summary>
        [JsonPropertyName("fromUserId")]
        public int FromUserId { get; set; }
        
        /// <summary>
        /// Name of the sender
        /// </summary>
        [JsonPropertyName("fromUserName")]
        public string FromUserName { get; set; } = string.Empty;
        
        /// <summary>
        /// User ID of the recipient
        /// </summary>
        [JsonPropertyName("toUserId")]
        public int ToUserId { get; set; }

        /// <summary>
        /// The type of message (chat, system, notification)
        /// </summary>
        public MessageType MessageType { get; set; } = MessageType.Chat;

        /// <summary>
        /// Name of the recipient
        /// </summary>
        [JsonPropertyName("toUserName")]
        public string ToUserName { get; set; } = string.Empty;
        
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
        /// When the message was read
        /// </summary>
        [JsonPropertyName("readAt")]
        public DateTime? ReadAt { get; set; }
        
        /// <summary>
        /// When the message was delivered
        /// </summary>
        [JsonPropertyName("deliveredAt")]
        public DateTime? DeliveredAt { get; set; }
        
        /// <summary>
        /// Whether the message has been read
        /// </summary>
        [JsonIgnore]
        public bool IsRead => ReadAt.HasValue;
        
        /// <summary>
        /// Whether the message has been delivered
        /// </summary>
        [JsonIgnore]
        public bool IsDelivered => DeliveredAt.HasValue;
        
        /// <summary>
        /// Color representation of the sender's status (online, away, etc.)
        /// </summary>
        [JsonIgnore]
        public Color SenderStatusColor { get; set; } = Color.Gray; // Default gray
        
        /// <summary>
        /// Hex string representation of SenderStatusColor for XAML binding
        /// </summary>
        [JsonIgnore]
        public string SenderStatusColorHex 
        { 
            get => $"#{SenderStatusColor.R:X2}{SenderStatusColor.G:X2}{SenderStatusColor.B:X2}"; 
        }
        
        /// <summary>
        /// Whether to show the sender's status indicator
        /// </summary>
        [JsonIgnore]
        public bool ShowSenderStatus { get; set; } = true;
        
        // Backwards compatibility properties
        [JsonIgnore]
        public int SenderId 
        { 
            get => FromUserId; 
            set => FromUserId = value; 
        }
        
        [JsonIgnore]
        public string SenderName 
        { 
            get => FromUserName; 
            set => FromUserName = value; 
        }
        
        [JsonIgnore]
        public int ReceiverId 
        { 
            get => ToUserId; 
            set => ToUserId = value; 
        }
        
        [JsonIgnore]
        public string ReceiverName 
        { 
            get => ToUserName; 
            set => ToUserName = value; 
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
        
        [JsonIgnore]
        public int MessageId
        {
            get => Id;
            set => Id = value;
        }

        // UI specific properties - Should be populated by platform-specific ViewModel
        [JsonIgnore]
        public bool IsUnread => !IsRead;
    }
} 