using System.Text.Json.Serialization;

namespace TDFShared.Enums
{
    /// <summary>
    /// Supported leave types for requests.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum LeaveType
    {
        /// <summary>Annual leave</summary>
        Annual,
        /// <summary>Emergency leave (also known as Casual Leave)</summary>
        Emergency,
        /// <summary>Unpaid leave</summary>
        Unpaid,
        /// <summary>Permission leave</summary>
        Permission,
        /// <summary>External assignment leave</summary>
        ExternalAssignment,
        /// <summary>Work From Home leave</summary>
        WorkFromHome
    }
}
