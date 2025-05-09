using System.Text.Json.Serialization;

namespace TDFShared.Enums
{
    /// <summary>
    /// Message status enum
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MessageStatus
    {
        /// <summary>
        /// Message has been sent
        /// </summary>
        Sent = 0,
        
        /// <summary>
        /// Message has been delivered to recipient
        /// </summary>
        Delivered = 1,
        
        /// <summary>
        /// Message has been read by recipient
        /// </summary>
        Read = 2,

        /// <summary>
        /// Message delivery failed
        /// </summary>
        Failed = 3
    }
} 