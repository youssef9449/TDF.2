using TDFShared.Enums;

namespace TDFShared.DTOs.Common
{
    /// <summary>
    /// DTO for returning notification permission status and optional rationale.
    /// </summary>
    public class NotificationPermissionDto
    {
        /// <summary>
        /// The current notification permission status.
        /// </summary>
        public NotificationPermissionStatus Status { get; set; }

        /// <summary>
        /// Optional rationale or message for the permission status.
        /// </summary>
        public string? Rationale { get; set; }

        /// <summary>
        /// The platform for which the permission applies (e.g., Android, iOS, Windows).
        /// </summary>
        public string? Platform { get; set; }
    }
} 