using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TDFMAUI.Helpers;
using TDFShared.Contracts;

namespace TDFMAUI.Services.WebSocket
{
    /// <summary>
    /// Default <see cref="IWebSocketTokenProvider"/>. Desktop reads the in-memory
    /// <see cref="IUserSessionService"/> token; mobile consults secure storage and falls
    /// back to <see cref="TDFShared.Contracts.IAuthClient.RefreshTokenAsync(string, string)"/>
    /// when the current token is missing or expired.
    /// </summary>
    public sealed class WebSocketTokenProvider : IWebSocketTokenProvider
    {
        private readonly ILogger<WebSocketTokenProvider> _logger;
        private readonly SecureStorageService _secureStorage;
        private readonly TDFShared.Contracts.IAuthClient _authService;
        private readonly IUserSessionService _userSessionService;

        public WebSocketTokenProvider(
            ILogger<WebSocketTokenProvider> logger,
            SecureStorageService secureStorage,
            TDFShared.Contracts.IAuthClient authService,
            IUserSessionService userSessionService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _secureStorage = secureStorage ?? throw new ArgumentNullException(nameof(secureStorage));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));
        }

        public async Task<string?> GetValidTokenAsync(string? providedToken = null)
        {
            try
            {
                if (!string.IsNullOrEmpty(providedToken))
                {
                    _logger.LogDebug("Using provided token for WebSocket connection");
                    return providedToken;
                }

                string? tokenToValidate;
                DateTime tokenExpiry;

                if (DeviceHelper.IsDesktop)
                {
                    tokenToValidate = _userSessionService.CurrentToken;
                    tokenExpiry = _userSessionService.TokenExpiration;
                }
                else
                {
                    var (storedToken, expiration) = await _secureStorage.GetTokenAsync();
                    tokenToValidate = storedToken;
                    tokenExpiry = expiration;
                }

                if (!string.IsNullOrEmpty(tokenToValidate) && tokenExpiry > DateTime.UtcNow)
                {
                    _logger.LogDebug("Using existing valid token for WebSocket connection");
                    return tokenToValidate;
                }

                _logger.LogWarning("No valid existing token found, attempting to refresh.");

                var (currentToken, _) = await _secureStorage.GetTokenAsync();
                var (currentRefreshToken, _) = await _secureStorage.GetRefreshTokenAsync();

                if (!string.IsNullOrEmpty(currentToken) && !string.IsNullOrEmpty(currentRefreshToken))
                {
                    var refreshResult = await _authService.RefreshTokenAsync(currentToken, currentRefreshToken);
                    if (refreshResult != null)
                    {
                        _logger.LogInformation("Token refreshed successfully. Using new token for WebSocket connection.");
                        if (DeviceHelper.IsDesktop)
                        {
                            return _userSessionService.CurrentToken;
                        }

                        var (newToken, _) = await _secureStorage.GetTokenAsync();
                        return newToken;
                    }
                }

                _logger.LogWarning("No valid token available for WebSocket connection");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting valid token for WebSocket connection");
                return null;
            }
        }
    }
}
