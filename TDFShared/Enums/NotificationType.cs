using System.Text.Json.Serialization;

namespace TDFShared.Enums
{
    /// <summary>
    /// Types of notifications by importance/severity
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum NotificationType
    {
        /// <summary>
        /// Informational notification
        /// </summary>
        Info,

        /// <summary>
        /// Success notification
        /// </summary>
        Success,

        /// <summary>
        /// Warning notification
        /// </summary>
        Warning,

        /// <summary>
        /// Error notification
        /// </summary>
        Error,

        /// <summary>
        /// System-level notification
        /// </summary>
        System
    }
}