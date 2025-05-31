using System;
using TDFShared.Enums;

namespace TDFMAUI.Helpers
{
    /// <summary>
    /// Local notification record used for tracking notification delivery and history within the MAUI application.
    /// This class is specifically designed for local storage and UI display, containing additional properties
    /// for tracking delivery status and retry attempts that are not relevant to API communication.
    /// </summary>
    /// <remarks>
    /// This class is separate from TDFShared.DTOs.Messages.NotificationRecord which is used for API communication.
    /// The local version includes additional properties for tracking delivery status and retry attempts
    /// that are specific to the MAUI application's needs.
    /// </remarks>
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

        /// <summary>
        /// Whether the notification was successfully delivered to the user
        /// </summary>
        /// <remarks>
        /// This property is used to track the delivery status of local notifications
        /// and is not relevant to API communication.
        /// </remarks>
        public bool WasDelivered { get; set; }

        /// <summary>
        /// When the notification was successfully delivered to the user
        /// </summary>
        /// <remarks>
        /// This property is used to track the delivery timing of local notifications
        /// and is not relevant to API communication.
        /// </remarks>
        public DateTime? DeliveryTime { get; set; }

        /// <summary>
        /// Error message if the notification delivery failed
        /// </summary>
        /// <remarks>
        /// This property is used to track delivery failures of local notifications
        /// and is not relevant to API communication.
        /// </remarks>
        public string DeliveryError { get; set; }

        /// <summary>
        /// Number of retry attempts made to deliver the notification
        /// </summary>
        /// <remarks>
        /// This property is used to track retry attempts for local notifications
        /// and is not relevant to API communication.
        /// </remarks>
        public int RetryCount { get; set; }
    }
}