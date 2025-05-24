using System.ComponentModel.DataAnnotations;
using TDFShared.Validation;

namespace TDFShared.DTOs.Auth
{
    /// <summary>
    /// Data transfer object for login requests
    /// </summary>
    public class LoginRequestDto
    {
        /// <summary>
        /// The username of the user attempting to log in
        /// </summary>
        [Required(ErrorMessage = "Username is required")]
        [Username]
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// The password of the user attempting to log in
        /// </summary>
        [Required(ErrorMessage = "Password is required")]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters long")]
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Optional device identifier for tracking login sessions
        /// </summary>
        public string? DeviceId { get; set; }

    }
}