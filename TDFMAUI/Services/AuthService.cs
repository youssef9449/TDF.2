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
    private readonly IApiService _apiService;
    private readonly ISecureStorage _secureStorage;
    private readonly IUserSessionService _userSessionService;
    private string? _currentToken;
    private DateTime _tokenExpiration = DateTime.MinValue;

    public AuthService(
        SecureStorageService secureStorageService,
        IUserProfileService userProfileService,
        IHttpClientService httpClientService,
        ISecurityService securityService,
        ILogger<AuthService> logger,
        IRoleService roleService,
        IApiService apiService,
        ISecureStorage secureStorage,
        IUserSessionService userSessionService)
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

            logger?.LogInformation("Checking ApiService...");
            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            logger?.LogInformation("ApiService resolved successfully.");

            logger?.LogInformation("Checking SecureStorage...");
            _secureStorage = secureStorage ?? throw new ArgumentNullException(nameof(secureStorage));
            logger?.LogInformation("SecureStorage resolved successfully.");

            logger?.LogInformation("Checking UserSessionService...");
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));
            logger?.LogInformation("UserSessionService resolved successfully.");

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
            // Use shared service to post the login request and get raw response
            var apiResponseContent = await _httpClientService.PostAsync<LoginRequestDto, string>(endpoint, loginRequest);
            _logger.LogDebug("Login API response: {Content}", apiResponseContent);

            // Log the raw response for debugging
            _logger.LogInformation("Raw login response: {Response}", apiResponseContent);
            
            // Try multiple JSON options to handle potential serialization issues
            try
            {
                // First try with DefaultOptions which has more complete configuration
                var options = TDFShared.Helpers.JsonSerializationHelper.DefaultOptions;
                
                // Try to deserialize as ApiResponse<TokenResponse> first (standard format)
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<TokenResponse>>(apiResponseContent, options);

                if (apiResponse?.Data != null && !string.IsNullOrEmpty(apiResponse.Data.Token))
                {
                    _logger.LogInformation("Login successful for user {Username} using ApiResponse<TokenResponse> format", username);

                    // Create UserDetailsDto from TokenResponse data
                    var userDetails = new UserDetailsDto
                    {
                        UserId = apiResponse.Data.UserId,
                        FullName = apiResponse.Data.FullName ?? string.Empty,
                        UserName = apiResponse.Data.Username ?? string.Empty,
                        Department = apiResponse.Data.User?.Department,
                        IsAdmin = apiResponse.Data.IsAdmin,
                        IsManager = apiResponse.Data.IsManager,
                        IsHR = apiResponse.Data.IsHR,
                        Roles = new()
                    };

                    // Add roles based on bit fields
                    if (apiResponse.Data.IsAdmin) userDetails.Roles.Add("Admin");
                    if (apiResponse.Data.IsManager) userDetails.Roles.Add("Manager");
                    if (apiResponse.Data.IsHR) userDetails.Roles.Add("HR");

                    // Add User role by default if no other roles are present
                    if (userDetails.Roles.Count == 0)
                    {
                        userDetails.Roles.Add("User");
                    }

                    // Store the token and user details
                    await _secureStorageService.SaveTokenAsync(apiResponse.Data.Token, apiResponse.Data.Expiration);
                    _userProfileService.SetUserDetails(userDetails);
                    await _httpClientService.SetAuthenticationTokenAsync(apiResponse.Data.Token);

                    // Create and set current user via UserSessionService
                    var currentUser = new TDFShared.DTOs.Users.UserDto
                    {
                        UserID = userDetails.UserId,
                        UserName = userDetails.UserName ?? string.Empty,
                        FullName = userDetails.FullName ?? string.Empty,
                        Department = userDetails.Department,
                        Roles = userDetails.Roles
                    };
                    _userSessionService.SetCurrentUser(currentUser);
                    _userSessionService.SetTokens(apiResponse.Data.Token, apiResponse.Data.Expiration);

                    _logger.LogInformation("User {Username} logged in with roles: {Roles}",
                        username, string.Join(", ", userDetails.Roles));

                    return apiResponse.Data;
                }

                // Check for specific error messages in the API response
                if (!apiResponse?.Success ?? true)
                {
                    var errorMessage = !string.IsNullOrEmpty(apiResponse?.ErrorMessage) 
                        ? apiResponse.ErrorMessage 
                        : apiResponse?.Message ?? "Invalid username or password";
                    _logger.LogWarning("Login failed with API error: {Error}", errorMessage);
                    throw new InvalidOperationException(errorMessage);
                }

                _logger.LogWarning("ApiResponse<TokenResponse> format succeeded but data or token was null/empty. Raw response: {Raw}", apiResponseContent);
            }
            catch (JsonException jsonEx)
            {
                _logger.LogWarning(jsonEx, "Failed to deserialize as ApiResponse<TokenResponse>, trying direct LoginResponseDto format. Raw response: {Raw}", apiResponseContent);

                try
                {
                    // Try with different JSON options
                    var fallbackOptions = TDFShared.Helpers.JsonSerializationHelper.BasicOptions;
                    _logger.LogInformation("Trying fallback deserialization with BasicOptions");
                    
                    // Try to deserialize directly as TokenResponse first
                    var directTokenResponse = JsonSerializer.Deserialize<TokenResponse>(apiResponseContent, fallbackOptions);
                    if (directTokenResponse != null && !string.IsNullOrEmpty(directTokenResponse.Token))
                    {
                        _logger.LogInformation("Login successful for user {Username} using direct TokenResponse format", username);
                        
                        // Create UserDetailsDto from TokenResponse data
                        var userDetails = new UserDetailsDto
                        {
                            UserId = directTokenResponse.UserId,
                            FullName = directTokenResponse.FullName ?? string.Empty,
                            UserName = directTokenResponse.Username ?? string.Empty,
                            Department = directTokenResponse.User?.Department,
                            IsAdmin = directTokenResponse.IsAdmin,
                            IsManager = directTokenResponse.IsManager,
                            IsHR = directTokenResponse.IsHR,
                            Roles = new()
                        };
                        
                        // Add roles based on bit fields
                        if (directTokenResponse.IsAdmin) userDetails.Roles.Add("Admin");
                        if (directTokenResponse.IsManager) userDetails.Roles.Add("Manager");
                        if (directTokenResponse.IsHR) userDetails.Roles.Add("HR");
                        
                        // Add User role by default if no other roles are present
                        if (userDetails.Roles.Count == 0)
                        {
                            userDetails.Roles.Add("User");
                        }
                        
                        // Store the token and user details
                        await _secureStorageService.SaveTokenAsync(directTokenResponse.Token, directTokenResponse.Expiration);
                        _userProfileService.SetUserDetails(userDetails);
                        
                        // Create and set current user via UserSessionService
                        var currentUser = new TDFShared.DTOs.Users.UserDto
                        {
                            UserID = userDetails.UserId,
                            UserName = userDetails.UserName ?? string.Empty,
                            FullName = userDetails.FullName ?? string.Empty,
                            Department = userDetails.Department,
                            Roles = userDetails.Roles
                        };
                        _userSessionService.SetCurrentUser(currentUser);
                        _userSessionService.SetTokens(directTokenResponse.Token, directTokenResponse.Expiration);
                        
                        _logger.LogInformation("User {Username} logged in with roles: {Roles}",
                            username, string.Join(", ", userDetails.Roles));
                            
                        return directTokenResponse;
                    }
                    
                    // If direct TokenResponse fails, try LoginResponseDto format
                    _logger.LogInformation("Direct TokenResponse deserialization failed, trying LoginResponseDto format");
                    var loginResponse = JsonSerializer.Deserialize<LoginResponseDto>(apiResponseContent, fallbackOptions);

                    if (loginResponse?.UserDetails != null && !string.IsNullOrEmpty(loginResponse.Token))
                    {
                        _logger.LogInformation("Login successful for user {Username} using LoginResponseDto format", username);

                        // Ensure all properties are properly initialized
                        loginResponse.UserDetails.FullName ??= string.Empty;
                        loginResponse.UserDetails.UserName ??= string.Empty;
                        loginResponse.UserDetails.Roles ??= new();

                        // Add User role by default if no roles are present
                        if (loginResponse.UserDetails.Roles.Count == 0)
                        {
                            loginResponse.UserDetails.Roles.Add("User");
                        }

                        DateTime expiration = DateTime.UtcNow.AddHours(24); // Default expiration
                        await _secureStorageService.SaveTokenAsync(loginResponse.Token, expiration);
                        _userProfileService.SetUserDetails(loginResponse.UserDetails);

                        // Create and set current user via UserSessionService
                        var currentUser = new TDFShared.DTOs.Users.UserDto
                        {
                            UserID = loginResponse.UserDetails.UserId,
                            UserName = loginResponse.UserDetails.UserName ?? string.Empty,
                            FullName = loginResponse.UserDetails.FullName ?? string.Empty,
                            Department = loginResponse.UserDetails.Department,
                            Roles = loginResponse.UserDetails.Roles
                        };
                        _userSessionService.SetCurrentUser(currentUser);
                        _userSessionService.SetTokens(loginResponse.Token, DateTime.UtcNow.AddHours(24));

                        // Create and return TokenResponse
                        return new TokenResponse
                        {
                            Token = loginResponse.Token,
                            Expiration = DateTime.UtcNow.AddHours(24), // Default expiration
                            UserId = loginResponse.UserDetails.UserId,
                            Username = loginResponse.UserDetails.UserName,
                            FullName = loginResponse.UserDetails.FullName,
                            IsAdmin = loginResponse.UserDetails.IsAdmin,
                            IsManager = loginResponse.UserDetails.IsManager,
                            IsHR = loginResponse.UserDetails.IsHR,
                            User = new UserDto
                            {
                                UserID = loginResponse.UserDetails.UserId,
                                UserName = loginResponse.UserDetails.UserName,
                                FullName = loginResponse.UserDetails.FullName,
                                Department = loginResponse.UserDetails.Department
                            }
                        };
                    }
                    
                    // Last resort - try to parse the JSON manually to extract the token
                    _logger.LogInformation("Standard deserialization failed, attempting manual JSON parsing");
                    try {
                        using var document = JsonDocument.Parse(apiResponseContent);
                        var root = document.RootElement;
                        
                        // Try to find token in various locations in the JSON structure
                        string? token = null;
                        
                        // Check if token is directly in the root
                        if (root.TryGetProperty("token", out var tokenElement))
                        {
                            token = tokenElement.GetString();
                        }
                        // Check if token is in data property
                        else if (root.TryGetProperty("data", out var dataElement) && 
                                dataElement.TryGetProperty("token", out tokenElement))
                        {
                            token = tokenElement.GetString();
                        }
                        
                        if (!string.IsNullOrEmpty(token))
                        {
                            _logger.LogInformation("Successfully extracted token using manual JSON parsing");
                            
                            // Extract other properties if possible
                            int userId = 0;
                            string userName = username;
                            string fullName = username;
                            bool isAdmin = false;
                            bool isManager = false;
                            bool isHR = false;
                            
                            // Try to extract userId
                            if (root.TryGetProperty("userId", out var userIdElement))
                            {
                                userId = userIdElement.TryGetInt32(out int id) ? id : 0;
                            }
                            // Check if userId is in data property
                            else if (root.TryGetProperty("data", out var dataElement2) && 
                                    dataElement2.TryGetProperty("userId", out userIdElement))
                            {
                                userId = userIdElement.TryGetInt32(out int id) ? id : 0;
                            }
                            
                            // Create a minimal TokenResponse
                            var manualTokenResponse = new TokenResponse
                            {
                                Token = token,
                                Expiration = DateTime.UtcNow.AddHours(24), // Default expiration
                                UserId = userId,
                                Username = userName,
                                FullName = fullName,
                                IsAdmin = isAdmin,
                                IsManager = isManager,
                                IsHR = isHR
                            };
                            
                            // Create UserDetailsDto
                            var userDetails = new UserDetailsDto
                            {
                                UserId = userId,
                                UserName = userName,
                                FullName = fullName,
                                IsAdmin = isAdmin,
                                IsManager = isManager,
                                IsHR = isHR,
                                Roles = new() { "User" } // Default role
                            };
                            
                            // Store the token and user details
                            await _secureStorageService.SaveTokenAsync(token, manualTokenResponse.Expiration);
                            _userProfileService.SetUserDetails(userDetails);
                            
                            return manualTokenResponse;
                        }
                    }
                    catch (Exception jsonDocEx)
                    {
                        _logger.LogError(jsonDocEx, "Manual JSON parsing failed");
                    }
                }
                catch (JsonException innerEx)
                {
                    _logger.LogError(innerEx, "Failed to deserialize login response in all fallback formats. Raw response: {Raw}", apiResponseContent);
                    throw new InvalidOperationException($"Login failed: Unexpected response format from server. Please contact support. Raw response: {apiResponseContent}");
                }
            }

            // If we get here, login failed
            _logger.LogWarning("Login failed for user {Username}: Invalid credentials or unexpected response. Raw response: {Raw}", username, apiResponseContent);
            
            // Try to extract a meaningful error message from the response
            try
            {
                using var document = JsonDocument.Parse(apiResponseContent);
                var root = document.RootElement;
                
                string errorMessage = "Login failed: Invalid credentials or unexpected response from server";
                
                // Check for error message in various locations
                if (root.TryGetProperty("message", out var messageElement) && messageElement.ValueKind == JsonValueKind.String)
                {
                    errorMessage = messageElement.GetString() ?? errorMessage;
                }
                else if (root.TryGetProperty("errorMessage", out var errorMsgElement) && errorMsgElement.ValueKind == JsonValueKind.String)
                {
                    errorMessage = errorMsgElement.GetString() ?? errorMessage;
                }
                else if (root.TryGetProperty("error", out var errorElement) && errorElement.ValueKind == JsonValueKind.String)
                {
                    errorMessage = errorElement.GetString() ?? errorMessage;
                }
                
                throw new InvalidOperationException(errorMessage);
            }
            catch (Exception)
            {
                // If JSON parsing fails, use a generic error message
                throw new InvalidOperationException("Login failed: Invalid credentials or unexpected response from server");
            }
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
            // Try to update user status to Offline before clearing credentials
            var currentUser = _userSessionService.CurrentUser;
            if (currentUser != null && currentUser.UserID > 0)
            {
                try
                {
                    // Get the UserPresenceService from DI
                    var serviceProvider = IPlatformApplication.Current?.Services;
                    if (serviceProvider != null)
                    {
                        var userPresenceService = serviceProvider.GetService<IUserPresenceService>();
                        if (userPresenceService != null)
                        {
                            _logger.LogInformation("Setting user {UserName} (ID: {UserId}) status to Offline before logout", 
                                App.CurrentUser.UserName, App.CurrentUser.UserID);
                            await userPresenceService.UpdateStatusAsync(UserPresenceStatus.Offline, "");
                        }
                    }
                }
                catch (Exception statusEx)
                {
                    _logger.LogError(statusEx, "Failed to update user status to Offline during logout");
                    success = false;
                    // Continue with logout even if status update fails
                }
            }
            else if (currentUser != null && currentUser.UserID <= 0)
            {
                _logger.LogWarning("Invalid user ID ({UserId}), skipping status update during logout", currentUser.UserID);
            }

            // Call the API to logout (which will also update status on server-side)
            try
            {
                var apiService = IPlatformApplication.Current?.Services?.GetService<ApiService>();
                if (apiService != null)
                {
                    await apiService.LogoutAsync();
                }
            }
            catch (Exception apiEx)
            {
                _logger.LogError(apiEx, "Failed to call logout API endpoint");
                success = false;
                // Continue with local logout even if API call fails
            }

            try
            {
                // Clear local user data
                _userProfileService.ClearUserDetails();
                await _secureStorageService.RemoveTokenAsync();
                
                // Clear user session including persistent storage
                await _userSessionService.ClearSessionAsync();
                _logger.LogInformation("User session and persistent storage cleared during logout");

                // Navigate back to the login page after logout
                await Shell.Current.GoToAsync("//LoginPage");
                _logger.LogInformation("Navigated to LoginPage after logout.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to navigate to LoginPage after logout.");
                success = false;
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during logout");
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
            _logger.LogInformation("Getting current user details via ApiService");

            // Use the ApiService to get current user instead of making direct HTTP calls
            var apiResponse = await _apiService.GetCurrentUserAsync();
            if (apiResponse?.Success == true && apiResponse.Data != null)
            {
                // Set current user via UserSessionService
                _userSessionService.SetCurrentUser(apiResponse.Data);
                _logger.LogInformation("Successfully retrieved current user details and set via UserSessionService: {UserName}", apiResponse.Data.FullName);
                return apiResponse.Data;
            }
            else
            {
                _logger.LogWarning("Failed to get current user from ApiService: {Message}", apiResponse?.Message);
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
            var refreshRequest = new { Token = token, RefreshToken = refreshToken };
            var endpoint = "auth/refresh-token";

            var apiResponseContent = await _httpClientService.PostAsync<object, string>(endpoint, refreshRequest);
            _logger.LogInformation("Raw refresh token API response: {Content}", apiResponseContent);
            var tokenResponse = JsonSerializer.Deserialize<ApiResponse<TokenResponse>>(apiResponseContent, _serializerOptions);

            if (tokenResponse?.Data != null)
            {
                await _secureStorageService.SaveTokenAsync(tokenResponse.Data.Token, tokenResponse.Data.Expiration);

                // Update ApiConfig for in-memory token on desktop
                if (DeviceHelper.IsDesktop)
                {
                    ApiConfig.CurrentToken = tokenResponse.Data.Token;
                    ApiConfig.TokenExpiration = tokenResponse.Data.Expiration;
                }

                _logger.LogInformation("Token refreshed successfully");
                return tokenResponse.Data;
            }

            _logger.LogWarning("Token refresh response did not contain valid data");
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
            // This is a placeholder - implement according to your authentication system
            var newToken = await RefreshTokenFromServerAsync(refreshToken);
            if (!string.IsNullOrEmpty(newToken))
            {
                // Store the new token
                await _secureStorageService.SaveTokenAsync(newToken, DateTime.UtcNow.AddHours(1));
                _currentToken = newToken;
                _tokenExpiration = DateTime.UtcNow.AddHours(1); // Adjust based on your token lifetime
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
}