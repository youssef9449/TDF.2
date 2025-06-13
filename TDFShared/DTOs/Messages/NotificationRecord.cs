using System;
using System.Text.Json.Serialization;
using TDFShared.Enums;

namespace TDFShared.DTOs.Messages
{
    /// <summary>
    /// Data Transfer Object (DTO) for notification records used in API communication.
    /// This class is specifically designed for serialization/deserialization of notification data
    /// between the client and server, containing only the essential properties needed for API communication.
    /// </summary>
    /// <remarks>
    /// This class is separate from TDFMAUI.Helpers.NotificationRecord which is used for local notification tracking.
    /// The API version is optimized for network transmission and contains only the core notification properties
    /// that are relevant to both client and server.
    /// 
    /// Properties are decorated with JsonPropertyName attributes to ensure consistent serialization
    /// across different platforms and programming languages.
    /// </remarks>
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
        public required string Title { get; set; }
        
        /// <summary>
        /// Content/message of the notification
        /// </summary>
        [JsonPropertyName("message")]
        public required string Message { get; set; }
        
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
        public required string Data { get; set; }
    }
} 