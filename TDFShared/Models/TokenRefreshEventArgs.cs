using System;

namespace TDFShared.Models
{
    /// <summary>
    /// Event arguments for token refresh events
    /// </summary>
    public class TokenRefreshEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the access token
        /// </summary>
        public string AccessToken { get; }

        /// <summary>
        /// Gets the refresh token
        /// </summary>
        public string RefreshToken { get; }

        /// <summary>
        /// Gets when the token expires
        /// </summary>
        public DateTime ExpiresAt { get; }

        /// <summary>
        /// Creates a new instance of TokenRefreshEventArgs with default values
        /// </summary>
        public TokenRefreshEventArgs()
        {
            AccessToken = string.Empty;
            RefreshToken = string.Empty;
            ExpiresAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Creates a new instance of TokenRefreshEventArgs with the specified values
        /// </summary>
        /// <param name="accessToken">The access token</param>
        /// <param name="refreshToken">The refresh token</param>
        /// <param name="expiresAt">When the token expires</param>
        public TokenRefreshEventArgs(string accessToken, string refreshToken, DateTime expiresAt)
        {
            AccessToken = accessToken;
            RefreshToken = refreshToken;
            ExpiresAt = expiresAt;
        }
    }
} 