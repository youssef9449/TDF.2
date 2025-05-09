using System.Text.Json.Serialization;

namespace TDFShared.DTOs.Auth
{
    /// <summary>
    /// Request for refreshing an authentication token
    /// </summary>
    public class RefreshTokenRequest
    {
        /// <summary>
        /// The expired or current JWT token
        /// </summary>
        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;

        /// <summary>
        /// The refresh token used to obtain a new access token
        /// </summary>
        [JsonPropertyName("refreshToken")]
        public string RefreshToken { get; set; } = string.Empty;
    }
} 