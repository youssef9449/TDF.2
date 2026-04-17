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
        [JsonPropertyName("senderId")]
        public int SenderId { get; set; }

        /// <summary>
        /// The ID of the user receiving the message.
        /// </summary>
        [JsonPropertyName("receiverId")]
        public int ReceiverId { get; set; }

        [JsonPropertyName("department")]
        public string? Department { get; set; }

        [JsonPropertyName("messageType")]
        public MessageType MessageType { get; set; } = MessageType.Chat;

        /// <summary>
        /// The content of the message.
        /// </summary>
        [JsonPropertyName("content")]
        [Required(ErrorMessage = "Message content cannot be empty.")]
        [StringLength(2000, ErrorMessage = "Message content cannot exceed 2000 characters.")]
        public required string Content { get; set; }

        /// <summary>
        /// Optional idempotency key for message tracking
        /// </summary>
        [JsonPropertyName("idempotencyKey")]
        public string? IdempotencyKey { get; set; }
    }
}
