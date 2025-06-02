using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using TDFShared.Models.Request;
using TDFShared.Enums;
using TDFShared.Models.Message;


namespace TDFShared.Models.User
{
    /// <summary>
    /// Represents the User entity corresponding to the dbo.Users table
    /// </summary>
    public class UserEntity
    {
        /// <summary>
        /// Gets or sets the unique identifier for the user.
        /// </summary>
        [Key]
        public int UserID { get; set; }

        /// <summary>
        /// Gets or sets the username used for authentication.
        /// </summary>
        [Required]
        [StringLength(50)]
        public string UserName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the hashed password for the user.
        /// </summary>
        [Required]
        [StringLength(256)]
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the salt used for password hashing.
        /// </summary>
        [Required]
        [StringLength(128)]
        public string Salt { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the full name of the user.
        /// </summary>
        [StringLength(100)]
        public string? FullName { get; set; }

        /// <summary>
        /// Gets or sets the job title of the user.
        /// </summary>
        [StringLength(100)]
        public string? Title { get; set; }

        /// <summary>
        /// Gets or sets the department the user belongs to.
        /// </summary>
        [Required]
        [StringLength(255)]
        public string Department { get; set; } = string.Empty;
 
        /// <summary>
        /// Gets or sets the user's profile picture as a byte array.
        /// </summary>
        public byte[]? Picture { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the user is currently connected.
        /// </summary>
        public bool? IsConnected { get; set; }

        /// <summary>
        /// Gets or sets the name of the machine the user is connected from.
        /// </summary>
        [StringLength(100)]
        public string? MachineName { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the user's last activity.
        /// </summary>
        public DateTime? LastActivityTime { get; set; }
        /// <summary>
        /// Date and time when the user was created
        /// </summary>
        [JsonPropertyName("createdAt")]
        public DateTime? CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the current presence status of the user.
        /// </summary>
        public UserPresenceStatus PresenceStatus { get; set; } = UserPresenceStatus.Offline;

        /// <summary>
        /// Gets or sets the current device the user is using.
        /// </summary>
        [StringLength(100)]
        public string? CurrentDevice { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the user is available for chat.
        /// </summary>
        public bool? IsAvailableForChat { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the user has administrator privileges.
        /// </summary>
        public bool? IsAdmin { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the user has manager privileges.
        /// </summary>
        public bool? IsManager { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the user has HR privileges.
        /// </summary>
        public bool? IsHR { get; set; }

        /// <summary>
        /// Gets or sets the refresh token used for authentication.
        /// </summary>
        [StringLength(128)]
        public string? RefreshToken { get; set; }

        /// <summary>
        /// Gets or sets the expiry time of the refresh token.
        /// </summary>
        public DateTime? RefreshTokenExpiryTime { get; set; }

        /// <summary>
        /// Gets or sets the date and time of the user's last login.
        /// </summary>
        public DateTime? LastLoginDate { get; set; }

        /// <summary>
        /// Gets or sets the IP address from which the user last logged in.
        /// </summary>
        [StringLength(50)]
        public string? LastLoginIp { get; set; }

        /// <summary>
        /// Gets or sets the number of failed login attempts.
        /// </summary>
        public int FailedLoginAttempts { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the user account is locked.
        /// </summary>
        public bool? IsLocked { get; set; }

        /// <summary>
        /// Gets or sets the date and time when the account lockout ends.
        /// </summary>
        public DateTime? LockoutEndTime { get; set; }

        /// <summary>
        /// Gets or sets the user's status message.
        /// </summary>
        [StringLength(255)]
        public string? StatusMessage { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the user account is active.
        /// </summary>
        public bool? IsActive { get; set; }
        
        /// <summary>
        /// Gets or sets the date and time when the user was last updated.
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        // --- Navigation Properties ---
        
        /// <summary>
        /// Gets or sets the collection of requests associated with this user.
        /// </summary>
        /// <remarks>
        /// EF Core won't load this automatically without explicit Include.
        /// </remarks>
        public virtual ICollection<RequestEntity> Requests { get; set; } = new List<RequestEntity>();

        /// <summary>
        /// Gets or sets the annual leave information for this user.
        /// </summary>
        /// <remarks>
        /// This is a one-to-one relationship based on the UserID key.
        /// </remarks>
        public virtual AnnualLeaveEntity? AnnualLeave { get; set; }

        // Add other navigation properties if needed for Messages, Notifications etc.
        // /// <summary>
        // /// Gets or sets the collection of messages sent by this user.
        // /// </summary>
        // public virtual ICollection<MessageEntity> SentMessages { get; set; } = new List<MessageEntity>();
        
        // /// <summary>
        // /// Gets or sets the collection of messages received by this user.
        // /// </summary>
        // public virtual ICollection<MessageEntity> ReceivedMessages { get; set; } = new List<MessageEntity>();
        
        // /// <summary>
        // /// Gets or sets the collection of notifications for this user.
        // /// </summary>
        // public virtual ICollection<NotificationEntity> Notifications { get; set; } = new List<NotificationEntity>();
    }
} 