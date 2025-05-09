using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace TDFShared.DTOs.Users
{
    /// <summary>
    /// DTO for a user updating their own profile information.
    /// </summary>
    public class UpdateMyProfileRequest
    {
        /// <summary>
        /// User's full name
        /// </summary>
        [JsonPropertyName("fullName")]
        [Required(ErrorMessage = "Full name is required.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Full name must be between 2 and 100 characters")]
        public string FullName { get; set; }

        /// <summary>
        /// User's department (optional, may not be self-editable depending on policy)
        /// </summary>
        [JsonPropertyName("department")]
        [StringLength(100, ErrorMessage = "Department cannot exceed 100 characters")]
        public string Department { get; set; } // Consider if this should be editable by user

        /// <summary>
        /// User's job title (optional, may not be self-editable depending on policy)
        /// </summary>
        [JsonPropertyName("title")]
        [StringLength(100, ErrorMessage = "Title cannot exceed 100 characters")]
        public string Title { get; set; } // Consider if this should be editable by user

        [StringLength(255)]
        public string? StatusMessage { get; set; }
    }
} 