using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TDFAPI.Services;
using TDFShared.Constants;
using TDFShared.DTOs.Users;
using TDFShared.DTOs.Common;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.RateLimiting;
using System.ComponentModel.DataAnnotations;
using TDFShared.Services;

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
        [Route(ApiRoutes.Users.GetAll)]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<PaginatedResult<UserDto>>>> GetUsers(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            // Validate input
            if (page < 1 || pageSize < 1 || pageSize > 100)
            {
                return BadRequest(ApiResponse<PaginatedResult<UserDto>>.ErrorResponse(
                    "Invalid pagination parameters. Page must be >= 1 and pageSize must be between 1 and 100."));
            }

            try
            {
                var users = await _userService.GetPaginatedAsync(page, pageSize);
                return Ok(ApiResponse<PaginatedResult<UserDto>>.SuccessResponse(users));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving paginated users: {Message}", ex.Message);
                return StatusCode(500, ApiResponse<PaginatedResult<UserDto>>.ErrorResponse("An error occurred retrieving users"));
            }
        }

        [HttpGet("{id}")]
        [Route(ApiRoutes.Users.GetById)]
        public async Task<ActionResult<ApiResponse<UserDto>>> GetUser(int id)
        {
            try
            {
                if (id <= 0)
                {
                    return BadRequest(ApiResponse<UserDto>.ErrorResponse("Invalid user ID"));
                }

                // Check if the user is requesting their own info or is an admin
                var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user {UserId}: {Message}", id, ex.Message);
                return StatusCode(500, ApiResponse<UserDto>.ErrorResponse("An error occurred retrieving user details"));
            }
        }

        [HttpGet("department/{department}")]
        [Route(ApiRoutes.Users.GetByDepartment)]
        [Authorize(Roles = "Admin,HR,Manager")] // Or adjust roles as needed
        public async Task<ActionResult<ApiResponse<IEnumerable<UserDto>>>> GetUsersByDepartment(string department)
        {
            if (string.IsNullOrWhiteSpace(department))
            {
                return BadRequest(ApiResponse<IEnumerable<UserDto>>.ErrorResponse("Department name cannot be empty."));
            }

            // Optional: Check if the requesting user (Manager) is in the requested department
            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var currentUserDept = User.FindFirst(ClaimTypes.GroupSid)?.Value; // Assuming Department is stored in a claim like GroupSid
            var isManager = User.IsInRole("Manager");
            var isAdminOrHR = User.IsInRole("Admin") || User.IsInRole("HR");

            // Allow Admins/HR to see any department. Managers can access their own department (including constituent departments for hyphenated departments).
            if (isManager && !isAdminOrHR)
            {
                var currentUser = new UserDto { IsManager = true, Department = currentUserDept };
                bool canAccessDepartment = RequestStateManager.CanManageDepartment(currentUser, department);
                if (!canAccessDepartment)
                {
                    _logger.LogWarning("Manager {UserId} in department {UserDept} attempted to access users for department {TargetDept} but lacks permission",
                        currentUserId, currentUserDept, department);
                    return Forbid(); // Or return empty list depending on policy
                }
            }

            try
            {
                _logger.LogInformation("Getting users for department: {Department}", department);
                // Assuming IUserService has a method to get users by department
                var users = await _userService.GetUsersByDepartmentAsync(department);

                if (users == null || !users.Any())
                {
                    _logger.LogInformation("No users found for department: {Department}", department);
                    // Return empty list instead of NotFound for potentially valid but empty departments
                    return Ok(ApiResponse<IEnumerable<UserDto>>.SuccessResponse(new List<UserDto>()));
                }

                return Ok(ApiResponse<IEnumerable<UserDto>>.SuccessResponse(users));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving users for department {Department}: {Message}", department, ex.Message);
                return StatusCode(500, ApiResponse<IEnumerable<UserDto>>.ErrorResponse("An error occurred retrieving users by department"));
            }
        }

        [HttpPut("{id}")]
        [Route(ApiRoutes.Users.Update)]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<bool>>> UpdateUser(int id, [FromBody] UpdateUserRequest userDto)
        {
            try
            {
                if (id <= 0)
                {
                    return BadRequest(ApiResponse<bool>.ErrorResponse("Invalid user ID"));
                }

                if (userDto == null)
                {
                    return BadRequest(ApiResponse<bool>.ErrorResponse("User data is required"));
                }

                // Validate input
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
                    id, User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
                return Ok(ApiResponse<bool>.SuccessResponse(true, "User updated successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user {UserId}: {Message}", id, ex.Message);
                return StatusCode(500, ApiResponse<bool>.ErrorResponse("An error occurred updating the user"));
            }
        }

        [HttpDelete("{id}")]
        [Route(ApiRoutes.Users.Delete)]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteUser(int id)
        {
            try
            {
                if (id <= 0)
                {
                    return BadRequest(ApiResponse<bool>.ErrorResponse("Invalid user ID"));
                }

                // Prevent deleting yourself
                var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                if (id == currentUserId)
                {
                    return BadRequest(ApiResponse<bool>.ErrorResponse("You cannot delete your own account"));
                }

                var success = await _userService.DeleteAsync(id);
                if (!success)
                {
                    return NotFound(ApiResponse<bool>.ErrorResponse("User not found or delete failed"));
                }

                _logger.LogInformation("User {UserId} deleted by admin {AdminId}",
                    id, currentUserId);
                return Ok(ApiResponse<bool>.SuccessResponse(true, "User deleted successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user {UserId}: {Message}", id, ex.Message);
                return StatusCode(500, ApiResponse<bool>.ErrorResponse("An error occurred deleting the user"));
            }
        }

        [HttpPost("change-password")]
        [Route(ApiRoutes.Users.ChangePassword)]
        [EnableRateLimiting("auth")]
        public async Task<ActionResult<ApiResponse<bool>>> ChangePassword([FromBody] ChangePasswordDto changePasswordDto)
        {
            try
            {
                if (changePasswordDto == null)
                {
                    return BadRequest(ApiResponse<bool>.ErrorResponse("Password data is required"));
                }

                // Validate password criteria
                if (string.IsNullOrEmpty(changePasswordDto.NewPassword) || changePasswordDto.NewPassword.Length < 8)
                {
                    return BadRequest(ApiResponse<bool>.ErrorResponse("New password must be at least 8 characters long"));
                }

                var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var success = await _userService.ChangePasswordAsync(
                    currentUserId,
                    changePasswordDto.CurrentPassword,
                    changePasswordDto.NewPassword);

                if (!success)
                {
                    _logger.LogWarning("Failed password change attempt for user {UserId}", currentUserId);
                    return BadRequest(ApiResponse<bool>.ErrorResponse("Password change failed. Current password may be incorrect."));
                }

                _logger.LogInformation("Password changed successfully for user {UserId}", currentUserId);
                return Ok(ApiResponse<bool>.SuccessResponse(true, "Password changed successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password: {Message}", ex.Message);
                return StatusCode(500, ApiResponse<bool>.ErrorResponse("An error occurred changing the password"));
            }
        }

        [HttpPut("profile")]
        public async Task<ActionResult<ApiResponse<bool>>> UpdateMyProfile([FromBody] UpdateMyProfileRequest dto)
        {
            try
            {
                if (dto == null)
                {
                    return BadRequest(ApiResponse<bool>.ErrorResponse("User data is required"));
                }

                // Get current user ID from token
                var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                if (currentUserId <= 0)
                {
                    _logger.LogWarning("UpdateMyProfile attempted with invalid token (missing/invalid user ID)");
                    return Unauthorized(ApiResponse<bool>.ErrorResponse("Invalid authentication token"));
                }

                // Call the service method for self-updates
                var success = await _userService.UpdateSelfAsync(currentUserId, dto);

                if (!success)
                {
                    // User might not exist if deleted between token issuance and call, although unlikely.
                    return NotFound(ApiResponse<bool>.ErrorResponse("User not found or update failed"));
                }

                _logger.LogInformation("User {UserId} updated their profile successfully", currentUserId);
                return Ok(ApiResponse<bool>.SuccessResponse(true, "Profile updated successfully"));
            }
            catch (ValidationException vex)
            {
                _logger.LogWarning("Validation error updating profile for user {UserId}: {Message}",
                    User.FindFirst(ClaimTypes.NameIdentifier)?.Value, vex.Message);
                return BadRequest(ApiResponse<bool>.ErrorResponse(vex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile for user {UserId}: {Message}",
                    User.FindFirst(ClaimTypes.NameIdentifier)?.Value, ex.Message);
                return StatusCode(500, ApiResponse<bool>.ErrorResponse("An error occurred updating the profile"));
            }
        }

        [HttpPost("profile/image")]
        [Route(ApiRoutes.Users.UploadProfilePicture)]
        [RequestSizeLimit(5 * 1024 * 1024)] // Limit request size to 5MB
        [RequestFormLimits(MultipartBodyLengthLimit = 5 * 1024 * 1024)] // Limit form body size
        public async Task<ActionResult<ApiResponse<bool>>> UploadProfilePicture(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return BadRequest(ApiResponse<bool>.ErrorResponse("No file uploaded or file is empty."));
                }

                // Validate file type (example: allow only jpeg and png)
                var allowedContentTypes = new[] { "image/jpeg", "image/png", "image/gif" }; // Add gif or others if needed
                if (!allowedContentTypes.Contains(file.ContentType.ToLowerInvariant()))
                {
                    return BadRequest(ApiResponse<bool>.ErrorResponse("Invalid file type. Only JPG, PNG, GIF allowed."));
                }

                // Get current user ID
                var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                if (currentUserId <= 0)
                {
                    _logger.LogWarning("UploadProfilePicture attempted with invalid token");
                    return Unauthorized(ApiResponse<bool>.ErrorResponse("Invalid authentication token"));
                }

                // Get stream from uploaded file
                await using var stream = file.OpenReadStream();

                // Call service to update picture
                var success = await _userService.UpdateProfilePictureAsync(currentUserId, stream, file.ContentType);

                if (!success)
                {
                    // Service layer might have returned false if user not found or other issue
                    return NotFound(ApiResponse<bool>.ErrorResponse("User not found or failed to update picture."));
                }

                _logger.LogInformation("User {UserId} uploaded a new profile picture ({FileName}, {ContentType}, {Size} bytes)",
                    currentUserId, file.FileName, file.ContentType, file.Length);

                // Return success. Client might need to re-fetch profile to see the change.
                return Ok(ApiResponse<bool>.SuccessResponse(true, "Profile picture updated successfully."));
            }
            catch (ValidationException vex)
            {
                 _logger.LogWarning("Validation error uploading profile picture for user {UserId}: {Message}",
                    User.FindFirst(ClaimTypes.NameIdentifier)?.Value, vex.Message);
                return BadRequest(ApiResponse<bool>.ErrorResponse(vex.Message)); // e.g., file too large
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading profile picture for user {UserId}: {Message}",
                    User.FindFirst(ClaimTypes.NameIdentifier)?.Value, ex.Message);
                return StatusCode(500, ApiResponse<bool>.ErrorResponse("An error occurred uploading the profile picture."));
            }
        }

        [HttpGet("online")]
        [Route(ApiRoutes.Users.GetOnline)]
        [Authorize] // All authenticated users can see who is online, or adjust roles if needed
        public async Task<ActionResult<ApiResponse<IEnumerable<UserDto>>>> GetOnlineUsers()
        {
            try
            {
                var users = await _userService.GetOnlineUsersAsync();
                return Ok(ApiResponse<IEnumerable<UserDto>>.SuccessResponse(users));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving online users: {Message}", ex.Message);
                return StatusCode(500, ApiResponse<IEnumerable<UserDto>>.ErrorResponse("An error occurred retrieving online users"));
            }
        }

        [HttpGet("all")] // Route: api/users/all
        [Route(ApiRoutes.Users.GetAllWithStatus)]
        [Authorize] // All authenticated users should be able to get this list for presence.
        public async Task<ActionResult<ApiResponse<PaginatedResult<UserDto>>>> GetAllUsersWithStatus([FromQuery] int page = 1, [FromQuery] int pageSize = 1000)
        {
            try
            {
                var users = await _userService.GetPaginatedAsync(page, pageSize);
                return Ok(ApiResponse<PaginatedResult<UserDto>>.SuccessResponse(users));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all users with presence: {Message}", ex.Message);
                return StatusCode(500, ApiResponse<PaginatedResult<UserDto>>.ErrorResponse("An error occurred retrieving all users"));
            }
        }

        [HttpPut("{id}/connection")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<bool>>> UpdateUserConnection(int id, [FromBody] UpdateConnectionRequest request)
        {
            try
            {
                if (id <= 0)
                {
                    return BadRequest(ApiResponse<bool>.ErrorResponse("Invalid user ID"));
                }

                if (request == null)
                {
                    return BadRequest(ApiResponse<bool>.ErrorResponse("Connection data is required"));
                }

                // Check if the user is updating their own connection status or is an admin
                var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var isAdmin = User.IsInRole("Admin");

                if (id != currentUserId && !isAdmin)
                {
                    _logger.LogWarning("User {CurrentUserId} attempted to update connection status for user {RequestedId}",
                        currentUserId, id);
                    return Forbid();
                }

                // Update user presence based on connection status
                var success = await _userService.UpdateUserPresenceAsync(id, request.IsConnected);
                if (!success)
                {
                    return NotFound(ApiResponse<bool>.ErrorResponse("User not found or update failed"));
                }

                _logger.LogInformation("User {UserId} connection status updated to {IsConnected} by user {UpdatedBy}",
                    id, request.IsConnected, currentUserId);
                return Ok(ApiResponse<bool>.SuccessResponse(true, "Connection status updated successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating connection status for user {UserId}: {Message}", id, ex.Message);
                return StatusCode(500, ApiResponse<bool>.ErrorResponse("An error occurred updating the connection status"));
            }
        }
    }
}