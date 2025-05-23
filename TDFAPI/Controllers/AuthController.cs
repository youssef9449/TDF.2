using Microsoft.AspNetCore.Mvc;
using TDFAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using TDFShared.DTOs.Users;
using TDFShared.DTOs.Auth;
using TDFShared.DTOs.Common;
using TDFAPI.Repositories;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using TDFAPI.Extensions;
using TDFShared.Constants;
using TDFShared.Services;

namespace TDFAPI.Controllers
{
    [Route(ApiRoutes.Auth.Base)]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IUserService _userService;
        private readonly IUserRepository _userRepository;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            IAuthService authService,
            IUserService userService,
            IUserRepository userRepository,
            ILogger<AuthController> logger)
        {
            _authService = authService;
            _userService = userService;
            _userRepository = userRepository;
            _logger = logger;
        }

        [EnableRateLimiting("auth")]
        [HttpPost("login")]
        [Route(ApiRoutes.Auth.Login)]
        public async Task<ActionResult<ApiResponse<TokenResponse>>> Login([FromBody] AuthLoginRequest request)
        {
            try
            {
                // Basic validation
                if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                {
                    return BadRequest(ApiResponse<TokenResponse>.ErrorResponse("Username and password are required"));
                }

                // Get client information for security logging
                var ipAddress = HttpContext.GetRealIpAddress();
                var userAgent = HttpContext.Request.Headers.UserAgent.ToString();

                _logger.LogInformation("Login attempt for user {Username} from IP {IpAddress}", request.Username, ipAddress);

                var tokenResponse = await _authService.LoginAsync(request.Username, request.Password);
                if (tokenResponse == null)
                {
                    // Check if account is locked - we could enhance AuthService to return specific failure reasons
                    var user = await _userService.GetByUsernameAsync(request.Username);
                    var userAuth = user != null ? await _userRepository.GetUserAuthDataAsync(user.UserID) : null;

                    if (userAuth != null && userAuth.IsLocked && userAuth.LockoutEnd.HasValue && userAuth.LockoutEnd.Value > DateTime.UtcNow)
                    {
                        var remainingLockoutTime = Math.Ceiling((userAuth.LockoutEnd.Value - DateTime.UtcNow).TotalMinutes);
                        _logger.LogWarning("Failed login attempt for locked account: {Username} from IP {IpAddress}", request.Username, ipAddress);
                        return Unauthorized(ApiResponse<TokenResponse>.ErrorResponse($"Account is temporarily locked. Please try again in {remainingLockoutTime} minute(s)"));
                    }

                    _logger.LogWarning("Failed login attempt for user {Username} from IP {IpAddress}", request.Username, ipAddress);
                    return Unauthorized(ApiResponse<TokenResponse>.ErrorResponse("Invalid username or password"));
                }

                // Include useful information in the success response
                _logger.LogInformation("User {UserId} ({Username}) successfully logged in from {IpAddress}",
                    tokenResponse.UserId, request.Username, ipAddress);

                // Update user's online status
                await _userService.UpdateUserPresenceAsync(tokenResponse.UserId, true, userAgent);

                return Ok(ApiResponse<TokenResponse>.SuccessResponse(tokenResponse,
                    $"Login successful. Token expires in {(tokenResponse.Expiration - DateTime.UtcNow).TotalMinutes:0} minutes"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for user {Username}", request.Username);
                return StatusCode(500, ApiResponse<TokenResponse>.ErrorResponse("An error occurred during login"));
            }
        }

        [EnableRateLimiting("auth")]
        [HttpPost("refresh-token")]
        [Route(ApiRoutes.Auth.RefreshToken)]
        public async Task<ActionResult<ApiResponse<TokenResponse>>> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.RefreshToken))
                {
                    return BadRequest(ApiResponse<TokenResponse>.ErrorResponse("Token and refresh token are required"));
                }

                // Get client information for security logging
                var ipAddress = HttpContext.GetRealIpAddress();

                var tokenResponse = await _authService.RefreshTokenAsync(request.Token, request.RefreshToken);
                if (tokenResponse == null)
                {
                    _logger.LogWarning("Invalid refresh token attempt from IP {IpAddress}", ipAddress);
                    return Unauthorized(ApiResponse<TokenResponse>.ErrorResponse("Invalid or expired tokens"));
                }

                _logger.LogInformation("User {UserId} ({Username}) refreshed their token from {IpAddress}",
                    tokenResponse.UserId, tokenResponse.Username, ipAddress);

                return Ok(ApiResponse<TokenResponse>.SuccessResponse(tokenResponse,
                    $"Token refreshed successfully. New token expires in {(tokenResponse.Expiration - DateTime.UtcNow).TotalMinutes:0} minutes"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during token refresh");
                return StatusCode(500, ApiResponse<TokenResponse>.ErrorResponse("An error occurred while refreshing the token"));
            }
        }

        [EnableRateLimiting("auth")]
        [HttpPost("register")]
        [Route(ApiRoutes.Auth.Register)]
        public async Task<ActionResult<ApiResponse<UserDto>>> Register([FromBody] CreateUserRequest request)
        {
            try
            {
                // Validate request
                if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                {
                    return BadRequest(ApiResponse<UserDto>.ErrorResponse("Username and password are required"));
                }

                // Validate username format (letters, numbers, underscore, no spaces)
                if (!System.Text.RegularExpressions.Regex.IsMatch(request.Username, @"^[a-zA-Z0-9_]+$"))
                {
                    return BadRequest(ApiResponse<UserDto>.ErrorResponse("Username can only contain letters, numbers, and underscores"));
                }

                // Check if username already exists
                var existingUser = await _userService.GetByUsernameAsync(request.Username);
                if (existingUser != null)
                {
                    return BadRequest(ApiResponse<UserDto>.ErrorResponse("Username already exists"));
                }

                // Sanitize inputs
                request.FullName = string.IsNullOrWhiteSpace(request.FullName) ? string.Empty : request.FullName.Trim();
                request.Department = string.IsNullOrWhiteSpace(request.Department) ? string.Empty : request.Department.Trim();
                request.Title = string.IsNullOrWhiteSpace(request.Title) ? string.Empty : request.Title.Trim();

                // Prevent self-assignment of admin privileges for regular registrations
                request.IsAdmin = false; // Force to false for public registrations
                request.IsManager = false; // Also force IsManager to false
                request.IsHR = false;

                // Set account status flag for future verification system
                // For now, we'll just create the user without verification

                var userId = await _userService.CreateAsync(request);
                var newUser = await _userService.GetUserDtoByIdAsync(userId);

                _logger.LogInformation("New user registered: {Username}", request.Username);
                if (newUser == null)
                {
                    return StatusCode(500, ApiResponse<UserDto>.ErrorResponse("Failed to retrieve user after registration"));
                }
                return Ok(ApiResponse<UserDto>.SuccessResponse(newUser, "User registered successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user registration for {Username}", request.Username);
                return StatusCode(500, ApiResponse<UserDto>.ErrorResponse($"An error occurred: {ex.Message}"));
            }
        }

        [Authorize]
        [HttpPost("logout")]
        [Route(ApiRoutes.Auth.Logout)]
        public async Task<ActionResult<ApiResponse<object>>> Logout()
        {
            try
            {
                // Get client information for security logging
                var ipAddress = HttpContext.GetRealIpAddress();

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var username = User.Identity?.Name;

                if (!string.IsNullOrEmpty(userId) && int.TryParse(userId, out var id))
                {
                    // Invalidate the refresh token by setting it to null
                    await _userService.InvalidateRefreshTokenAsync(id);

                    // Revoke the current access token
                    var jti = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
                    var expiryClaim = User.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;
                    if (!string.IsNullOrEmpty(jti) && long.TryParse(expiryClaim, out var expiryUnixTime))
                    {
                        var expiryDateTime = DateTimeOffset.FromUnixTimeSeconds(expiryUnixTime).UtcDateTime;
                        await _authService.RevokeTokenAsync(jti, expiryDateTime, id);
                        _logger.LogInformation("Revoked access token {Jti} for user {UserId}", jti, id);
                    }
                    else
                    {
                        _logger.LogWarning("Could not revoke access token for user {UserId}: JTI or Expiry claim missing/invalid.", id);
                    }

                    // Update user's offline status
                    await _userService.UpdateUserPresenceAsync(id, false);

                    _logger.LogInformation("User {UserId} ({Username}) logged out from {IpAddress}", id, username, ipAddress);
                }
                else
                {
                    _logger.LogWarning("Logout attempted with invalid user identity from {IpAddress}", ipAddress);
                }

                return Ok(ApiResponse<object>.SuccessResponse(null, "Logout successful"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                return StatusCode(500, ApiResponse<object>.ErrorResponse("An error occurred during logout"));
            }
        }
    }
}