using System;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using TDFShared.Enums;

namespace TDFShared.DTOs.Users
{
    /// <summary>
    /// Data transfer object for user information
    /// </summary>
    public class UserDto : ICloneable
    {
    
        /// <summary>
        /// User's unique identifier
        /// </summary>
        [JsonPropertyName("id")]
        public int UserID { get; set; }

        
        /// <summary>
        /// User's login username
        /// </summary>
        [JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;
        
        /// <summary>
        /// User's full name
        /// </summary>
        [JsonPropertyName("fullName")]
        public string FullName { get; set; } = string.Empty;
         
        /// <summary>
        /// User's department
        /// </summary>
        [JsonPropertyName("department")]
        public string Department { get; set; } = string.Empty;
        
        /// <summary>
        /// User's job title
        /// </summary>
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;
        
        /// <summary>
        /// Flag indicating if the user account is active
        /// </summary>
        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; } = true;
        
        /// <summary>
        /// Flag indicating if the user has administrator privileges
        /// </summary>
        [JsonPropertyName("isAdmin")]
        public bool IsAdmin { get; set; } = false;

        /// <summary>
        /// Flag indicating if the user has manager privileges
        /// </summary>
        [JsonPropertyName("isManager")]
        public bool IsManager { get; set; } = false;

        /// <summary>
        /// Flag indicating if the user has HR privileges
        /// </summary>
        [JsonPropertyName("isHR")]
        public bool IsHR { get; set; } = false;


        [JsonPropertyName("roles")]
        public List<string> Roles { get; set; }

        [JsonPropertyName("role")]
        public string? Role { get; set; }

        /// <summary>
        /// Raw image data for the user's profile picture (if stored directly in DB)
        /// </summary>
        [JsonPropertyName("profilePictureData")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] // Don't serialize if null
        public byte[] ProfilePictureData { get; set; }
        
        /// <summary>
        /// Current presence status (Online, Offline, Away, etc.)
        /// </summary>
        [JsonPropertyName("presenceStatus")]
        [JsonConverter(typeof(JsonStringEnumConverter))] // Serialize enum as string
        public UserPresenceStatus PresenceStatus { get; set; } = UserPresenceStatus.Offline;
        
        /// <summary>
        /// Optional custom status message set by the user.
        /// </summary>
        [JsonPropertyName("statusMessage")]
        public string? StatusMessage { get; set; }

        /// <summary>
        /// Optional custom status message set by the user.
        /// </summary>
        [JsonPropertyName("currentdevice")]
        public string? CurrentDevice { get; set; }

        /// <summary>
        /// Indicates if the user is currently available for chat.
        /// </summary>
        [JsonPropertyName("isAvailableForChat")]
        public bool IsAvailableForChat { get; set; } = true; // Default availability
        
        /// <summary>
        /// The last time user activity was detected.
        /// </summary>
        [JsonPropertyName("lastActivityTime")]
        public DateTime? LastActivityTime { get; set; }
        
        /// <summary>
        /// Date and time when the user was created
        /// </summary>
        [JsonPropertyName("createdAt")]
        public DateTime? CreatedAt { get; set; }

        /// <summary>
        /// Date and time when the user was last updated
        /// </summary>
        [JsonPropertyName("updatedAt")]
        public DateTime? UpdatedAt { get; set; }

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

        public bool isConnected { get; set; } // Note: C# convention usually uses PascalCase (IsConnected)
        public int? FailedLoginAttempts { get; set; }
        public bool? IsLocked { get; set; }
        public DateTime? LockoutEndTime { get; set; }

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