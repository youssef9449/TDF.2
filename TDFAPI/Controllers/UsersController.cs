using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using TDFAPI.Services;
using TDFShared.Constants;
using TDFShared.Services;
using TDFShared.DTOs.Common;
using TDFShared.DTOs.Users;

namespace TDFAPI.Controllers
{
    [Route(ApiRoutes.Users.Base)]
    [ApiController]
    [Authorize]
    [EnableRateLimiting("api")]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ILogger<UsersController> _logger;

        public UsersController(IUserService userService, ILogger<UsersController> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<PaginatedResult<UserDto>>>> GetUsers(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            if (page < 1 || pageSize < 1 || pageSize > 100)
            {
                return BadRequest(ApiResponse<PaginatedResult<UserDto>>.ErrorResponse(
                    "Invalid pagination parameters. Page must be >= 1 and pageSize must be between 1 and 100."));
            }

            var users = await _userService.GetPaginatedAsync(page, pageSize);
            return Ok(ApiResponse<PaginatedResult<UserDto>>.SuccessResponse(users));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<UserDto>>> GetUser(int id)
        {
            if (id <= 0)
            {
                return BadRequest(ApiResponse<UserDto>.ErrorResponse("Invalid user ID"));
            }

            var currentUserId = GetCurrentUserId();
            var isAdmin = User.IsInRole("Admin");

            if (id != currentUserId && !isAdmin)
            {
                _logger.LogWarning("User {CurrentUserId} attempted to access details for user {RequestedId}",
                    currentUserId, id);
                return Forbid();
            }

            var user = await _userService.GetUserDtoByIdAsync(id);
            if (user == null)
            {
                return NotFound(ApiResponse<UserDto>.ErrorResponse("User not found"));
            }

            return Ok(ApiResponse<UserDto>.SuccessResponse(user));
        }

        [HttpGet("department/{department}")]
        [Authorize(Roles = "Admin,HR,Manager")]
        public async Task<ActionResult<ApiResponse<IEnumerable<UserDto>>>> GetUsersByDepartment(string department)
        {
            if (string.IsNullOrWhiteSpace(department))
            {
                return BadRequest(ApiResponse<IEnumerable<UserDto>>.ErrorResponse("Department name cannot be empty."));
            }

            var currentUserId = GetCurrentUserId();
            var currentUserDept = User.FindFirst(ClaimTypes.GroupSid)?.Value;
            var isManager = User.IsInRole("Manager");
            var isAdminOrHR = User.IsInRole("Admin") || User.IsInRole("HR");

            if (isManager && !isAdminOrHR)
            {
                var currentUser = new UserDto { IsManager = true, Department = currentUserDept };
                bool canAccessDepartment = RequestStateManager.CanManageDepartment(currentUser, department);
                if (!canAccessDepartment)
                {
                    _logger.LogWarning(
                        "Manager {UserId} in department {UserDept} attempted to access users for department {TargetDept} but lacks permission",
                        currentUserId, currentUserDept, department);
                    return Forbid();
                }
            }

            _logger.LogInformation("Getting users for department: {Department}", department);
            var users = await _userService.GetUsersByDepartmentAsync(department);

            if (users == null || !users.Any())
            {
                _logger.LogInformation("No users found for department: {Department}", department);
                return Ok(ApiResponse<IEnumerable<UserDto>>.SuccessResponse(new List<UserDto>()));
            }

            return Ok(ApiResponse<IEnumerable<UserDto>>.SuccessResponse(users));
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<bool>>> UpdateUser(int id, [FromBody] UpdateUserRequest userDto)
        {
            if (id <= 0)
            {
                return BadRequest(ApiResponse<bool>.ErrorResponse("Invalid user ID"));
            }
            if (userDto == null)
            {
                return BadRequest(ApiResponse<bool>.ErrorResponse("User data is required"));
            }
            if (string.IsNullOrWhiteSpace(userDto.FullName))
            {
                return BadRequest(ApiResponse<bool>.ErrorResponse("Full name is required"));
            }

            var success = await _userService.UpdateAsync(id, userDto);
            if (!success)
            {
                return NotFound(ApiResponse<bool>.ErrorResponse("User not found or update failed"));
            }

            _logger.LogInformation("User {UserId} updated successfully by admin {AdminId}",
                id, GetCurrentUserId());
            return Ok(ApiResponse<bool>.SuccessResponse(true, "User updated successfully"));
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteUser(int id)
        {
            if (id <= 0)
            {
                return BadRequest(ApiResponse<bool>.ErrorResponse("Invalid user ID"));
            }

            var currentUserId = GetCurrentUserId();
            if (id == currentUserId)
            {
                return BadRequest(ApiResponse<bool>.ErrorResponse("You cannot delete your own account"));
            }

            var success = await _userService.DeleteAsync(id);
            if (!success)
            {
                return NotFound(ApiResponse<bool>.ErrorResponse("User not found or delete failed"));
            }

            _logger.LogInformation("User {UserId} deleted by admin {AdminId}", id, currentUserId);
            return Ok(ApiResponse<bool>.SuccessResponse(true, "User deleted successfully"));
        }

        [HttpPost("change-password")]
        [EnableRateLimiting("auth")]
        public async Task<ActionResult<ApiResponse<bool>>> ChangePassword([FromBody] ChangePasswordDto changePasswordDto)
        {
            if (changePasswordDto == null)
            {
                return BadRequest(ApiResponse<bool>.ErrorResponse("Password data is required"));
            }
            if (string.IsNullOrEmpty(changePasswordDto.NewPassword) || changePasswordDto.NewPassword.Length < 8)
            {
                return BadRequest(ApiResponse<bool>.ErrorResponse("New password must be at least 8 characters long"));
            }

            var currentUserId = GetCurrentUserId();
            var success = await _userService.ChangePasswordAsync(
                currentUserId,
                changePasswordDto.CurrentPassword,
                changePasswordDto.NewPassword);

            if (!success)
            {
                _logger.LogWarning("Failed password change attempt for user {UserId}", currentUserId);
                return BadRequest(ApiResponse<bool>.ErrorResponse(
                    "Password change failed. Current password may be incorrect."));
            }

            _logger.LogInformation("Password changed successfully for user {UserId}", currentUserId);
            return Ok(ApiResponse<bool>.SuccessResponse(true, "Password changed successfully"));
        }

        [HttpPut("profile")]
        public async Task<ActionResult<ApiResponse<bool>>> UpdateMyProfile([FromBody] UpdateMyProfileRequest dto)
        {
            if (dto == null)
            {
                return BadRequest(ApiResponse<bool>.ErrorResponse("User data is required"));
            }

            var currentUserId = GetCurrentUserId();
            if (currentUserId <= 0)
            {
                _logger.LogWarning("UpdateMyProfile attempted with invalid token (missing/invalid user ID)");
                return Unauthorized(ApiResponse<bool>.ErrorResponse("Invalid authentication token"));
            }

            var success = await _userService.UpdateSelfAsync(currentUserId, dto);
            if (!success)
            {
                return NotFound(ApiResponse<bool>.ErrorResponse("User not found or update failed"));
            }

            _logger.LogInformation("User {UserId} updated their profile successfully", currentUserId);
            return Ok(ApiResponse<bool>.SuccessResponse(true, "Profile updated successfully"));
        }

        [HttpPost("profile/image")]
        [RequestSizeLimit(5 * 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = 5 * 1024 * 1024)]
        public async Task<ActionResult<ApiResponse<bool>>> UploadProfilePicture(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(ApiResponse<bool>.ErrorResponse("No file uploaded or file is empty."));
            }

            var allowedContentTypes = new[] { "image/jpeg", "image/png", "image/gif" };
            if (!allowedContentTypes.Contains(file.ContentType.ToLowerInvariant()))
            {
                return BadRequest(ApiResponse<bool>.ErrorResponse("Invalid file type. Only JPG, PNG, GIF allowed."));
            }

            var currentUserId = GetCurrentUserId();
            if (currentUserId <= 0)
            {
                _logger.LogWarning("UploadProfilePicture attempted with invalid token");
                return Unauthorized(ApiResponse<bool>.ErrorResponse("Invalid authentication token"));
            }

            await using var stream = file.OpenReadStream();
            var success = await _userService.UpdateProfilePictureAsync(currentUserId, stream, file.ContentType);
            if (!success)
            {
                return NotFound(ApiResponse<bool>.ErrorResponse("User not found or failed to update picture."));
            }

            _logger.LogInformation(
                "User {UserId} uploaded a new profile picture ({FileName}, {ContentType}, {Size} bytes)",
                currentUserId, file.FileName, file.ContentType, file.Length);
            return Ok(ApiResponse<bool>.SuccessResponse(true, "Profile picture updated successfully."));
        }

        [HttpGet("online")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<IEnumerable<UserDto>>>> GetOnlineUsers()
        {
            var users = await _userService.GetOnlineUsersAsync();
            return Ok(ApiResponse<IEnumerable<UserDto>>.SuccessResponse(users));
        }

        [HttpGet("all")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<PaginatedResult<UserDto>>>> GetAllUsersWithStatus(
            [FromQuery] int page = 1, [FromQuery] int pageSize = 1000)
        {
            var users = await _userService.GetPaginatedAsync(page, pageSize);
            return Ok(ApiResponse<PaginatedResult<UserDto>>.SuccessResponse(users));
        }

        [HttpPut("{id}/connection")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<bool>>> UpdateUserConnection(
            int id, [FromBody] UpdateConnectionRequest request)
        {
            if (id <= 0)
            {
                return BadRequest(ApiResponse<bool>.ErrorResponse("Invalid user ID"));
            }
            if (request == null)
            {
                return BadRequest(ApiResponse<bool>.ErrorResponse("Connection data is required"));
            }

            var currentUserId = GetCurrentUserId();
            var isAdmin = User.IsInRole("Admin");

            if (id != currentUserId && !isAdmin)
            {
                _logger.LogWarning("User {CurrentUserId} attempted to update connection status for user {RequestedId}",
                    currentUserId, id);
                return Forbid();
            }

            var success = await _userService.UpdateUserPresenceAsync(id, request.IsConnected);
            if (!success)
            {
                return NotFound(ApiResponse<bool>.ErrorResponse("User not found or update failed"));
            }

            _logger.LogInformation("User {UserId} connection status updated to {IsConnected} by user {UpdatedBy}",
                id, request.IsConnected, currentUserId);
            return Ok(ApiResponse<bool>.SuccessResponse(true, "Connection status updated successfully"));
        }

        private int GetCurrentUserId()
        {
            var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(value, out var id) ? id : 0;
        }
    }
}
