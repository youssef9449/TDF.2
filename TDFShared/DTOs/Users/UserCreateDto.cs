using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace TDFShared.DTOs.Users
{
    /// <summary>
    /// Data transfer object for creating a new user
    /// </summary>
    public class UserCreateDto
    {
        /// <summary>
        /// Username for login
        /// </summary>
        [Required]
        [JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;
        
        /// <summary>
        /// Password for the new account
        /// </summary>
        [Required]
        [JsonPropertyName("password")]
        public string Password { get; set; } = string.Empty;
        
        /// <summary>
        /// Full name of the user
        /// </summary>
        [Required]
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
        /// Roles assigned to the user
        /// </summary>
        [JsonPropertyName("roles")]
        public string[] Roles { get; set; } = System.Array.Empty<string>();
    }
} 