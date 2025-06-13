using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using TDFShared.Enums;

namespace TDFShared.DTOs.Messages
{
    /// <summary>
    /// DTO for creating a new message.
    /// </summary>
    public class MessageCreateDto
    {
        /// <summary>
        /// The ID of the user sending the message.
        /// </summary>
        [JsonPropertyName("fromUserId")]
        public int SenderID { get; set; }

        /// <summary>
        /// The ID of the user receiving the message.
        /// </summary>
        [JsonPropertyName("toUserId")]
        public int ReceiverID { get; set; }
        public string? Department { get; set; }
        public MessageType MessageType { get; set; } = MessageType.Chat;
        /// <summary>
        /// When the message was sent
        /// </summary>
        [JsonPropertyName("sentAt")]
        public DateTime SentAt { get; set; }

        /// <summary>
        /// Name of the sender
        /// </summary>
        [JsonPropertyName("fromUserName")]
        public string FromUserName { get; set; } = string.Empty;
        /// <summary>
        /// The content of the message.
        /// </summary>
        [JsonPropertyName("content")]
        [Required(ErrorMessage = "Message content cannot be empty.")]
        [StringLength(2000, ErrorMessage = "Message content cannot exceed 2000 characters.")]
        public required string MessageText { get; set; }
    }
} 