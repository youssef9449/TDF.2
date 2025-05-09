using System;
using System.Text.Json.Serialization;
using TDFShared.Enums;

namespace TDFShared.DTOs.Messages
{
    /// <summary>
    /// Record of a notification for tracking and history
    /// </summary>
    public class NotificationRecord
    {
        /// <summary>
        /// Unique identifier for the notification
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        /// <summary>
        /// Title of the notification
        /// </summary>
        [JsonPropertyName("title")]
        public string Title { get; set; }
        
        /// <summary>
        /// Content/message of the notification
        /// </summary>
        [JsonPropertyName("message")]
        public string Message { get; set; }
        
        /// <summary>
        /// Type/severity of the notification
        /// </summary>
        [JsonPropertyName("type")]
        public NotificationType Type { get; set; }
        
        /// <summary>
        /// When the notification was created/shown
        /// </summary>
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }
        
        /// <summary>
        /// Additional data associated with the notification
        /// </summary>
        [JsonPropertyName("data")]
        public string Data { get; set; }
    }
} 