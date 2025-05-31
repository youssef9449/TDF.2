using System;
using TDFShared.Enums;

namespace TDFShared.DTOs.Common
{
    /// <summary>
    /// DTO for scheduling a notification to be delivered at a specific time.
    /// </summary>
    public class NotificationScheduleDto
    {
        /// <summary>
        /// Unique identifier for the scheduled notification.
        /// </summary>
        public Guid ScheduleId { get; set; }

        /// <summary>
        /// ID of the user who should receive this notification.
        /// </summary>
        public int ReceiverId { get; set; }

        /// <summary>
        /// Title of the notification.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Content/message of the notification.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// The date and time when the notification should be fired (in UTC).
        /// </summary>
        public DateTime FireAt { get; set; }

        /// <summary>
        /// The time zone for the scheduled notification (IANA or Windows format).
        /// </summary>
        public string TimeZone { get; set; }

        /// <summary>
        /// Optional additional data for the notification.
        /// </summary>
        public string? Data { get; set; }

        /// <summary>
        /// The type/severity of the notification.
        /// </summary>
        public NotificationType Type { get; set; } = NotificationType.Info;
    }
} 