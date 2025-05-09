using System.Collections.Generic;
using System.Text.Json.Serialization;
using TDFShared.Enums;

namespace TDFShared.DTOs.Messages
{
    /// <summary>
    /// DTO for creating a new chat message
    /// </summary>
    public class ChatMessageCreateDto
    {
        /// <summary>
        /// ID of the user sending the message
        /// </summary>
        [JsonPropertyName("senderId")]
        public int SenderID { get; set; }
        
        /// <summary>
        /// ID of the user receiving the message
        /// </summary>
        [JsonPropertyName("recipientId")]
        public int ReceiverID { get; set; }
        
        /// <summary>
        /// Text content of the message
        /// </summary>
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
        
        /// <summary>
        /// Indicates if the message is a private direct message
        /// </summary>
        [JsonPropertyName("isPrivate")]
        public bool IsPrivate { get; set; }
        
        /// <summary>
        /// Department ID for department messages
        /// </summary>
        [JsonPropertyName("department")]
        public string? Department { get; set; }

        /// <summary>
        /// Optional idempotency key for message tracking
        /// </summary>
        [JsonPropertyName("idempotencykey")]
        public string? IdempotencyKey { get; set; }

        /// <summary>
        /// Optional attachments for the message
        /// </summary>
        [JsonPropertyName("attachments")]
        public List<AttachmentDto>? Attachments { get; set; }


    }
} 