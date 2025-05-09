using System.Text.Json.Serialization;
using TDFShared.Enums; // Assuming MessageStatus enum is here

namespace TDFShared.DTOs.Messages
{
    /// <summary>
    /// DTO for updating the status of a message.
    /// </summary>
    public class MessageStatusUpdateDto
    {
        /// <summary>
        /// The ID of the message being updated.
        /// </summary>
        [JsonPropertyName("messageId")]
        public int MessageId { get; set; } // Assuming int ID

        /// <summary>
        /// The new status of the message (e.g., Delivered, Read).
        /// </summary>
        [JsonPropertyName("status")]
        public MessageStatus Status { get; set; }

        /// <summary>
        /// Optional: The ID of the user whose status is relevant (e.g., who read the message).
        /// </summary>
        [JsonPropertyName("userId")]
        public int? UserId { get; set; }
    }
} 