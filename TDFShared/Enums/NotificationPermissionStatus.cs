namespace TDFShared.Enums
{
    /// <summary>
    /// Represents the permission status for notifications on a device.
    /// </summary>
    public enum NotificationPermissionStatus
    {
        /// <summary>
        /// The user has granted permission to show notifications.
        /// </summary>
        Granted,

        /// <summary>
        /// The user has denied permission to show notifications.
        /// </summary>
        Denied,

        /// <summary>
        /// The user has not yet been prompted or has not made a choice.
        /// </summary>
        NotDetermined
    }
} 