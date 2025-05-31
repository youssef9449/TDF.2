namespace TDFShared.Enums
{
    /// <summary>
    /// Represents the status of a scheduled notification.
    /// </summary>
    public enum NotificationScheduleStatus
    {
        /// <summary>
        /// The notification is scheduled and pending delivery.
        /// </summary>
        Scheduled,

        /// <summary>
        /// The notification has been delivered.
        /// </summary>
        Triggered,

        /// <summary>
        /// The scheduled notification was cancelled before delivery.
        /// </summary>
        Cancelled,

        /// <summary>
        /// The scheduled notification failed to deliver.
        /// </summary>
        Failed
    }
} 