using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace TDFShared.DTOs.Users
{
    /// <summary>
    /// Data transfer object for updating an existing user
    /// </summary>
    public class UserUpdateDto
    {
        /// <summary>
        /// Full name of the user
        /// </summary>
        [JsonPropertyName("fullName")]
        public string FullName { get; set; } = string.Empty;
        
        /// <summary>
        /// Department of the user
        /// </summary>
        [JsonPropertyName("department")]
        public string Department { get; set; } = string.Empty;
        
        /// <summary>
        /// Job title of the user
        /// </summary>
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;
        
        /// <summary>
        /// Flag indicating if the user account is active
        /// </summary>
        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; } = true;
        
        /// <summary>
        /// Roles assigned to the user
        /// </summary>
        [JsonPropertyName("roles")]
        public string[]? Roles { get; set; }
    }
} 