using System.ComponentModel.DataAnnotations;

namespace TDFShared.DTOs.Users
{
    /// <summary>
    /// Data transfer object for registering a push notification token
    /// </summary>
    public class PushTokenRegistrationDto
    {
        /// <summary>
        /// The push notification token from the device
        /// </summary>
        [Required]
        [StringLength(500)]
        public required string Token { get; set; }

        /// <summary>
        /// The platform the token is for (ios, android, windows, macos)
        /// </summary>
        [Required]
        [StringLength(50)]
        public required string Platform { get; set; }

        /// <summary>
        /// The name of the device
        /// </summary>
        [StringLength(100)]
        public required string DeviceName { get; set; }

        /// <summary>
        /// The model of the device
        /// </summary>
        [StringLength(100)]
        public required string DeviceModel { get; set; }

        /// <summary>
        /// The version of the app
        /// </summary>
        [StringLength(50)]
        public required string AppVersion { get; set; }
    }
} 