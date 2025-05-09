using System.Text.Json.Serialization;

namespace TDFShared.Enums
{
    /// <summary>
    /// Types of messages supported by the system
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MessageType 
    { 
        /// <summary>
        /// Direct message between users
        /// </summary>
        Chat = 0,
        
        /// <summary>
        /// System-generated message
        /// </summary>
        System = 1,
        
        /// <summary>
        /// Notification for events
        /// </summary>
        Notification = 2,

        /// <summary>
        /// Notification for Announcement
        /// </summary>
        Announcement = 3,
        
        /// <summary>
        /// Private direct message
        /// </summary>
        Private = 4
    }
} 