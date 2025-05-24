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
        /// Casual leave balance
        /// </summary>
        [JsonPropertyName("casualLeaveBalance")]
        public int CasualLeaveBalance { get; set; }

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
        /// Legacy casual balance property (kept for compatibility)
        /// </summary>
        [JsonPropertyName("casualBalance")]
        public int CasualBalance { get; set; }

        /// <summary>
        /// Creates a clone of the UserDto
        /// </summary>
        public object Clone()
        {
            return this.MemberwiseClone();
        }
    }

    /// <summary>
    /// Represents the online status and presence information for a user
    /// </summary>
    public class UserPresence
    {
        /// <summary>
        /// The ID of the user
        /// </summary>
        public int UserId { get; set; }

        /// <summary>
        /// Whether the user is currently online
        /// </summary>
        public bool IsOnline { get; set; }

        /// <summary>
        /// The last time the user was seen online
        /// </summary>
        public DateTime? LastSeen { get; set; }

        /// <summary>
        /// The connection ID for the user's current WebSocket connection (if any)
        /// </summary>
        public string ConnectionId { get; set; }

        /// <summary>
        /// The type of device the user is connected from (e.g., "Mobile", "Desktop")
        /// </summary>
        public string DeviceType { get; set; }
    }
} 