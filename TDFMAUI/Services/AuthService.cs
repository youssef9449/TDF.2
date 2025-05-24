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
using TDFShared.DTOs.Auth;
using TDFShared.DTOs.Common;
using TDFShared.DTOs.Users;
using TDFShared.Enums;
using TDFShared.Services;

namespace TDFMAUI.Services;

public class AuthService : TDFShared.Services.IAuthService
{
    private readonly SecureStorageService _secureStorageService;
    private readonly IUserProfileService _userProfileService;
    private readonly TDFShared.Services.IHttpClientService _httpClientService;
    private readonly ILogger<AuthService> _logger;
    private readonly TDFShared.Services.ISecurityService _securityService;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly string _baseApiUrl;
    private readonly IRoleService _roleService;
    private readonly IApiService _apiService;
    private readonly ISecureStorage _secureStorage;

    public AuthService(
        SecureStorageService secureStorageService,
        IUserProfileService userProfileService,
        TDFShared.Services.IHttpClientService httpClientService,
        TDFShared.Services.ISecurityService securityService,
        ILogger<AuthService> logger,
        IRoleService roleService,
        IApiService apiService,
        ISecureStorage secureStorage)
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

            // Use centralized JSON options for consistency
            var options = TDFShared.Helpers.JsonSerializationHelper.BasicOptions;

            try
            {
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

                        _logger.LogInformation("User {Username} logged in with roles: {Roles}",
                            username, string.Join(", ", userDetails.Roles));

                        return apiResponse.Data;
                    }

                    _logger.LogWarning("ApiResponse<TokenResponse> format succeeded but data or token was null/empty");
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogWarning(jsonEx, "Failed to deserialize as ApiResponse<TokenResponse>, trying direct LoginResponseDto format");

                    try
                    {
                        var loginResponse = JsonSerializer.Deserialize<LoginResponseDto>(apiResponseContent, options);

                        if (loginResponse?.UserDetails != null && !string.IsNullOrEmpty(loginResponse.Token))
                        {
                            _logger.LogInformation("Login successful for user {Username} using direct LoginResponseDto format", username);

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

                            // Create and return TokenResponse
                            return new TokenResponse
                            {
                                Token = loginResponse.Token,
                                Expiration = expiration,
                                UserId = loginResponse.UserDetails.UserId,
                                Username = loginResponse.UserDetails.UserName,
                                FullName = loginResponse.UserDetails.FullName,
                                IsAdmin = loginResponse.UserDetails.IsAdmin,
                                IsManager = loginResponse.UserDetails.IsManager,
                                IsHR = loginResponse.UserDetails.IsHR,
                                User = new UserDto
                                {
                                    UserID = loginResponse.UserDetails.UserId,
                                    Username = loginResponse.UserDetails.UserName,
                                    FullName = loginResponse.UserDetails.FullName,
                                    Department = loginResponse.UserDetails.Department
                                }
                            };
                        }
                    }
                    catch (JsonException innerEx)
                    {
                        _logger.LogError(innerEx, "Failed to deserialize login response in fallback format");
                    }
                }

            _logger.LogError("Login API call successful but response format could not be parsed. Response: {Response}", apiResponseContent);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for {Username}", username);
            return null;
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
            if (App.CurrentUser != null)
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
                            _logger.LogInformation("Setting user status to Offline before logout");
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
            // Check if we should persist token based on platform
            if (DeviceHelper.IsDesktop)
            {
                _logger.LogInformation("GetCurrentUserIdAsync: Desktop platform detected, checking token persistence");
                // For desktop platforms, check if we should clear tokens
                bool shouldPersist = _secureStorageService.ShouldPersistToken();
                if (!shouldPersist)
                {
                    _logger.LogInformation("GetCurrentUserIdAsync: Token persistence disabled for desktop platform");
                    // This will ensure we don't use any stored tokens on desktop platforms
                    await _secureStorageService.HandleTokenPersistenceAsync();
                    return 0;
                }
            }


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

    public async Task<UserDto> GetCurrentUserAsync()
    {
        try
        {
            // Check if we should persist token based on platform
            if (DeviceHelper.IsDesktop)
            {
                _logger.LogInformation("GetCurrentUserAsync: Desktop platform detected, checking token persistence");
                // For desktop platforms, check if we should clear tokens
                bool shouldPersist = _secureStorageService.ShouldPersistToken();
                if (!shouldPersist)
                {
                    _logger.LogInformation("GetCurrentUserAsync: Token persistence disabled for desktop platform");
                    // This will ensure we don't use any stored tokens on desktop platforms
                    await _secureStorageService.HandleTokenPersistenceAsync();
                    return null;
                }
            }

            var userId = await GetCurrentUserIdAsync();
            if (userId <= 0)
            {
                _logger.LogWarning("GetCurrentUserAsync: No valid user ID found");
                return null;
            }

            var endpoint = $"users/{userId}";
            _logger.LogInformation("Getting user details for user {UserId}", userId);

            // Set authentication token if available
            var (token, _) = await _secureStorageService.GetTokenAsync().ConfigureAwait(false);
            if (!string.IsNullOrEmpty(token))
            {
                await _httpClientService.SetAuthenticationTokenAsync(token);
            }

            var user = await _httpClientService.GetAsync<UserDto>(endpoint);
            _logger.LogInformation("Successfully retrieved user details for {UserId}", userId);
            return user;
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

            var tokenResponse = await _httpClientService.PostAsync<object, ApiResponse<TokenResponse>>(endpoint, refreshRequest);

            if (tokenResponse?.Data != null)
            {
                await _secureStorageService.SaveTokenAsync(tokenResponse.Data.Token, tokenResponse.Data.Expiration);
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

    public string GenerateJwtToken(UserDto user)
    {
        _logger.LogInformation("Generating JWT token for user {UserId}", user.UserID);

        // Note: In a real application, this would generate a JWT token on the client side
        // This is typically done on the server side, so this is a simplified version
        // that just returns a mock token for demonstration purposes

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = System.Text.Encoding.ASCII.GetBytes("your-secret-key-here"); // Should be stored securely

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserID.ToString()),
            new Claim(ClaimTypes.Name, user.Username ?? string.Empty)
        };

        // Add roles if available
        if (user.Roles != null)
        {
            foreach (var role in user.Roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }
        }

        var tokenDescriptor = new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddHours(24),
            SigningCredentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(
                new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(key),
                Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
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

    private async Task<LoginResponse> HandleLoginResponse(LoginResponse loginResponse)
    {
        if (loginResponse?.UserDetails == null)
        {
            throw new AuthenticationException("Invalid login response");
        }

        // Assign roles using RoleService
        _roleService.AssignRoles(loginResponse.UserDetails);

        // Store user details
        await _secureStorageService.SaveTokenAsync(loginResponse.Token, loginResponse.Expiration);
        _userProfileService.SetUserDetails(loginResponse.UserDetails);

        return loginResponse;
    }
}