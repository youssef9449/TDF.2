using System;
using System.Text.Json.Serialization;
using TDFShared.DTOs.Users;

namespace TDFShared.DTOs.Auth
{
    /// <summary>
    /// Response returned after successful authentication
    /// </summary>
    public class TokenResponse
    {
        /// <summary>
        /// JWT token for API authentication
        /// </summary>
        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;

        /// <summary>
        /// Username
        /// </summary>
        [JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// fullname
        /// </summary>
        [JsonPropertyName("fullName")]
        public string FullName { get; set; } = string.Empty;

        /// <summary>
        /// Token expiration timestamp
        /// </summary>
        [JsonPropertyName("expiration")]
        public DateTime Expiration { get; set; }

        /// <summary>
        /// Flag indicating if the user has administrator privileges
        /// </summary>
        [JsonPropertyName("isAdmin")]
        public bool IsAdmin { get; set; } = false;

        /// <summary>
        /// Flag indicating if the user has manager privileges
        /// </summary>
        [JsonPropertyName("isManager")]
        public bool IsManager { get; set; } = false;

        /// <summary>
        /// Flag indicating if the user has HR privileges
        /// </summary>
        [JsonPropertyName("isHR")]
        public bool IsHR { get; set; } = false;

        /// <summary>
        /// Flag indicating if the user RequiresPasswordChange
        /// </summary>
        [JsonPropertyName("requiresPasswordChange")]
        public bool RequiresPasswordChange { get; set; } = false;

        /// <summary>
        /// UserId
        /// </summary>
        [JsonPropertyName("userId")]
        public int UserId { get; set; }

        /// <summary>
        /// Refresh Token expiration timestamp
        /// </summary>
        [JsonPropertyName("refreshTokenExpiration")]
        public DateTime RefreshTokenExpiration { get; set; }

        /// <summary>
        /// Refresh token for obtaining a new access token
        /// </summary>
        [JsonPropertyName("refreshToken")]
        public string? RefreshToken { get; set; }
        
        /// <summary>
        /// Information about the authenticated user
        /// </summary>
        [JsonPropertyName("user")]
        public UserDto User { get; set; } = new UserDto();
        
        /// <summary>
        /// User's role(s) for authorization
        /// </summary>
        [JsonPropertyName("roles")]
        public string[] Roles { get; set; } = Array.Empty<string>();
    }
}