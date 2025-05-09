using System.Text.Json.Serialization;

namespace TDFShared.DTOs.Users
{
    /// <summary>
    /// DTO for changing a user's password
    /// </summary>
    public class ChangePasswordDto
    {
        /// <summary>
        /// User's current password for verification
        /// </summary>
        [JsonPropertyName("currentPassword")]
        public string CurrentPassword { get; set; } = string.Empty;
        
        /// <summary>
        /// New password to set for the user
        /// </summary>
        [JsonPropertyName("newPassword")]
        public string NewPassword { get; set; } = string.Empty;
        
        /// <summary>
        /// Confirmation of the new password
        /// </summary>
        [JsonPropertyName("confirmPassword")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
} 