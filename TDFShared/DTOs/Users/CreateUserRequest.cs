using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

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
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters")]
        public string Username { get; set; }
        
        /// <summary>
        /// User's password
        /// </summary>
        [JsonPropertyName("password")]
        [Required(ErrorMessage = "Password is required")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be between 6 and 100 characters")]
        public string Password { get; set; }
        
        /// <summary>
        /// User's full name
        /// </summary>
        [JsonPropertyName("fullName")]
        [Required(ErrorMessage = "Full name is required")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Full name must be between 2 and 100 characters")]
        public string FullName { get; set; }
        
        /// <summary>
        /// User's department
        /// </summary>
        [JsonPropertyName("department")]
        [StringLength(100, ErrorMessage = "Department cannot exceed 100 characters")]
        public string Department { get; set; }
        
        /// <summary>
        /// User's job title
        /// </summary>
        [JsonPropertyName("title")]
        [StringLength(100, ErrorMessage = "Title cannot exceed 100 characters")]
        public string Title { get; set; }
        
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