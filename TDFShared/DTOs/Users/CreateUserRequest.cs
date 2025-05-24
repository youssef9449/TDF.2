using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using TDFShared.Validation;

namespace TDFShared.DTOs.Users
{
    /// <summary>
    /// DTO for creating a new user
    /// </summary>
    public class CreateUserRequest
    {
        /// <summary>
        /// User's login username
        /// </summary>
        [JsonPropertyName("username")]
        [Required(ErrorMessage = "Username is required")]
        [Username]
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// User's password
        /// </summary>
        [JsonPropertyName("password")]
        [Required(ErrorMessage = "Password is required")]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters long")]
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// User's full name
        /// </summary>
        [JsonPropertyName("fullName")]
        [Required(ErrorMessage = "Full name is required")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Full name must be between 2 and 100 characters")]
        public string FullName { get; set; } = string.Empty;

        /// <summary>
        /// User's department
        /// </summary>
        [JsonPropertyName("department")]
        [Required(ErrorMessage = "Department is required")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Department must be between 2 and 100 characters")]
        public string Department { get; set; } = string.Empty;

        /// <summary>
        /// User's job title
        /// </summary>
        [JsonPropertyName("title")]
        [Required(ErrorMessage = "Title is required")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Title must be between 2 and 100 characters")]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Whether the user is an administrator
        /// </summary>
        [JsonPropertyName("isAdmin")]
        public bool IsAdmin { get; set; }

        /// <summary>
        /// Whether the user is a manager
        /// </summary>
        [JsonPropertyName("isManager")]
        public bool IsManager { get; set; }

        /// <summary>
        /// Whether the user is a manager
        /// </summary>
        [JsonPropertyName("IsHR")]
        public bool IsHR { get; set; }
    }
}