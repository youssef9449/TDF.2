namespace TDFShared.DTOs.Auth
{
    public class LoginResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public UserDetailsDto? UserDetails { get; set; }
        // Optionally include token expiration, refresh token, etc.
    }
} 