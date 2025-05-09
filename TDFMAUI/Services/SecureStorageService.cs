using System.Text.Json;
using TDFMAUI.Config;

namespace TDFMAUI.Services
{
    public class SecureStorageService
    {
        private const string TokenKey = "auth_token";
        private const string TokenExpirationKey = "token_expiration";
        
        public async Task SaveTokenAsync(string token, DateTime expiration)
        {
            try
            {
                await SecureStorage.SetAsync(TokenKey, token);
                await SecureStorage.SetAsync(TokenExpirationKey, expiration.ToString("o"));
                
                // Also update in-memory values
                ApiConfig.CurrentToken = token;
                ApiConfig.TokenExpiration = expiration;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving token: {ex.Message}");
            }
        }
        
        public async Task<(string Token, DateTime Expiration)> GetTokenAsync()
        {
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
    }
} 