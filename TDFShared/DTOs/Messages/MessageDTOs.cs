using System;
using System.Text.Json.Serialization;

namespace TDFShared.DTOs.Messages
{
    /// <summary>
    /// DTO for notification messages
    /// </summary>
    public class NotificationDto : BaseMessageDTO
    {
        /// <summary>
        /// Default constructor, sets type to "notification"
        /// </summary>
        public NotificationDto()
        {
            Type = "notification";
        }

        /// <summary>
        /// Title of the notification
        /// </summary>
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;
                
        /// <summary>
        /// Importance level (low, medium, high)
        /// </summary>
        [JsonPropertyName("level")]
        public NotificationLevel Level { get; set; } = NotificationLevel.Medium;
        
        /// <summary>
        /// Unique identifier for the notification
        /// </summary>
        [JsonPropertyName("notificationId")]
        public int NotificationId { get; set; }
        
        /// <summary>
        /// ID of the user who should receive this notification
        /// </summary>
        [JsonPropertyName("userId")]
        public int UserId { get; set; }
        public int? SenderId { get; set; }
        public string? SenderName { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool IsBroadcast { get; set; }
        public string? Department { get; set; }

        /// <summary>
        /// Unique identifier for the notification
        /// </summary>
        public int NotificationID { get; set; }

        /// <summary>
        /// ID of the user receiving the notification
        /// </summary>
        public int ReceiverID { get; set; }

        /// <summary>
        /// Optional ID of the user who sent/triggered the notification
        /// </summary>
        public int? SenderID { get; set; }

        /// <summary>
        /// Optional ID of a related message
        /// </summary>
        public int? MessageID { get; set; }

        /// <summary>
        /// Whether the notification has been seen by the receiver
        /// </summary>
        public bool IsSeen { get; set; }

        /// <summary>
        /// When the notification was created
        /// </summary>
        public new DateTime Timestamp { get; set; }

    }

    /// <summary>
    /// Enum for notification importance levels
    /// </summary>
    public enum NotificationLevel
    {
        Low = 0,
        Medium = 1,
        High = 2
    }

} 