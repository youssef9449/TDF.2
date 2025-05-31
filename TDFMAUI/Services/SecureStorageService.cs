using System.Text.Json;
using TDFMAUI.Config;
using TDFMAUI.Helpers;
using Microsoft.Extensions.Logging;

namespace TDFMAUI.Services
{
    public class SecureStorageService
    {
        private const string TokenKey = "auth_token";
        private const string TokenExpirationKey = "token_expiration";
        private readonly ILogger<SecureStorageService> _logger;

        public SecureStorageService(ILogger<SecureStorageService> logger = null)
        {
            _logger = logger;
        }

        public async Task SaveTokenAsync(string token, DateTime expiration)
        {
            try
            {
                // Always update in-memory values regardless of platform
                ApiConfig.CurrentToken = token;
                ApiConfig.TokenExpiration = expiration;

                // Only persist token to secure storage if platform allows it
                if (ShouldPersistToken())
                {
                    _logger?.LogInformation("Saving token to secure storage (mobile platform)");
                    await SecureStorage.SetAsync(TokenKey, token);
                    await SecureStorage.SetAsync(TokenExpirationKey, expiration.ToString("o"));
                }
                else
                {
                    _logger?.LogInformation("Token not persisted to secure storage (desktop platform)");
                    // For desktop platforms, we don't persist tokens
                    // The token will still be available in memory during the current session
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving token: {ex.Message}");
                _logger?.LogError(ex, "Error saving token");
            }
        }

        public async Task<(string Token, DateTime Expiration)> GetTokenAsync()
        {
            // On desktop, check in-memory token first
            if (DeviceHelper.IsDesktop)
            {
                if (!string.IsNullOrEmpty(ApiConfig.CurrentToken) && ApiConfig.TokenExpiration > DateTime.UtcNow)
                {
                    return (ApiConfig.CurrentToken, ApiConfig.TokenExpiration);
                }
                // If not set, fall through to try SecureStorage (should be empty)
            }

            try
            {
                string token = await SecureStorage.GetAsync(TokenKey);
                string expirationString = await SecureStorage.GetAsync(TokenExpirationKey);

                if (!string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(expirationString))
                {
                    DateTime expiration = DateTime.Parse(expirationString);

                    // Update in-memory values
                    ApiConfig.CurrentToken = token;
                    ApiConfig.TokenExpiration = expiration;

                    return (token, expiration);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting token: {ex.Message}");
            }

            return (null, DateTime.MinValue);
        }

        public async Task ClearTokenAsync()
        {
            try
            {
                SecureStorage.Remove(TokenKey);
                SecureStorage.Remove(TokenExpirationKey);

                // Also clear in-memory values
                ApiConfig.CurrentToken = null;
                ApiConfig.TokenExpiration = DateTime.MinValue;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error clearing token: {ex.Message}");
            }
        }

        // Alias method for ClearTokenAsync for compatibility
        public async Task RemoveTokenAsync()
        {
            await ClearTokenAsync();
        }

        /// <summary>
        /// Checks if token persistence is enabled for the current platform
        /// </summary>
        /// <returns>True if tokens should be persisted, false otherwise</returns>
        public bool ShouldPersistToken()
        {
            // On desktop platforms (Windows/Mac), don't persist tokens
            if (DeviceHelper.IsDesktop)
            {
                _logger?.LogInformation("Token persistence disabled for desktop platform");
                return false;
            }

            // On mobile platforms, persist tokens
            _logger?.LogInformation("Token persistence enabled for mobile platform");
            return true;
        }

        /// <summary>
        /// Handles token persistence based on platform
        /// </summary>
        /// <returns>True if a valid token exists and should be used, false otherwise</returns>
        public async Task<bool> HandleTokenPersistenceAsync()
        {
            // For desktop platforms, always clear tokens on app start
            if (DeviceHelper.IsDesktop)
            {
                _logger?.LogInformation("Clearing token on desktop platform at app start");
                await ClearTokenAsync();
                return false;
            }

            // For mobile platforms, check if we have a valid token
            var (token, expiration) = await GetTokenAsync();
            bool hasValidToken = !string.IsNullOrEmpty(token) && expiration > DateTime.UtcNow;

            _logger?.LogInformation("Mobile platform token check: Valid token exists = {HasValidToken}", hasValidToken);
            return hasValidToken;
        }
    }
}