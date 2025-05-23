using System.Text.Json.Serialization;

namespace TDFShared.Enums
{
    /// <summary>
    /// Status of a request
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RequestStatus
    {
        /// <summary>
        /// Request is pending approval
        /// </summary>
        Pending,
        
        /// <summary>
        /// Request has been approved
        /// </summary>
        Approved,

        /// <summary>
        /// Request has been approved by manager
        /// </summary>
        ManagerApproved,

        /// <summary>
        /// Request has been approved by HR
        /// </summary>
        HRApproved,
        
        /// <summary>
        /// Request has been rejected
        /// </summary>
        Rejected,
        
        /// <summary>
        /// Request has been canceled
        /// </summary>
        Canceled,
        
        /// <summary>
        /// Request is in progress
        /// </summary>
        All
    }
} 