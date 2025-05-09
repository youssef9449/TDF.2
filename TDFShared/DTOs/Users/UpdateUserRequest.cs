using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace TDFShared.DTOs.Users
{
    /// <summary>
    /// DTO for updating an existing user
    /// </summary>
    public class UpdateUserRequest
    {
        public int UserID { get; set; }

        public string Username { get; set; }

        /// <summary>
        /// User's full name
        /// </summary>
        [JsonPropertyName("fullName")]
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
        /// URL to user's profile image
        /// </summary>
        [JsonPropertyName("picture")]
        public byte[] Picture { get; set; }
        
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
        /// Whether the user is a HR
        /// </summary>
        [JsonPropertyName("IsHR")]
        public bool IsHR { get; set; }

        /// <summary>
        /// Whether the user account is active
        /// </summary>
        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; }
    }
} 