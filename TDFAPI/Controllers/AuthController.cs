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
using System.ComponentModel.DataAnnotations;

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
        public async Task<ActionResult<ApiResponse<TokenResponse>>> Login([FromBody] LoginRequestDto request)
        {
            try
            {
                // Model validation
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage);
                    return BadRequest(ApiResponse<TokenResponse>.ErrorResponse(string.Join(", ", errors)));
                }

                // Get client information for security logging
                var ipAddress = HttpContext.GetRealIpAddress();
                var userAgent = HttpContext.Request.Headers.UserAgent.ToString();

                _logger.LogInformation("Login attempt for user {Username} from IP {IpAddress}", request.Username, ipAddress);

                // Check if account is locked before attempting login
                var user = await _userService.GetByUsernameAsync(request.Username);
                var userAuth = user != null ? await _userRepository.GetUserAuthDataAsync(user.UserID) : null;

                if (userAuth?.IsLocked == true && userAuth.LockoutEnd.HasValue && userAuth.LockoutEnd.Value > DateTime.UtcNow)
                {
                    var remainingLockoutTime = Math.Ceiling((userAuth.LockoutEnd.Value - DateTime.UtcNow).TotalMinutes);
                    _logger.LogWarning("Failed login attempt for locked account: {Username} from IP {IpAddress}", request.Username, ipAddress);
                    return Unauthorized(ApiResponse<TokenResponse>.ErrorResponse($"Account is temporarily locked. Please try again in {remainingLockoutTime} minute(s)"));
                }

                var tokenResponse = await _authService.LoginAsync(request.Username, request.Password);
                if (tokenResponse == null)
                {
                    _logger.LogWarning("Failed login attempt for user {Username} from IP {IpAddress}", request.Username, ipAddress);
                    return Unauthorized(ApiResponse<TokenResponse>.ErrorResponse("Invalid username or password"));
                }

                // Include useful information in the success response
                _logger.LogInformation("User {UserId} ({Username}) successfully logged in from {IpAddress}",
                    tokenResponse.UserId, request.Username, ipAddress);

                // Update user's online status and device info
                await _userService.UpdateUserPresenceAsync(tokenResponse.UserId, true, userAgent);
                if (!string.IsNullOrEmpty(request.DeviceId))
                {
                    await _userService.UpdateUserDeviceInfoAsync(tokenResponse.UserId, request.DeviceId, userAgent);
                }

                return Ok(ApiResponse<TokenResponse>.SuccessResponse(tokenResponse,
                    $"Login successful. Token expires in {(tokenResponse.Expiration - DateTime.UtcNow).TotalMinutes:0} minutes"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for user {Username}", request.Username);
                return StatusCode(500, ApiResponse<TokenResponse>.ErrorResponse("An error occurred during login. Please try again later."));
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
        public async Task<ActionResult<ApiResponse<RegisterResponseDto>>> Register([FromBody] RegisterRequestDto request)
        {
            try
            {
                // Model validation is handled by ASP.NET Core using the data annotations
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage);
                    return BadRequest(ApiResponse<RegisterResponseDto>.ErrorResponse(string.Join(", ", errors)));
                }

                // Check if username already exists
                var existingUser = await _userService.GetByUsernameAsync(request.Username);
                if (existingUser != null)
                {
                    return BadRequest(ApiResponse<RegisterResponseDto>.ErrorResponse("Username already exists"));
                }
                
                // Check if full name is already taken
                try
                {
                    var isFullNameTaken = await _userService.IsFullNameTakenAsync(request.FullName);
                    if (isFullNameTaken)
                    {
                        return BadRequest(ApiResponse<RegisterResponseDto>.ErrorResponse($"Full name '{request.FullName}' is already taken"));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error checking for duplicate full name: {FullName}", request.FullName);
                    // Continue with registration attempt even if this check fails
                }

                // Prevent self-assignment of admin privileges for regular registrations
                request.IsAdmin = false;
                request.IsManager = false;
                request.IsHR = false;

                // Create user using the service
                var userId = await _userService.CreateAsync(new CreateUserRequest
                {
                    Username = request.Username,
                    Password = request.Password,
                    FullName = request.FullName,
                    Department = request.Department,
                    Title = request.Title,
                    IsAdmin = request.IsAdmin,
                    IsManager = request.IsManager,
                    IsHR = request.IsHR
                });

                var newUser = await _userService.GetUserDtoByIdAsync(userId);

                _logger.LogInformation("New user registered: {Username}", request.Username);

                if (newUser == null)
                {
                    return StatusCode(500, ApiResponse<RegisterResponseDto>.ErrorResponse("Failed to retrieve user after registration"));
                }

                var response = new RegisterResponseDto
                {
                    Success = true,
                    Message = "User registered successfully",
                    UserDetails = new UserDetailsDto
                    {
                        UserId = newUser.UserID,
                        UserName = newUser.UserName,
                        FullName = newUser.FullName,
                        Department = newUser.Department,
                        IsAdmin = newUser.IsAdmin,
                        IsManager = newUser.IsManager,
                        IsHR = newUser.IsHR,
                        Roles = new List<string>()
                    }
                };

                return Ok(ApiResponse<RegisterResponseDto>.SuccessResponse(response, "User registered successfully"));
            }
            catch (ValidationException ex)
            {
                _logger.LogWarning(ex, "Validation error during user registration for {Username}", request.Username);
                return BadRequest(ApiResponse<RegisterResponseDto>.ErrorResponse(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Operation error during user registration for {Username}", request.Username);
                // Try to provide a user-friendly message for duplicate username
                if (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(ApiResponse<RegisterResponseDto>.ErrorResponse(ex.Message));
                }
                return BadRequest(ApiResponse<RegisterResponseDto>.ErrorResponse("Registration failed: " + ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user registration for {Username}", request.Username);
                return StatusCode(500, ApiResponse<RegisterResponseDto>.ErrorResponse($"An error occurred: {ex.Message}"));
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

                return Ok(ApiResponse<object>.SuccessResponse(new { }, "Logout successful"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                return StatusCode(500, ApiResponse<object>.ErrorResponse("An error occurred during logout"));
            }
        }
    }
}