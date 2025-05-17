using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using TDFShared.Models.Request;

namespace TDFShared.Models.User
{
    // Represents the User entity corresponding to the dbo.Users table
    public class User
    {
        [Key]
        public int UserID { get; set; }

        [Required]
        [StringLength(50)]
        public string UserName { get; set; } // Mapped from UserName in SQL

        [Required]
        [StringLength(256)]
        public string PasswordHash { get; set; }

        [Required]
        [StringLength(128)]
        public string Salt { get; set; }

        [StringLength(100)]
        public string? FullName { get; set; }

        [StringLength(100)]
        public string? Title { get; set; }

        [Required]
        [StringLength(255)]
        public string Department { get; set; }
 
        public byte[]? Picture { get; set; }

        public bool isConnected { get; set; } // Note: C# convention usually uses PascalCase (IsConnected)

        [StringLength(100)]
        public string? MachineName { get; set; }

        public DateTime? LastActivityTime { get; set; }
        /// <summary>
        /// Date and time when the user was created
        /// </summary>
        [JsonPropertyName("createdAt")]
        public DateTime? CreatedAt { get; set; }

        public int? PresenceStatus { get; set; }

        [StringLength(100)]
        public string? CurrentDevice { get; set; }

        public bool? IsAvailableForChat { get; set; }

        public bool? IsAdmin { get; set; }

        public bool? IsManager { get; set; }

        public bool? IsHR { get; set; }

        [StringLength(128)]
        public string? RefreshToken { get; set; }

        public DateTime? RefreshTokenExpiryTime { get; set; }

        public DateTime? LastLoginDate { get; set; }

        [StringLength(50)]
        public string? LastLoginIp { get; set; }

        public int? FailedLoginAttempts { get; set; }

        public bool? IsLocked { get; set; }

        public DateTime? LockoutEndTime { get; set; }

        [StringLength(255)]
        public string? StatusMessage { get; set; }

        // Ignored in DbContext configuration - managed in AnnualLeave table
        // public int? AnnualBalance { get; set; }
        // public int? CasualBalance { get; set; }

        public bool? IsActive { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // --- Navigation Properties ---
        // Restore Requests navigation property (EF Core won't load it automatically)
        public virtual ICollection<RequestEntity> Requests { get; set; } = new List<RequestEntity>();

        // To link to the AnnualLeave table (one-to-one based on UserID key)
        public virtual AnnualLeaveEntity? AnnualLeave { get; set; }

        // Add other navigation properties if needed for Messages, Notifications etc.
        // public virtual ICollection<MessageEntity> SentMessages { get; set; } = new List<MessageEntity>();
        // public virtual ICollection<MessageEntity> ReceivedMessages { get; set; } = new List<MessageEntity>();
        // public virtual ICollection<NotificationEntity> Notifications { get; set; } = new List<NotificationEntity>();
    }
} 