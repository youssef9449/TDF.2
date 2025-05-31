using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TDFShared.Models.User;

namespace TDFAPI.Models
{
    /// <summary>
    /// Represents a device's push notification token for sending notifications
    /// </summary>
    public class PushToken
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        [StringLength(500)]
        public string Token { get; set; }

        [Required]
        [StringLength(50)]
        public string Platform { get; set; } // "ios", "android", "windows", "macos"

        [Required]
        public DateTime CreatedAt { get; set; }

        [Required]
        public DateTime LastUsedAt { get; set; }

        public bool IsActive { get; set; } = true;

        [StringLength(100)]
        public string DeviceName { get; set; }

        [StringLength(100)]
        public string DeviceModel { get; set; }

        [StringLength(50)]
        public string AppVersion { get; set; }

        [ForeignKey("UserId")]
        public virtual UserEntity User { get; set; }
    }
} 