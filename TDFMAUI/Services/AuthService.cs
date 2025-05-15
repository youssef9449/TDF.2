using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TDFShared.DTOs.Auth;
using TDFShared.DTOs.Users;
using TDFShared.DTOs.Common;
using TDFShared.Enums;
using TDFMAUI.Config;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;

namespace TDFMAUI.Services;

public class AuthService : IAuthService
{
    private readonly SecureStorageService _secureStorageService;
    private readonly IUserProfileService _userProfileService;
    private readonly HttpClient _httpClient;
    private readonly ILogger<AuthService> _logger;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly string _baseApiUrl;

    public AuthService(
        SecureStorageService secureStorageService,
        IUserProfileService userProfileService,
        HttpClient httpClient,
        ILogger<AuthService> logger)
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

            logger?.LogInformation("Checking HttpClient...");
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            logger?.LogInformation("HttpClient resolved successfully.");

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

    public async Task<UserDetailsDto?> LoginAsync(string username, string password)
    {
        var loginRequest = new { UserName = username, Password = password };
        var uri = $"{_baseApiUrl}/auth/login";
        _logger.LogInformation("Attempting login for user {Username} via {Uri}", username, uri);

        try
        {
            var response = await _httpClient.PostAsJsonAsync(uri, loginRequest, _serializerOptions);

            if (response.IsSuccessStatusCode)
            {
                // Try to deserialize the API response which wraps the actual token data
                var apiResponseContent = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("Login API response: {Content}", apiResponseContent);

                // Create options that properly handle JSON property names
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                try
                {
                    // First try to deserialize as ApiResponse<TokenResponse>
                    var apiResponse = JsonSerializer.Deserialize<ApiResponse<TokenResponse>>(apiResponseContent, options);

                    if (apiResponse?.Data != null && !string.IsNullOrEmpty(apiResponse.Data.Token))
                    {
                        _logger.LogInformation("Login successful for user {Username} using ApiResponse<TokenResponse> format", username);

                        // Create UserDetailsDto from TokenResponse data
                        var userDetails = new UserDetailsDto
                        {
                            UserId = apiResponse.Data.UserId,
                            FullName = apiResponse.Data.FullName ?? string.Empty,  // Ensure non-null value
                            UserName = apiResponse.Data.Username ?? string.Empty,  // Ensure non-null value
                            Roles = new List<string>(),
                            Department = apiResponse.Data.User?.Department // Extract department if available
                        };

                        // Add roles from token response if available
                        if (apiResponse.Data.Roles != null && apiResponse.Data.Roles.Length > 0)
                        {
                            userDetails.Roles.AddRange(apiResponse.Data.Roles);
                        }
                        // If no roles but IsAdmin is true, add admin role
                        else if (apiResponse.Data.IsAdmin && !userDetails.Roles.Contains("Admin"))
                        {
                            userDetails.Roles.Add("Admin");
                        }

                        // Store the token and user details
                        await _secureStorageService.SaveTokenAsync(apiResponse.Data.Token, apiResponse.Data.Expiration);
                        _userProfileService.SetUserDetails(userDetails);

                        return userDetails;
                    }

                    _logger.LogWarning("ApiResponse<TokenResponse> format succeeded but data or token was null/empty");
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogWarning(jsonEx, "Failed to deserialize as ApiResponse<TokenResponse>, trying direct LoginResponseDto format");

                    // Fall back to the direct format if needed
                    try
                    {
                        var loginResponse = JsonSerializer.Deserialize<LoginResponseDto>(apiResponseContent, options);

                        if (loginResponse?.UserDetails != null && !string.IsNullOrEmpty(loginResponse.Token))
                        {
                            _logger.LogInformation("Login successful for user {Username} using direct LoginResponseDto format", username);

                            // Ensure all properties are properly initialized
                            loginResponse.UserDetails.FullName ??= string.Empty;
                            loginResponse.UserDetails.UserName ??= string.Empty;
                            loginResponse.UserDetails.Roles ??= new List<string>();

                            DateTime expiration = DateTime.UtcNow.AddHours(24); // Default expiration
                            await _secureStorageService.SaveTokenAsync(loginResponse.Token, expiration);
                            _userProfileService.SetUserDetails(loginResponse.UserDetails);
                            return loginResponse.UserDetails;
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
            else
            {
                string errorContent = await response.Content.ReadAsStringAsync();
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    _logger.LogWarning("Login failed for user {Username}. Status: {StatusCode}. Reason: {ReasonPhrase}. Content: {Content}",
                        username, response.StatusCode, response.ReasonPhrase, errorContent);
                }
                else
                {
                    _logger.LogError("Login API call failed for user {Username}. Status: {StatusCode}. Reason: {ReasonPhrase}. Content: {Content}",
                        username, response.StatusCode, response.ReasonPhrase, errorContent);
                }
                return null;
            }
        }
        catch (HttpRequestException httpEx)
        {
            _logger.LogError(httpEx, "HTTP error during login for {Username} to {Uri}", username, uri);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during login for {Username}", username);
            return null;
        }
    }

    public async Task LogoutAsync()
    {
        _logger.LogInformation("Logging out user.");

        try
        {
            // Try to update user status to Offline before clearing credentials
            // This needs to be done before clearing user details and token
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
                // Continue with local logout even if API call fails
            }

            // Clear local user data
            _userProfileService.ClearUserDetails();
            await _secureStorageService.RemoveTokenAsync();

            // Navigate back to the login page after logout
            // Using Shell navigation, assuming LoginPage is registered with the route "//LoginPage"
            // The double slash ensures navigation from the root, replacing the current navigation stack.
            await Shell.Current.GoToAsync("//LoginPage");
            _logger.LogInformation("Navigated to LoginPage after logout.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to navigate to LoginPage after logout.");
            // Consider alternative actions if navigation fails (e.g., displaying an error)
        }
    }

    public async Task<int> GetCurrentUserIdAsync()
    {
        try
        {
            var (token, _) = await _secureStorageService.GetTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("No authentication token found.");
                return 0; // Indicate no authenticated user
            }

            var handler = new JwtSecurityTokenHandler();
            if (handler.CanReadToken(token))
            {
                var jwtToken = handler.ReadJwtToken(token);
                var userIdClaim = jwtToken.Claims.FirstOrDefault(claim => claim.Type == ClaimTypes.NameIdentifier || claim.Type == "sub" || claim.Type == "nameid");

                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                     _logger.LogInformation("Successfully retrieved User ID {UserId} from token.", userId);
                    return userId;
                }
                else
                {
                    _logger.LogWarning("Token does not contain a valid User ID claim (NameIdentifier/sub).");
                }
            }
            else
            {
                 _logger.LogWarning("Stored token is not a valid JWT format.");
            }
        }
        catch (Exception ex)
        {
             _logger.LogError(ex, "Error retrieving or parsing token for User ID.");
        }

        return 0; // Indicate failure or no authenticated user
    }

    public async Task<List<string>> GetUserRolesAsync()
    {
        var roles = new List<string>();
        try
        {
            var (token, _) = await _secureStorageService.GetTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("No authentication token found for getting roles.");
                return roles; // Return empty list
            }

            var handler = new JwtSecurityTokenHandler();
            if (handler.CanReadToken(token))
            {
                var jwtToken = handler.ReadJwtToken(token);
                // Standard claim type for roles is ClaimTypes.Role, but could be custom
                roles.AddRange(jwtToken.Claims
                    .Where(claim => claim.Type == ClaimTypes.Role || claim.Type == "role") // Check common role claim types
                    .Select(claim => claim.Value));

                _logger.LogInformation("Retrieved roles from token: {Roles}", string.Join(", ", roles));
            }
            else
            {
                _logger.LogWarning("Stored token is not a valid JWT format for getting roles.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving or parsing token for User Roles.");
        }
        return roles;
    }

    public async Task<string?> GetCurrentUserDepartmentAsync()
    {
        string? department = null;
        try
        {
            var (token, _) = await _secureStorageService.GetTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("No authentication token found for getting department.");
                return null;
            }

            var handler = new JwtSecurityTokenHandler();
            if (handler.CanReadToken(token))
            {
                var jwtToken = handler.ReadJwtToken(token);
                // Look for a 'department' claim (this might be custom)
                department = jwtToken.Claims.FirstOrDefault(claim => claim.Type == "department")?.Value;

                if (!string.IsNullOrEmpty(department))
                {
                     _logger.LogInformation("Retrieved department from token: {Department}", department);
                }
                else
                {
                    _logger.LogWarning("Token does not contain a 'department' claim.");
                }
            }
            else
            {
                _logger.LogWarning("Stored token is not a valid JWT format for getting department.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving or parsing token for User Department.");
        }
        return department;
    }

    public async Task<UserDto> GetCurrentUserAsync()
    {
        try
        {
            var userId = await GetCurrentUserIdAsync();
            if (userId <= 0)
            {
                _logger.LogWarning("GetCurrentUserAsync: No valid user ID found");
                return null;
            }

            var uri = $"{_baseApiUrl}/users/{userId}";
            _logger.LogInformation("Getting user details for user {UserId}", userId);

            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            var token = (await _secureStorageService.GetTokenAsync()).Item1;
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }

            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var user = await response.Content.ReadFromJsonAsync<UserDto>(_serializerOptions);
                _logger.LogInformation("Successfully retrieved user details for {UserId}", userId);
                return user;
            }
            else
            {
                _logger.LogWarning("Failed to retrieve user details for {UserId}. Status: {StatusCode}",
                    userId, response.StatusCode);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving current user details");
            return null;
        }
    }

    // Implement other IAuthService methods if they exist
    // e.g., IsUserAuthenticatedAsync, LoginAsync, LogoutAsync
}