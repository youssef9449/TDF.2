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
using TDFShared.Services;

namespace TDFAPI.Services;

public class AuthService : IAuthService
{
    private readonly ILogger<AuthService> _logger;
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly string _baseApiUrl;

    public AuthService(
        HttpClient httpClient,
        ILogger<AuthService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        _baseApiUrl = "https://localhost:7094/api"; // Replace with your actual API base URL
    }

    public async Task<TokenResponse?> LoginAsync(string username, string password)
    {
        var loginRequest = new { UserName = username, Password = password };
        var uri = $"{_baseApiUrl}/auth/login";
        _logger.LogInformation("Attempting login for user {Username} via {Uri}", username, uri);

        try
        {
            var response = await _httpClient.PostAsJsonAsync(uri, loginRequest, _serializerOptions);

            if (response.IsSuccessStatusCode)
            {
                var apiResponseContent = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("Login API response: {Content}", apiResponseContent);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                try
                {
                    var apiResponse = JsonSerializer.Deserialize<ApiResponse<TokenResponse>>(apiResponseContent, options);

                    if (apiResponse?.Data != null && !string.IsNullOrEmpty(apiResponse.Data.Token))
                    {
                        _logger.LogInformation("Login successful for user {Username} using ApiResponse<TokenResponse> format", username);

                        return apiResponse.Data;
                    }

                    _logger.LogWarning("ApiResponse<TokenResponse> format succeeded but data or token was null/empty");
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogWarning(jsonEx, "Failed to deserialize as ApiResponse<TokenResponse>, trying direct LoginResponseDto format");
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
    }

    public async Task<int> GetCurrentUserIdAsync()
    {
        return 1;
    }

    public async Task<List<string>> GetUserRolesAsync()
    {
        return new List<string> { "Admin" };
    }

    public async Task<string?> GetCurrentUserDepartmentAsync()
    {
        return "IT";
    }

    public async Task<UserDto> GetCurrentUserAsync()
    {
        return new UserDto { UserID = 1, Username = "admin", FullName = "Admin User" };
    }

    public async Task<TokenResponse?> RefreshTokenAsync(string refreshToken, string accessToken)
    {
        // TODO: Implement refresh token logic
        return null;
    }

    public string GenerateJwtToken(UserDto user)
    {
        // TODO: Implement JWT token generation logic
        return "Generated JWT Token";
    }

    public string HashPassword(string password, out string salt)
    {
        // Generate a salt
        salt = TDFShared.Services.Security.GenerateSalt();

        // Hash the password using the salt
        string storedHash = TDFShared.Services.Security.HashPassword(password, salt);
		return storedHash;
    }

    public bool VerifyPassword(string password, string storedHash, string salt)
    {
        // Verify the password using the stored hash and salt
        return TDFShared.Services.Security.VerifyPassword(password, storedHash, salt);
    }

    public async Task RevokeTokenAsync(string token, DateTime expiryDate)
    {
        // TODO: Implement token revocation logic
    }

    public async Task<bool> IsTokenRevokedAsync(string token)
    {
        // TODO: Implement token revocation check logic
        return false;
    }
}