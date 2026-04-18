using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Maui.ApplicationModel;
using TDFMAUI.Config;
using TDFMAUI.Helpers;
using TDFShared.Constants; // Added
using TDFShared.DTOs.Auth;
using TDFShared.DTOs.Common;
using TDFShared.DTOs.Users;
using TDFShared.Enums;
using TDFShared.Services;
using TDFShared.Helpers;
using Microsoft.Maui.Storage;
using Microsoft.Maui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;

namespace TDFMAUI.Services;

/// <summary>
/// Implementation of IAuthService that handles token management
/// </summary>
public class AuthService : IAuthService
{
    private readonly SecureStorageService _secureStorageService;
    private readonly IUserProfileService _userProfileService;
    private readonly IHttpClientService _httpClientService;
    private readonly ILogger<AuthService> _logger;
    private readonly ISecurityService _securityService;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly string _baseApiUrl;
    private readonly IRoleService _roleService;
    private readonly IAuthApiService _authApiService;
    private readonly IUserApiService _userApiService;
    private readonly ISecureStorage _secureStorage;
    private readonly IUserSessionService _userSessionService;
    private readonly IUserPresenceService _userPresenceService;
    private readonly IWebSocketService _webSocketService;
    private string? _currentToken;
    private DateTime _tokenExpiration = DateTime.MinValue;
    private readonly SemaphoreSlim _refreshSemaphore = new(1, 1);

    public AuthService(
        SecureStorageService secureStorageService,
        IUserProfileService userProfileService,
        IHttpClientService httpClientService,
        ISecurityService securityService,
        ILogger<AuthService> logger,
        IRoleService roleService,
        IAuthApiService authApiService,
        IUserApiService userApiService,
        ISecureStorage secureStorage,
        IUserSessionService userSessionService,
        IUserPresenceService userPresenceService,
        IWebSocketService webSocketService)
    {
        try
        {
            // Log entry immediately, before any potential null checks
            logger?.LogInformation("AuthService constructor started.");

            // Log each dependency resolution
            logger?.LogInformation("Checking SecureStorageService...");
            _secureStorageService = secureStorageService ?? throw new ArgumentNullException(nameof(secureStorageService));
            logger?.LogInformation("SecureStorageService resolved successfully.");

            logger?.LogInformation("Checking UserProfileService...");
            _userProfileService = userProfileService ?? throw new ArgumentNullException(nameof(userProfileService));
            logger?.LogInformation("UserProfileService resolved successfully.");

            logger?.LogInformation("Checking HttpClientService...");
            _httpClientService = httpClientService ?? throw new ArgumentNullException(nameof(httpClientService));
            logger?.LogInformation("HttpClientService resolved successfully.");

            logger?.LogInformation("Checking SecurityService...");
            _securityService = securityService ?? throw new ArgumentNullException(nameof(securityService));
            logger?.LogInformation("SecurityService resolved successfully.");

            logger?.LogInformation("Checking RoleService...");
            _roleService = roleService ?? throw new ArgumentNullException(nameof(roleService));
            logger?.LogInformation("RoleService resolved successfully.");

            logger?.LogInformation("Checking AuthApiService...");
            _authApiService = authApiService ?? throw new ArgumentNullException(nameof(authApiService));
            logger?.LogInformation("AuthApiService resolved successfully.");

            logger?.LogInformation("Checking UserApiService...");
            _userApiService = userApiService ?? throw new ArgumentNullException(nameof(userApiService));
            logger?.LogInformation("UserApiService resolved successfully.");

            logger?.LogInformation("Checking SecureStorage...");
            _secureStorage = secureStorage ?? throw new ArgumentNullException(nameof(secureStorage));
            logger?.LogInformation("SecureStorage resolved successfully.");

            logger?.LogInformation("Checking UserSessionService...");
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));
            logger?.LogInformation("UserSessionService resolved successfully.");

            logger?.LogInformation("Checking UserPresenceService...");
            _userPresenceService = userPresenceService ?? throw new ArgumentNullException(nameof(userPresenceService));
            logger?.LogInformation("UserPresenceService resolved successfully.");

            logger?.LogInformation("Checking WebSocketService...");
            _webSocketService = webSocketService ?? throw new ArgumentNullException(nameof(webSocketService));
            logger?.LogInformation("WebSocketService resolved successfully.");

            logger?.LogInformation("Saving logger reference...");
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            logger?.LogInformation("Getting ApiConfig.BaseUrl...");
            _baseApiUrl = ApiConfig.BaseUrl ?? throw new InvalidOperationException("API Base URL is not configured.");
            // Ensure _baseApiUrl doesn't end with a slash to prevent double slashes in URL construction
            _baseApiUrl = _baseApiUrl.TrimEnd('/');
            logger?.LogInformation("BaseUrl obtained: {BaseUrl}", _baseApiUrl);

            logger?.LogInformation("Initializing JsonSerializerOptions...");
            _serializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            _logger?.LogInformation("AuthService constructor finished successfully.");

            // Subscribe to token refresh events from the HTTP client
            _httpClientService.TokenRefreshNeeded += OnTokenRefreshNeeded;
        }
        catch (Exception ex)
        {
            logger?.LogCritical(ex, "ERROR in AuthService constructor");
            throw; // Rethrow so DI container knows there was a failure
        }
    }

    public async Task<TokenResponse?> LoginAsync(string username, string password)
    {
        var loginRequest = new LoginRequestDto
        {
            Username = username,
            Password = password
        };
        var endpoint = "auth/login";
        _logger.LogInformation("Attempting login for user {Username} via {Endpoint}", username, endpoint);

        try
        {
            // Use the API service directly for login
            var apiResponse = await _authApiService.LoginAsync(loginRequest);

            if (apiResponse?.Success == true && apiResponse.Data != null && !string.IsNullOrEmpty(apiResponse.Data.Token))
            {
                var tokenData = apiResponse.Data;
                _logger.LogInformation("Login successful for user {Username}", username);

                // Create UserDetailsDto from TokenResponse data
                var userDetails = new UserDetailsDto
                {
                    UserId = tokenData.UserId,
                    FullName = tokenData.FullName ?? string.Empty,
                    UserName = tokenData.Username ?? string.Empty,
                    Department = tokenData.User?.Department,
                    IsAdmin = tokenData.IsAdmin,
                    IsManager = tokenData.IsManager,
                    IsHR = tokenData.IsHR,
                    Roles = tokenData.Roles?.ToList() ?? new List<string>()
                };

                // Add roles based on bit fields if Roles list is empty
                if (userDetails.Roles.Count == 0)
                {
                    if (tokenData.IsAdmin) userDetails.Roles.Add("Admin");
                    if (tokenData.IsManager) userDetails.Roles.Add("Manager");
                    if (tokenData.IsHR) userDetails.Roles.Add("HR");
                    if (userDetails.Roles.Count == 0) userDetails.Roles.Add("User");
                }

                // Token storage and HttpClient setting is already partially handled by IAuthApiService.LoginAsync
                // But we ensure consistency here across all services
                await _secureStorageService.SaveTokenAsync(tokenData.Token, tokenData.Expiration, tokenData.RefreshToken, tokenData.RefreshTokenExpiration);
                _userProfileService.SetUserDetails(userDetails);
                await _httpClientService.SetAuthenticationTokenAsync(tokenData.Token);

                // Create and set current user via UserSessionService
                var currentUser = new TDFShared.DTOs.Users.UserDto
                {
                    UserID = userDetails.UserId,
                    UserName = userDetails.UserName ?? string.Empty,
                    FullName = userDetails.FullName ?? string.Empty,
                    Department = userDetails.Department,
                    Roles = userDetails.Roles,
                    IsAdmin = userDetails.IsAdmin,
                    IsManager = userDetails.IsManager,
                    IsHR = userDetails.IsHR
                };

                _userSessionService.SetCurrentUser(currentUser);
                _userSessionService.SetTokens(tokenData.Token, tokenData.Expiration, tokenData.RefreshToken, tokenData.RefreshTokenExpiration);

                // Update ApiConfig for desktop fallback
                if (DeviceHelper.IsDesktop)
                {
                    ApiConfig.CurrentToken = tokenData.Token;
                    ApiConfig.TokenExpiration = tokenData.Expiration;
                }

                _logger.LogInformation("User {Username} session initialized with roles: {Roles}",
                    username, string.Join(", ", userDetails.Roles));

                return tokenData;
            }

            // If we get here, login failed
            string errorMessage = apiResponse?.Message ?? "Login failed: Invalid credentials or unexpected response from server";
            _logger.LogWarning("Login failed for user {Username}: {Error}", username, errorMessage);
            throw new InvalidOperationException(errorMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for {Username}", username);
            throw; // Rethrow to allow the ViewModel to handle the error appropriately
        }
    }

    /// <inheritdoc />
    public async Task<bool> LogoutAsync()
    {
        _logger.LogInformation("Logging out user.");
        bool success = true;

        try
        {
            // 1. Try to update user status to Offline via the presence service
            try
            {
                if (_userPresenceService != null)
                {
                    await _userPresenceService.UpdateStatusAsync(UserPresenceStatus.Offline, "Logging out");
                }
            }
            catch (Exception statusEx)
            {
                _logger.LogWarning("Failed to update user status to Offline during logout: {Message}", statusEx.Message);
            }

            // 2. Call the API to logout (server-side cleanup)
            try
            {
                await _authApiService.LogoutAsync();
            }
            catch (Exception apiEx)
            {
                _logger.LogWarning("Failed to call logout API endpoint: {Message}", apiEx.Message);
            }

            // 3. Clear all local state
            try
            {
                // Clear user details in profile service
                _userProfileService.ClearUserDetails();

                // Clear all token data from secure storage
                await _secureStorageService.RemoveTokenAsync();
                
                // Clear the HttpClient authentication token
                await _httpClientService.ClearAuthenticationTokenAsync();

                // Clear session service (this also clears secure storage as backup)
                await _userSessionService.ClearSessionAsync();

                // Clear local in-memory cache
                _currentToken = null;
                _tokenExpiration = DateTime.MinValue;

                // Reset ApiConfig
                ApiConfig.CurrentToken = null;
                ApiConfig.TokenExpiration = DateTime.MinValue;

                _logger.LogInformation("All local user session state cleared during logout.");
            }
            catch (Exception clearEx)
            {
                _logger.LogError(clearEx, "Error clearing local state during logout.");
                success = false;
            }

            // 4. Disconnect WebSocket
            try
            {
                await _webSocketService.DisconnectAsync();
            }
            catch (Exception wsEx)
            {
                _logger.LogWarning("Failed to disconnect WebSocket during logout: {Message}", wsEx.Message);
            }

            // 5. Navigate back to the login page
            try
            {
                if (Shell.Current != null)
                {
                    await Shell.Current.GoToAsync("//LoginPage");
                    _logger.LogInformation("Navigated to LoginPage after logout.");
                }
            }
            catch (Exception navEx)
            {
                _logger.LogError(navEx, "Failed to navigate to LoginPage after logout.");
                success = false;
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during logout process.");
            return false;
        }
    }

    public async Task<int> GetCurrentUserIdAsync()
    {
        try
        {
            // First try to get user ID from the session service (faster)
            var currentUserId = _userSessionService.GetCurrentUserId();
            if (currentUserId > 0)
            {
                _logger.LogInformation("Retrieved User ID {UserId} from UserSessionService", currentUserId);
                return currentUserId;
            }

            // Fallback to token parsing if session service doesn't have the user
            var (token, _) = await _secureStorageService.GetTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("No authentication token found");
                return 0;
            }

            var handler = new JwtSecurityTokenHandler();
            if (handler.CanReadToken(token))
            {
                var jwtToken = handler.ReadJwtToken(token);
                var userIdClaim = jwtToken.Claims.FirstOrDefault(claim =>
                    claim.Type == ClaimTypes.NameIdentifier ||
                    claim.Type == "sub" ||
                    claim.Type == "nameid");

                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                    _logger.LogInformation("Successfully retrieved User ID {UserId} from token", userId);
                    return userId;
                }

                _logger.LogWarning("Token does not contain a valid user ID claim");
            }
            else
            {
                _logger.LogWarning("Stored token is not a valid JWT format");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving or parsing token for user ID");
        }

        return 0;
    }

    public async Task<string?> GetCurrentUserDepartmentAsync()
    {
        try
        {
            var (token, _) = await _secureStorageService.GetTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("No authentication token found for getting department");
                return null;
            }

            var handler = new JwtSecurityTokenHandler();
            if (handler.CanReadToken(token))
            {
                var jwtToken = handler.ReadJwtToken(token);
                var departmentClaim = jwtToken.Claims
                    .FirstOrDefault(c => c.Type == "department" || c.Type == "dept");

                if (departmentClaim != null)
                {
                    _logger.LogDebug("Found department claim: {Department}", departmentClaim.Value);
                    return departmentClaim.Value;
                }

                _logger.LogWarning("No department claim found in token");
            }
            else
            {
                _logger.LogWarning("Stored token is not a valid JWT format");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user department from token");
        }

        return null;
    }

    public async Task RevokeTokenAsync(string jti, DateTime expiryDateUtc, int? userId = null)
    {
        _ = jti ?? throw new ArgumentNullException(nameof(jti));

        try
        {
            // Call the API to revoke the token on the server
            _logger.LogInformation("Revoking token with JTI: {Jti}", jti);
            var endpoint = "auth/revoke-token";
            var request = new { Jti = jti, ExpiryDateUtc = expiryDateUtc, UserId = userId };

            await _httpClientService.PostAsync(endpoint, request);
            _logger.LogInformation("Token revoked successfully on server");

            // Also remove the token from local secure storage
            await _secureStorageService.RemoveTokenAsync();
            _logger.LogInformation("Token removed from local storage (JTI: {Jti}, UserId: {UserId})", jti, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking token (JTI: {Jti})", jti);
            throw; // Re-throw to allow callers to handle the error
        }
    }

    public async Task<IReadOnlyList<string>> GetUserRolesAsync()
    {
        var roles = new List<string>();
        const string roleClaimType = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";

        try
        {
            var (token, _) = await _secureStorageService.GetTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("No authentication token found for getting roles");
                return roles.AsReadOnly();
            }

            var handler = new JwtSecurityTokenHandler();
            if (handler.CanReadToken(token))
            {
                var jwtToken = handler.ReadJwtToken(token);
                var roleClaims = jwtToken.Claims
                    .Where(c => c.Type == "role" || c.Type == ClaimTypes.Role || c.Type == roleClaimType);

                roles = roleClaims.Select(c => c.Value).Distinct().ToList();
                _logger.LogDebug("Found {RoleCount} roles in token", roles.Count);
            }
            else
            {
                _logger.LogWarning("Stored token is not a valid JWT format");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user roles from token");
        }

        return roles.AsReadOnly();
    }

    public async Task<UserDto?> GetCurrentUserAsync()
    {
        try
        {
            _logger.LogInformation("Getting current user details via UserApiService");

            // Use the UserApiService to get current user instead of making direct HTTP calls
            var apiResponse = await _userApiService.GetCurrentUserAsync();
            if (apiResponse?.Success == true && apiResponse.Data != null)
            {
                // Set current user via UserSessionService
                _userSessionService.SetCurrentUser(apiResponse.Data);
                _logger.LogInformation("Successfully retrieved current user details and set via UserSessionService: {UserName}", apiResponse.Data.FullName);
                return apiResponse.Data;
            }
            else
            {
                _logger.LogWarning("Failed to get current user from IUserApiService: {Message}", apiResponse?.Message);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving current user details");
            return null;
        }
    }

    public async Task<TokenResponse?> RefreshTokenAsync(string token, string refreshToken)
    {
        _logger.LogInformation("Attempting to refresh token");

        try
        {
            var refreshRequest = new RefreshTokenRequest { Token = token, RefreshToken = refreshToken };
            var response = await _httpClientService.PostAsync<RefreshTokenRequest, ApiResponse<TokenResponse>>(
                ApiRoutes.Auth.RefreshToken, refreshRequest);

            if (response?.Success == true && response.Data != null)
            {
                var tokenData = response.Data;
                await _secureStorageService.SaveTokenAsync(tokenData.Token, tokenData.Expiration, tokenData.RefreshToken, tokenData.RefreshTokenExpiration);

                // Update HttpClient
                await _httpClientService.SetAuthenticationTokenAsync(tokenData.Token);

                // Update ApiConfig for in-memory token on desktop
                if (DeviceHelper.IsDesktop)
                {
                    ApiConfig.CurrentToken = tokenData.Token;
                    ApiConfig.TokenExpiration = tokenData.Expiration;
                }

                // Update session service
                _userSessionService.SetTokens(tokenData.Token, tokenData.Expiration, tokenData.RefreshToken, tokenData.RefreshTokenExpiration);

                _logger.LogInformation("Token refreshed successfully");
                return tokenData;
            }

            _logger.LogWarning("Token refresh failed: {Message}", response?.Message ?? "No error message provided");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing token");
            return null;
        }
    }



    /// <summary>
    /// Hashes a password using the shared SecurityService
    /// </summary>
    public string HashPassword(string password, out string salt)
    {
        _logger.LogDebug("Hashing password using shared SecurityService");
        return _securityService.HashPassword(password, out salt);
    }

    /// <summary>
    /// Verifies a password using the shared SecurityService
    /// </summary>
    public bool VerifyPassword(string password, string storedHash, string salt)
    {
        try
        {
            bool isValid = _securityService.VerifyPassword(password, storedHash, salt);
            if (!isValid)
            {
                _logger.LogWarning("Password verification failed");
            }
            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying password");
            return false;
        }
    }

    public async Task<bool> IsTokenRevokedAsync(string jti)
    {
        _logger.LogDebug("Checking if token with JTI is revoked: {Jti}", jti);

        try
        {
            var endpoint = $"auth/is-token-revoked/{Uri.EscapeDataString(jti)}";

            var result = await _httpClientService.GetAsync<ApiResponse<bool>>(endpoint);

            if (result?.Data == true)
            {
                _logger.LogInformation("Token with JTI {Jti} is revoked", jti);
            }

            return result?.Data ?? false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if token with JTI {Jti} is revoked", jti);
            return true; // Default to considering token as revoked if there's an error
        }
    }

    /// <summary>
    /// Returns the current in-memory authentication token (if any)
    /// </summary>
    public async Task<string?> GetCurrentTokenAsync()
    {
        try
        {
            // Use the same logic as GetTokenAsync for consistency
            return await GetTokenAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current authentication token");
            return null;
        }
    }

    public async Task SetAuthenticationTokenAsync(string token)
    {
        _logger?.LogInformation("AuthService: Setting authentication token of length {Length}", token?.Length ?? 0);
        await _httpClientService.SetAuthenticationTokenAsync(token);
    }

    public async Task<string?> GetTokenAsync()
    {
        try
        {
            // If we have a valid in-memory token, return it
            if (!string.IsNullOrEmpty(_currentToken) && DateTime.UtcNow < _tokenExpiration)
            {
                _logger.LogDebug("Returning valid in-memory token");
                return _currentToken;
            }

            // Try to get token from secure storage
            var (storedToken, expiration) = await _secureStorageService.GetTokenAsync();
            if (!string.IsNullOrEmpty(storedToken))
            {
                _currentToken = storedToken;
                _tokenExpiration = expiration;
                _logger.LogDebug("Retrieved and cached token from secure storage");
                return storedToken;
            }

            // For desktop platform, also check ApiConfig as fallback
            if (DeviceHelper.IsDesktop && !string.IsNullOrEmpty(ApiConfig.CurrentToken))
            {
                _logger.LogDebug("Retrieved token from ApiConfig for desktop platform");
                _currentToken = ApiConfig.CurrentToken;
                _tokenExpiration = ApiConfig.TokenExpiration;
                return ApiConfig.CurrentToken;
            }

            _logger.LogWarning("No valid authentication token found");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting authentication token");
            return null;
        }
    }

    public async Task<string?> RefreshTokenAsync()
    {
        try
        {
            // Get the refresh token
            var (refreshToken, refreshExpiration) = await _secureStorageService.GetRefreshTokenAsync();
            if (string.IsNullOrEmpty(refreshToken))
            {
                _logger.LogWarning("No refresh token available");
                return null;
            }

            // Call your token refresh endpoint
            var newToken = await RefreshTokenFromServerAsync(refreshToken);
            if (!string.IsNullOrEmpty(newToken))
            {
                // RefreshTokenFromServerAsync already handles storage and in-memory updates
                _currentToken = ApiConfig.CurrentToken;
                _tokenExpiration = ApiConfig.TokenExpiration;
                return newToken;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing authentication token");
            return null;
        }
    }

    public async Task<bool> IsTokenValidAsync()
    {
        try
        {
            var token = await GetTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                return false;
            }

            // Check if token is expired
            if (DateTime.UtcNow >= _tokenExpiration)
            {
                return false;
            }

            // You might want to add additional validation here
            // For example, checking the token's signature or claims

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating token");
            return false;
        }
    }

    public async Task<string?> GetValidTokenAsync()
    {
        try
        {
            // First check if current token is valid
            if (await IsTokenValidAsync())
            {
                return _currentToken;
            }

            // Try to refresh the token
            var newToken = await RefreshTokenAsync();
            if (!string.IsNullOrEmpty(newToken))
            {
                return newToken;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting valid token");
            return null;
        }
    }

    private async Task<string?> RefreshTokenFromServerAsync(string refreshToken)
    {
        try
        {
            var response = await _httpClientService.PostAsync<RefreshTokenRequest, ApiResponse<TokenResponse>>(
                ApiRoutes.Auth.RefreshToken,
                new RefreshTokenRequest { RefreshToken = refreshToken });

            if (response?.Success == true && response.Data != null)
            {
                _logger.LogInformation("Token refreshed successfully. New token length: {Length}", response.Data.Token.Length);
                // Update the in-memory token and expiration in ApiConfig
                ApiConfig.CurrentToken = response.Data.Token;
                ApiConfig.TokenExpiration = response.Data.Expiration;
                // Also save the new refresh token if provided
                if (!string.IsNullOrEmpty(response.Data.RefreshToken))
                {
                    await _secureStorageService.SaveTokenAsync(response.Data.Token, response.Data.Expiration, response.Data.RefreshToken, response.Data.RefreshTokenExpiration);
                }
                else
                {
                    await _secureStorageService.SaveTokenAsync(response.Data.Token, response.Data.Expiration);
                }
                
                // Set the new token in HttpClientService
                await _httpClientService.SetAuthenticationTokenAsync(response.Data.Token);

                return response.Data.Token;
            }
            else
            {
                _logger.LogWarning("Failed to refresh token from server. API response: {Message}", response?.Message ?? "No response message");
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling RefreshTokenFromServerAsync");
            return null;
        }
    }

    private async void OnTokenRefreshNeeded(object? sender, TokenRefreshEventArgs e)
    {
        _logger.LogInformation("Token refresh needed event received.");

        try
        {
            // Use a semaphore to ensure only one refresh happens at a time
            if (!await _refreshSemaphore.WaitAsync(0))
            {
                _logger.LogInformation("Token refresh already in progress, skipping duplicate request.");
                return;
            }

            try
            {
                var (refreshToken, _) = await _secureStorageService.GetRefreshTokenAsync();
                var (token, _) = await _secureStorageService.GetTokenAsync();

                if (!string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(refreshToken))
                {
                    _logger.LogInformation("Attempting automatic token refresh...");
                    var result = await RefreshTokenAsync(token, refreshToken);
                    if (result != null)
                    {
                        _logger.LogInformation("Automatic token refresh successful.");
                    }
                    else
                    {
                        _logger.LogWarning("Automatic token refresh failed.");
                        // Optional: If refresh fails, we might want to force logout
                        // await LogoutAsync();
                    }
                }
                else
                {
                    _logger.LogWarning("Cannot refresh token: Token or RefreshToken is missing.");
                }
            }
            finally
            {
                _refreshSemaphore.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during automatic token refresh");
        }
    }
}