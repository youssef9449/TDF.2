using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace TDFShared.Models.Notification
{
    /// <summary>
    /// Represents a notification in the system
    /// </summary>
    public class NotificationEntity
    {
        /// <summary>
        /// Unique identifier for the notification
        /// </summary>
        [Key]
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
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Content of the notification
        /// </summary>
        public string? Message { get; set; }
        
        /// <summary>
        /// Alias for Message property (for backward compatibility)
        /// </summary>
        [JsonIgnore]
        public string? MessageText
        {
            get => Message;
            set => Message = value;
        }
    }
} 