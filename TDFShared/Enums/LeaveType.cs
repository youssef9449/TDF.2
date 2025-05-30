using System.Text.Json.Serialization;

namespace TDFShared.Enums
{
    /// <summary>
    /// Supported leave types for requests.
    /// </summary>
    // NOTE: 'Casual' leave is not supported. Use 'Emergency' for all such cases. Any legacy 'Casual' references are for DB compatibility only.
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum LeaveType
    {
        /// <summary>Annual leave</summary>
        Annual,
        /// <summary>Emergency leave</summary>
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
