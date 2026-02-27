using System;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using TDFShared.Enums;
using TDFShared.Models.User;

namespace TDFShared.DTOs.Users
{
    /// <summary>
    /// Data transfer object for user information
    /// </summary>
    public class UserDto : UserEntity, ICloneable
    {
        /// <summary>
        /// List of roles assigned to the user. If no roles are assigned, the user is considered a regular user.
        /// </summary>
        [JsonPropertyName("roles")]
        public List<string> Roles { get; set; } = new List<string>();

        /// <summary>
        /// Annual leave balance
        /// </summary>
        [JsonPropertyName("annualLeaveBalance")]
        public int AnnualLeaveBalance { get; set; }
        /// <summary>
        /// Emergency leave balance
        /// </summary>
        [JsonPropertyName("emergencyLeaveBalance")]
        public int EmergencyLeaveBalance { get; set; }

        /// <summary>
        /// Permissions balance
        /// </summary>
        [JsonPropertyName("permissionsBalance")]
        public int PermissionsBalance { get; set; }

        /// <summary>
        /// Unpaid leave used
        /// </summary>
        [JsonPropertyName("unpaidLeaveUsed")]
        public int UnpaidLeaveUsed { get; set; }

        /// <summary>
        /// Legacy annual balance property (kept for compatibility)
        /// </summary>
        [JsonPropertyName("annualBalance")]
        public int AnnualBalance { get; set; }

        /// <summary>
        /// Creates a clone of the UserDto
        /// </summary>
        public object Clone()
        {
            return this.MemberwiseClone();
        }
    }

}