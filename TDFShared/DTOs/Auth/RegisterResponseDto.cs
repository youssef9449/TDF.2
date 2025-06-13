namespace TDFShared.DTOs.Auth
{
    /// <summary>
    /// Response DTO for user registration operations
    /// </summary>
    public class RegisterResponseDto
    {
        /// <summary>
        /// Indicates whether the registration was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Message describing the registration result
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// User details for the newly registered user
        /// </summary>
        public UserDetailsDto UserDetails { get; set; } = new();
    }
}