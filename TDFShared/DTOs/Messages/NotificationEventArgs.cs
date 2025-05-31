using System;
using TDFShared.Enums;

namespace TDFShared.DTOs.Messages
{
    /// <summary>
    /// Event arguments for notification events
    /// </summary>
    public class NotificationEventArgs : EventArgs
    {
        /// <summary>
        /// Title of the notification
        /// </summary>
        public string Title { get; set; } = string.Empty;
        
        /// <summary>
        /// Message content of the notification
        /// </summary>
        public string Message { get; set; } = string.Empty;
        
        /// <summary>
        /// Type/severity of the notification
        /// </summary>
        public NotificationType Type { get; set; } = NotificationType.Info;
        
        /// <summary>
        /// When the notification was received
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;
        
        /// <summary>
        /// Unique identifier for the notification
        /// </summary>
        public int NotificationId { get; set; }
        
        /// <summary>
        /// ID of the user who sent the notification (if applicable)
        /// </summary>
        public int? SenderId { get; set; }
        
        /// <summary>
        /// Name of the user who sent the notification (if applicable)
        /// </summary>
        public string? SenderName { get; set; }
        
        /// <summary>
        /// Whether this is a broadcast notification
        /// </summary>
        public bool IsBroadcast { get; set; }
        
        /// <summary>
        /// Department the notification was sent to (if applicable)
        /// </summary>
        public string? Department { get; set; }
        
        /// <summary>
        /// Additional data associated with the notification
        /// </summary>
        public string? Data { get; set; }
    }
}