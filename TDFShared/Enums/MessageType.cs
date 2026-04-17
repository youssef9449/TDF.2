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
        /// Private direct message
        /// </summary>
        Private = 1,

        /// <summary>
        /// Group or Department message
        /// </summary>
        Group = 2,

        /// <summary>
        /// System-generated message appearing in chat
        /// </summary>
        System = 3
    }
}
