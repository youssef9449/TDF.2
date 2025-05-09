namespace TDFShared.DTOs.Auth
{
    // Represents the response after a registration attempt.
    // Could include user details or just success status.
    public class RegisterResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public UserDetailsDto? UserDetails { get; set; } // Optional: return user details on success
    }
}