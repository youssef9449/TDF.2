using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using TDFAPI.Repositories;
using TDFShared.DTOs.Auth;
using TDFShared.DTOs.Users;
using TDFAPI.Extensions; // Only one set of usings
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using TDFAPI.Domain.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TDFShared.DTOs.Common;
using TDFShared.Enums;
using TDFShared.Services;
using Microsoft.AspNetCore.Http;

namespace TDFAPI.Services;

public class AuthService : TDFShared.Services.IAuthService, IDisposable
{
    private readonly ILogger<AuthService> _logger;
    private readonly IUserRepository _userRepository;
    private readonly IRevokedTokenRepository _revokedTokenRepository;
    private readonly IConfiguration _configuration;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ISecurityService _securityService;
    private readonly IRoleService _roleService;
    private readonly string _jwtSecretKey;
    private readonly int _jwtTokenExpirationMinutes;
    private readonly int _refreshTokenExpirationDays;
    private bool _disposed = false;

    // Account lockout parameters
    private readonly int _maxFailedAttempts;
    private readonly TimeSpan _lockoutDuration;

    private string? _currentToken;

    public AuthService(
        IUserRepository userRepository,
        IRevokedTokenRepository revokedTokenRepository,
        IConfiguration configuration,
        IHttpContextAccessor httpContextAccessor,
        ISecurityService securityService,
        ILogger<AuthService> logger,
        IRoleService roleService)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _revokedTokenRepository = revokedTokenRepository ?? throw new ArgumentNullException(nameof(revokedTokenRepository));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _securityService = securityService ?? throw new ArgumentNullException(nameof(securityService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _roleService = roleService ?? throw new ArgumentNullException(nameof(roleService));

        // Get JWT settings from configuration
        _jwtSecretKey = _configuration["Jwt:SecretKey"] ??
            throw new InvalidOperationException("JWT Secret Key is not configured");

        // Get token expiration settings with defaults
        if (!int.TryParse(_configuration["Jwt:TokenExpirationMinutes"], out _jwtTokenExpirationMinutes))
        {
            _jwtTokenExpirationMinutes = 60; // Default to 60 minutes
        }

        if (!int.TryParse(_configuration["Jwt:RefreshTokenExpirationDays"], out _refreshTokenExpirationDays))
        {
            _refreshTokenExpirationDays = 7; // Default to 7 days
        }

        if (string.IsNullOrEmpty(_jwtSecretKey) || _jwtSecretKey.Length < 32)
        {
            throw new InvalidOperationException("JWT Secret Key must be at least 32 characters long");
        }

        // Account lockout parameters
        if (!int.TryParse(_configuration["AccountLockout:MaxFailedAttempts"], out _maxFailedAttempts))
        {
            _maxFailedAttempts = 5; // Default to 5 failed attempts
        }

        if (!TimeSpan.TryParse(_configuration["AccountLockout:LockoutDuration"], out _lockoutDuration))
        {
            _lockoutDuration = TimeSpan.FromMinutes(30); // Default to 30 minutes
        }
    }

    public async Task<TokenResponse?> LoginAsync(string username, string password)
    {
        _logger.LogInformation("Login attempt for user {Username}", username);

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning("Login failed: Username or password not provided");
            return null;
        }

        try
        {
            // Get user by username
            var user = await _userRepository.GetByUsernameAsync(username);
            if (user == null)
            {
                _logger.LogWarning("Login failed: User {Username} not found", username);
                return null;
            }

            // Get user auth data
            var userAuth = await _userRepository.GetUserAuthDataAsync(user.UserID);
            if (userAuth == null || string.IsNullOrEmpty(userAuth.PasswordHash) || string.IsNullOrEmpty(userAuth.PasswordSalt))
            {
                _logger.LogWarning("Login failed: Invalid auth data for user {UserId}", user.UserID);
                return null;
            }

            // Check if account is locked
            if (userAuth.IsLocked && userAuth.LockoutEnd.HasValue && userAuth.LockoutEnd.Value > DateTime.UtcNow)
            {
                _logger.LogWarning("Login failed: Account for user {UserId} is locked until {LockoutEnd}",
                    user.UserID, userAuth.LockoutEnd);
                return null;
            }

            // Verify password using shared SecurityService
            if (!_securityService.VerifyPassword(password, userAuth.PasswordHash, userAuth.PasswordSalt))
            {
                // Update failed login attempts
                var failedAttempts = userAuth.FailedLoginAttempts + 1;
                var isLocked = failedAttempts >= _maxFailedAttempts; // Lock after max failed attempts
                var lockoutEnd = isLocked ? DateTime.UtcNow.Add(_lockoutDuration) : (DateTime?)null; // Lock for lockout duration

                await _userRepository.UpdateLoginAttemptsAsync(
                    user.UserID,
                    failedAttempts,
                    isLocked,
                    lockoutEnd);

                _logger.LogWarning("Login failed: Invalid password for user {UserId} (attempt {Attempts})",
                    user.UserID, failedAttempts);

                return null;
            }

            // Reset failed login attempts on successful login
            if (userAuth.FailedLoginAttempts > 0 || userAuth.IsLocked)
            {
                await _userRepository.UpdateLoginAttemptsAsync(user.UserID, 0, false, null);
            }

            // Generate tokens
            var token = GenerateJwtToken(user);
            var refreshToken = GenerateRefreshToken();
            var refreshTokenExpiry = DateTime.UtcNow.AddDays(_refreshTokenExpirationDays);

            // Save refresh token
            await _userRepository.UpdateRefreshTokenAsync(user.UserID, refreshToken, refreshTokenExpiry);

            _logger.LogInformation("Login successful for user {UserId}", user.UserID);

            return new TokenResponse
            {
                Token = token,
                RefreshToken = refreshToken,
                Expiration = DateTime.UtcNow.AddMinutes(_jwtTokenExpirationMinutes),
                UserId = user.UserID,
                Username = user.UserName,
                FullName = user.FullName ?? string.Empty,
                IsAdmin = user.IsAdmin ?? false,
                IsManager = user.IsManager ?? false,
                IsHR = user.IsHR ?? false,
                RefreshTokenExpiration = refreshTokenExpiry,
                User = user,
                Roles = user.Roles?.ToArray() ?? Array.Empty<string>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for user {Username}", username);
            return null;
        }
    }

    public async Task<bool> LogoutAsync()
    {
        _logger.LogInformation("Attempting to log out user");

        try
        {
            // Get the current user's token from the HTTP context
            var httpContext = _httpContextAccessor.HttpContext;
            var token = httpContext?.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();

            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("Logout failed: No token found in request");
                return false;
            }

            // Get the token ID from the JWT
            var tokenHandler = new JwtSecurityTokenHandler();
            if (!tokenHandler.CanReadToken(token))
            {
                _logger.LogWarning("Logout failed: Invalid token format");
                return false;
            }

            var jwtToken = tokenHandler.ReadJwtToken(token);
            var tokenId = jwtToken.Id;
            var userIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(tokenId) || userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                _logger.LogWarning("Logout failed: Invalid token claims");
                return false;
            }

            // Clear the refresh token by setting it to empty values
            await _userRepository.UpdateRefreshTokenAsync(userId, string.Empty, DateTime.MinValue);

            _logger.LogInformation("Logout successful for user {UserId}", userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            return false;
        }
    }

    public int GetCurrentUserId()
    {
        try
        {
            var userIdClaim = GetCurrentUserClaim(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                _logger.LogWarning("Could not determine current user ID from claims");
                return 0;
            }
            return userId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current user ID");
            return 0;
        }
    }

    public async Task<int> GetCurrentUserIdAsync()
    {
        return await Task.FromResult(GetCurrentUserId());
    }

    public string? GetCurrentUserClaim(string claimType)
    {
        try
        {
            var user = _httpContextAccessor?.HttpContext?.User;
            if (user == null) return null;
            return user.FindFirst(claimType)?.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user claim: {ClaimType}", claimType);
            return null;
        }
    }

    public async Task<IReadOnlyList<string>> GetUserRolesAsync()
    {
        var roles = new List<string>(3); // Pre-allocate with expected capacity

        try
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
            {
                _logger.LogWarning("No authenticated user found when getting roles");
                return roles.AsReadOnly();
            }

            var user = await _userRepository.GetByIdAsync(userId).ConfigureAwait(false);
            if (user == null)
            {
                _logger.LogWarning("User not found for ID: {UserId}", userId);
                return roles.AsReadOnly();
            }

            // Add roles based on user properties
            if (user.IsAdmin ?? false) roles.Add("Admin");
            if (user.IsManager ?? false) roles.Add("Manager");
            if (user.IsHR ?? false) roles.Add("HR");

            _logger.LogDebug("Found {RoleCount} roles for user {UserId}", roles.Count, userId);
            return roles.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user roles");
            return new List<string>();
        }
    }

    public async Task<string?> GetCurrentUserDepartmentAsync()
    {
        try
        {
            // First try to get from claims (useful for API calls with JWT)
            var departmentClaim = GetCurrentUserClaim(ClaimTypes.Locality) ??
                                GetCurrentUserClaim("department") ??
                                GetCurrentUserClaim("dept");

            if (!string.IsNullOrEmpty(departmentClaim))
            {
                return departmentClaim;
            }

            // If not in claims, get from user data in database
            var userId = GetCurrentUserId();
            if (userId <= 0)
            {
                _logger.LogWarning("Cannot get department: Invalid user ID");
                return null;
            }

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("Cannot get department: User with ID {UserId} not found", userId);
                return null;
            }

            if (string.IsNullOrEmpty(user.Department))
            {
                _logger.LogInformation("No department set for user {UserId}", userId);
                return null;
            }

            return user.Department;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current user department");
            return null;
        }
    }

    public async Task<UserDto?> GetCurrentUserAsync()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId <= 0)
            {
                _logger.LogWarning("Cannot get current user: Invalid user ID");
                return null;
            }

            // Get user from repository
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("User with ID {UserId} not found", userId);
                return null;
            }

            // Get user roles
            var roles = (await GetUserRolesAsync()).ToList();

            // Get department
            var department = await GetCurrentUserDepartmentAsync();

            // Map to DTO
            return new UserDto
            {
                UserID = user.UserID,
                UserName = user.UserName,
                Department = department ?? user.Department,
                IsAdmin = user.IsAdmin,
                IsManager = user.IsManager,
                IsHR = user.IsHR,
                Roles = roles,
                LastLoginDate = user.LastLoginDate,
                LastLoginIp = user.LastLoginIp
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current user");
            return null;
        }
    }

    public async Task<TokenResponse?> RefreshTokenAsync(string token, string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(refreshToken))
        {
            _logger.LogWarning("Refresh token failed: Token or refresh token is null or empty");
            return null;
        }

        _logger.LogInformation("Refreshing token");

        try
        {
            // Validate the token (without checking expiration)
            var tokenHandler = new JwtSecurityTokenHandler();
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(_jwtSecretKey)),
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = false // We handle expiration ourselves
            };

            // Get the user ID from the expired token
            var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);
            var jwtToken = (JwtSecurityToken)validatedToken;

            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                _logger.LogWarning("Refresh token failed: Invalid token - missing or invalid user ID");
                return null;
            }

            // Get user and validate refresh token
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("Refresh token failed: User {UserId} not found", userId);
                return null;
            }

            // Get user auth data to validate refresh token
            var userAuth = await _userRepository.GetUserAuthDataAsync(userId);
            if (userAuth == null ||
                string.IsNullOrEmpty(userAuth.RefreshToken) ||
                userAuth.RefreshToken != refreshToken ||
                userAuth.RefreshTokenExpiryTime == null ||
                userAuth.RefreshTokenExpiryTime < DateTime.UtcNow)
            {
                _logger.LogWarning("Refresh token failed: Invalid or expired refresh token for user {UserId}", userId);
                return null;
            }

            // Note: Token revocation is handled by the JWT validation handler
            // We don't need to manually revoke the token here as we're already
            // generating a new refresh token which will invalidate the old one

            // Generate new tokens
            var newToken = GenerateJwtToken(user);
            var newRefreshToken = GenerateRefreshToken();
            var refreshTokenExpiryUtc = DateTime.UtcNow.AddDays(_refreshTokenExpirationDays);

            // Save the new refresh token
            await _userRepository.UpdateRefreshTokenAsync(userId, newRefreshToken, refreshTokenExpiryUtc);

            // Update the user object for the response
            // We'll create a new object with the updated refresh token information
            // using object initializer syntax for properties that exist on UserDto
            var updatedUser = new UserDto
            {
                UserID = user.UserID,
                UserName = user.UserName ?? string.Empty,
                FullName = user.FullName ?? string.Empty,
                IsAdmin = user.IsAdmin,
                IsManager = user.IsManager,
                IsHR = user.IsHR,
                Roles = user.Roles?.ToList() ?? new List<string>() // Ensure we have a valid list
            };

            // Use reflection to set any additional properties that might exist on derived types
            var userType = user.GetType();
            var updatedUserType = updatedUser.GetType();

            // Copy all properties from the original user to the updated user
            foreach (var prop in userType.GetProperties())
            {
                // Skip properties we've already set explicitly
                if (prop.Name == nameof(UserDto.UserID) ||
                    prop.Name == nameof(UserDto.UserName) ||
                    prop.Name == nameof(UserDto.FullName) ||
                    prop.Name == nameof(UserDto.IsAdmin) ||
                    prop.Name == nameof(UserDto.IsManager) ||
                    prop.Name == nameof(UserDto.IsHR) ||
                    prop.Name == nameof(UserDto.Roles))
                {
                    continue;
                }

                // Only copy properties that exist on the target type and are writable
                var targetProp = updatedUserType.GetProperty(prop.Name);
                if (targetProp != null && targetProp.CanWrite)
                {
                    var value = prop.GetValue(user);
                    targetProp.SetValue(updatedUser, value);
                }
            }

            // Set the refresh token information if the properties exist
            var refreshTokenProp = updatedUserType.GetProperty("RefreshToken");
            var refreshTokenExpiryProp = updatedUserType.GetProperty("RefreshTokenExpiryTime");

            if (refreshTokenProp != null && refreshTokenProp.CanWrite)
                refreshTokenProp.SetValue(updatedUser, newRefreshToken);

            if (refreshTokenExpiryProp != null && refreshTokenExpiryProp.CanWrite)
                refreshTokenExpiryProp.SetValue(updatedUser, refreshTokenExpiryUtc);

            user = updatedUser;

            _logger.LogInformation("Token refresh successful for user {UserId}", userId);

            return new TokenResponse
            {
                Token = newToken,
                RefreshToken = newRefreshToken,
                Expiration = DateTime.UtcNow.AddMinutes(_jwtTokenExpirationMinutes),
                UserId = user.UserID,
                Username = user.UserName,
                FullName = user.FullName ?? string.Empty,
                IsAdmin = user.IsAdmin ?? false,
                IsManager = user.IsManager ?? false,
                IsHR = user.IsHR ?? false,
                RefreshTokenExpiration = refreshTokenExpiryUtc,
                User = user,
                Roles = user.Roles?.ToArray() ?? Array.Empty<string>()
            };
        }
        catch (SecurityTokenException stex)
        {
            _logger.LogWarning(stex, "Refresh token failed: Invalid token");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token refresh");
            return null;
        }
    }

    public string GenerateRefreshToken()
    {
        // Use shared SecurityService for refresh token generation
        return _securityService.GenerateSecureToken(32);
    }

    public string GenerateJwtToken(UserDto user)
    {
        ArgumentNullException.ThrowIfNull(user);

        // Use shared SecurityService for JWT token generation
        return _securityService.GenerateJwtToken(
            user,
            _jwtSecretKey,
            _configuration["Jwt:Issuer"] ?? "tdfapi",
            _configuration["Jwt:Audience"] ?? "tdfapp",
            _jwtTokenExpirationMinutes);
    }

    /// <summary>
    /// Hashes a password using the shared SecurityService
    /// </summary>
    public string HashPassword(string password, out string salt)
    {
        return _securityService.HashPassword(password, out salt);
    }

    /// <summary>
    /// Verifies a password using the shared SecurityService
    /// </summary>
    public bool VerifyPassword(string password, string storedHash, string salt)
    {
        return _securityService.VerifyPassword(password, storedHash, salt);
    }

    public async Task RevokeTokenAsync(string jti, DateTime expiryDateUtc, int? userId = null)
    {
        if (string.IsNullOrEmpty(jti))
        {
            throw new ArgumentException("JTI cannot be null or empty", nameof(jti));
        }

        try
        {
            // Check if token is already revoked
            bool isRevoked = await _revokedTokenRepository.IsRevokedAsync(jti);
            if (isRevoked)
            {
                _logger.LogWarning("Token with JTI {Jti} is already revoked", jti);
                return;
            }

            // Add to revoked tokens
            await _revokedTokenRepository.AddAsync(jti, expiryDateUtc, userId);

            _logger.LogInformation("Successfully revoked token with JTI: {Jti} for user {UserId}", jti, userId?.ToString() ?? "unknown");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking token with JTI: {Jti} for user {UserId}", jti, userId?.ToString() ?? "unknown");
            throw new InvalidOperationException("Failed to revoke token. See inner exception for details.", ex);
        }
    }

    public async Task<bool> IsTokenRevokedAsync(string jti)
    {
        if (string.IsNullOrEmpty(jti))
        {
            _logger.LogWarning("Token validation failed: JTI is null or empty");
            return false;
        }

        try
        {
            // Check if token is revoked
            bool isRevoked = await _revokedTokenRepository.IsRevokedAsync(jti);

            if (isRevoked)
            {
                _logger.LogDebug("Token with JTI {Jti} is revoked", jti);
                return true;
            }

            _logger.LogDebug("Token with JTI {Jti} is not revoked", jti);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if token with JTI {Jti} is revoked", jti);
            // On error, assume the token is not revoked to avoid false positives
            // In a high-security environment, you might want to fail closed (return true)
            return false;
        }
    }

    public async Task<string?> GetCurrentTokenAsync()
    {
        return await Task.FromResult(_currentToken);
    }

    public async Task SetAuthenticationTokenAsync(string token)
    {
        _currentToken = token;
        await Task.CompletedTask;
    }

    #region IDisposable Implementation

    /// <summary>
    /// Protected implementation of Dispose pattern.
    /// </summary>
    /// <param name="disposing">True if called from Dispose(), false if called from the finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Dispose managed resources here
                // Note: _httpClient is typically managed by the HttpClientFactory, so we don't dispose it here
                _logger.LogDebug("AuthService disposed");
            }

            // Free unmanaged resources here

            _disposed = true;
        }
    }

    /// <summary>
    /// Public implementation of Dispose pattern.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Finalizer for AuthService.
    /// </summary>
    ~AuthService()
    {
        Dispose(false);
    }

    #endregion
}