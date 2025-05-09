using System;
using TDFShared.Enums;

namespace TDFMAUI.Helpers
{
    /// <summary>
    /// Record for storing notification history
    /// </summary>
    public class NotificationRecord
    {
        /// <summary>
        /// Unique identifier for the notification
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        /// <summary>
        /// Title of the notification
        /// </summary>
        public string Title { get; set; }
        
        /// <summary>
        /// Content/message of the notification
        /// </summary>
        public string Message { get; set; }
        
        /// <summary>
        /// Type/severity of the notification
        /// </summary>
        public NotificationType Type { get; set; }
        
        /// <summary>
        /// When the notification was created/shown
        /// </summary>
        public DateTime Timestamp { get; set; }
        
        /// <summary>
        /// Additional data associated with the notification
        /// </summary>
        public string Data { get; set; }
    }

    /// <summary>
    /// Event arguments for notification-related events
    /// </summary>
    public class NotificationEventArgs : EventArgs
    {
        /// <summary>
        /// Title of the notification
        /// </summary>
        public string Title { get; set; }
        
        /// <summary>
        /// Content/message of the notification
        /// </summary>
        public string Message { get; set; }
        
        /// <summary>
        /// Type/severity of the notification
        /// </summary>
        public NotificationType Type { get; set; }
        
        /// <summary>
        /// When the notification was created
        /// </summary>
        public DateTime Timestamp { get; set; }
        
        /// <summary>
        /// ID of the notification in the database
        /// </summary>
        public int NotificationId { get; set; }
        
        /// <summary>
        /// ID of the user who sent the notification (if any)
        /// </summary>
        public int? SenderId { get; set; }
        
        /// <summary>
        /// Name of the user who sent the notification
        /// </summary>
        public string SenderName { get; set; }
        
        /// <summary>
        /// Whether this is a broadcast notification
        /// </summary>
        public bool IsBroadcast { get; set; }
        
        /// <summary>
        /// Department the notification is targeting (for broadcasts)
        /// </summary>
        public string Department { get; set; }
        
        /// <summary>
        /// Additional data associated with the notification
        /// </summary>
        public string Data { get; set; }
    }
} 