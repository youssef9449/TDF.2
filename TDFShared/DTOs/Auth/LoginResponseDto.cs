/// <summary>
/// Response DTO for login operations
/// </summary>
namespace TDFShared.DTOs.Auth
{
    public class LoginResponseDto
    {
        /// <summary>
        /// JWT authentication token
        /// </summary>
        public string Token { get; set; } = string.Empty;

        /// <summary>
        /// User details including profile information
        /// </summary>
        public UserDetailsDto? UserDetails { get; set; }
        // Optionally include token expiration, refresh token, etc.
    }
} 