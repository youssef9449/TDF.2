using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using TDFAPI.Services;
using System;
using System.Security.Claims;
using TDFShared.DTOs.Requests;
using TDFShared.DTOs.Common;
using TDFShared.Constants;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Linq;
using TDFAPI.Exceptions;
using TDFShared.Services;
using TDFShared.Utilities;

namespace TDFAPI.Controllers
{
    [ApiController]
    [Authorize]
    [Route(ApiRoutes.Requests.Base)]
    [ApiVersion("1.0")]
    public class RequestController : ControllerBase
    {
        private readonly IRequestService _requestService;
        private readonly IUserService _userService;
        private readonly ILogger<RequestController> _logger;
        private readonly TDFShared.Validation.IBusinessRulesService _businessRulesService;
        private readonly TDFShared.Services.IErrorHandlingService _errorHandlingService;
        private readonly ICacheService _cacheService;
        private readonly TDFShared.Validation.IValidationService _validationService;

        public RequestController(
            IRequestService requestService,
            IUserService userService,
            ILogger<RequestController> logger,
            TDFShared.Validation.IBusinessRulesService businessRulesService,
            TDFShared.Services.IErrorHandlingService errorHandlingService,
            ICacheService cacheService,
            TDFShared.Validation.IValidationService validationService)
        {
            _requestService = requestService;
            _userService = userService;
            _logger = logger;
            _businessRulesService = businessRulesService;
            _errorHandlingService = errorHandlingService;
            _cacheService = cacheService;
            _validationService = validationService;
        }

        // GET: api/requests
        [HttpGet("")]
        [Authorize]
        public async Task<ActionResult<PaginatedResult<RequestResponseDto>>> GetAllRequests([FromQuery] RequestPaginationDto pagination)
        {
            try
            {
                int currentUserId = GetCurrentUserId();
                var currentUser = await GetCachedUserAsync(currentUserId);
                if (currentUser == null) return Unauthorized("User not found.");

                _logger.LogInformation("User {UserId} (Admin: {IsAdmin}, HR: {IsHR}, Manager: {IsManager}, Dept: {Department}) getting requests with pagination: {@Pagination}",
                    currentUserId, currentUser.IsAdmin, currentUser.IsHR, currentUser.IsManager, currentUser.Department, pagination);

                // Use RequestStateManager to check if user can manage requests
                bool canManage = RequestStateManager.CanManageRequests(currentUser);
                if (!canManage && currentUserId != 0)
                {
                    // If user can't manage requests, they can only see their own
                    _logger.LogInformation("Loading requests for regular user {UserId}", currentUserId);
                    var userResult = await _requestService.GetByUserIdAsync(currentUserId, pagination);
                    return Ok(userResult);
                }

                // Use shared authorization utilities to determine access level
                var accessLevel = AuthorizationUtilities.GetRequestAccessLevel(currentUser);
                PaginatedResult<RequestResponseDto> result;

                switch (accessLevel)
                {
                    case RequestAccessLevel.All:
                        // Admin and HR can see all requests
                        _logger.LogInformation("Loading all requests for Admin/HR user {UserId}", currentUserId);
                        result = await _requestService.GetAllAsync(pagination);
                        break;

                    case RequestAccessLevel.Department:
                        // Managers can see their own requests + requests from users in their department
                        _logger.LogInformation("Loading requests for Manager {UserId} in department '{Department}'", currentUserId, currentUser.Department);
                        result = await _requestService.GetRequestsForManagerAsync(currentUserId, currentUser.Department, pagination);
                        break;

                    case RequestAccessLevel.Own:
                        // Regular users can only see their own requests
                        _logger.LogInformation("Loading requests for regular user {UserId}", currentUserId);
                        result = await _requestService.GetByUserIdAsync(currentUserId, pagination);
                        break;

                    default:
                        _logger.LogWarning("User {UserId} has no request access", currentUserId);
                        return Forbid("You do not have permission to view requests.");
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving requests");
                var apiResponse = TDFShared.Utilities.ApiResponseUtilities.FromException<PaginatedResult<RequestResponseDto>>(ex, includeStackTrace: false);
                return StatusCode((int)apiResponse.StatusCode, apiResponse);
            }
        }

        // GET: api/requests/department/{department}
        [HttpGet("department/{department}")]
        [Route(ApiRoutes.Requests.GetByDepartment)]
        [Authorize(Roles = "Admin,Manager,HR")]
        public async Task<ActionResult<PaginatedResult<RequestResponseDto>>> GetRequestsByDepartment(string department, [FromQuery] RequestPaginationDto pagination)
        {
            try
            {
                int currentUserId = GetCurrentUserId();
                var currentUser = await GetCachedUserAsync(currentUserId);
                if (currentUser == null) return Unauthorized("User not found.");

                // Use RequestStateManager to check if current user can manage requests
                bool canManage = RequestStateManager.CanManageRequests(currentUser);
                if (!canManage)
                {
                    _logger.LogWarning("User {UserId} tried to access department '{Department}' but lacks management permissions",
                        currentUserId, department);
                    return Forbid("You do not have permission to view department requests.");
                }

                // Use RequestStateManager to check department access
                bool canAccessDepartment = RequestStateManager.CanManageDepartment(currentUser, department);
                if (!canAccessDepartment)
                {
                    _logger.LogWarning("User {UserId} from department '{UserDept}' tried to access department '{Department}' but lacks permission",
                        currentUserId, currentUser.Department, department);
                    return Forbid($"You do not have permission to view requests for department '{department}'.");
                }

                _logger.LogInformation("User {UserId} getting requests for department '{Department}' with pagination: {@Pagination}", currentUserId, department, pagination);
                var result = await _requestService.GetByDepartmentAsync(department, pagination);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving requests for department {Department}", department);
                var friendlyMessage = _errorHandlingService.GetFriendlyErrorMessage(ex, $"retrieving requests for department {department}");
                return StatusCode(500, new { message = friendlyMessage });
            }
        }

        // GET: api/requests/my
        [HttpGet("my")]
        [Route(ApiRoutes.Requests.GetMy)]
        public async Task<ActionResult<PaginatedResult<RequestResponseDto>>> GetMyRequests([FromQuery] RequestPaginationDto pagination)
        {
            try
            {
                int userId = GetCurrentUserId();
                _logger.LogInformation("User {UserId} getting their requests with pagination: {@Pagination}", userId, pagination);
                var result = await _requestService.GetByUserIdAsync(userId, pagination);
                return Ok(result);
            }
            catch (TDFAPI.Exceptions.UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt in GetMyRequests");
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving requests for current user");
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/requests/user/{userId}
        [HttpGet("user/{userId:int}")]
        [Route(ApiRoutes.Requests.GetByUserId)]
        [Authorize(Roles = "Admin,HR,Manager")]
        public async Task<ActionResult<PaginatedResult<RequestResponseDto>>> GetRequestsByUserId(int userId, [FromQuery] RequestPaginationDto pagination)
        {
            try
            {
                int currentUserId = GetCurrentUserId();
                var currentUser = await GetCachedUserAsync(currentUserId);
                if (currentUser == null) return Unauthorized("User not found.");

                // Get the target user to check department authorization
                var targetUser = await _userService.GetUserByIdAsync(userId);
                if (targetUser == null) return NotFound($"User with ID {userId} not found.");

                // Use RequestStateManager to check if current user can manage requests for the target user
                bool canManage = RequestStateManager.CanManageRequests(currentUser);
                if (!canManage)
                {
                    _logger.LogWarning("User {CurrentUserId} tried to access requests for user {TargetUserId} but lacks management permissions",
                        currentUserId, userId);
                    return Forbid("You do not have permission to view requests for other users.");
                }

                // For managers, ensure they can only access their department (including constituent departments for hyphenated departments)
                if ((currentUser.IsManager ?? false) && !(currentUser.IsAdmin ?? false) && !(currentUser.IsHR ?? false))
                {
                    bool canAccessDepartment = RequestStateManager.CanManageDepartment(currentUser, targetUser.Department);
                    if (!canAccessDepartment)
                    {
                        _logger.LogWarning("Manager {CurrentUserId} from department '{CurrentDept}' tried to access requests for user {TargetUserId} from department '{TargetDept}' but lacks permission",
                            currentUserId, currentUser.Department, userId, targetUser.Department);
                        return Forbid("You can only view requests for users in your department.");
                    }
                }

                _logger.LogInformation("User {CurrentUserId} getting requests for user {TargetUserId} with pagination: {@Pagination}", currentUserId, userId, pagination);
                var result = await _requestService.GetByUserIdAsync(userId, pagination);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving requests for user ID {UserId}", userId);
                var friendlyMessage = _errorHandlingService.GetFriendlyErrorMessage(ex, "retrieving user requests");
                return StatusCode(500, new { message = friendlyMessage });
            }
        }

        // GET: api/requests/{id}
        [HttpGet("{id:int}")]
        [Route(ApiRoutes.Requests.GetById)]
        public async Task<ActionResult<RequestResponseDto>> GetRequestById(int id)
        {
            try
            {
                _logger.LogInformation("Attempting to retrieve request with ID {RequestId}", id);
                RequestResponseDto requestDto = await _requestService.GetByIdAsync(id);

                if (requestDto == null)
                {
                    _logger.LogWarning("Request with ID {RequestId} not found.", id);
                    return NotFound();
                }

                int currentUserId = GetCurrentUserId();
                var currentUser = await GetCachedUserAsync(currentUserId);
                if (currentUser == null) return Unauthorized("User not found.");

                // Use RequestStateManager for authorization validation
                bool canView = RequestStateManager.CanViewRequest(requestDto, currentUser);
                if (!canView)
                {
                    _logger.LogWarning("User {UserId} (Roles: {Roles}, Dept: {Dept}) tried to access request {RequestId} belonging to {OwnerId} in dept {RequestDept}",
                        currentUserId, string.Join(',', User.FindAll(ClaimTypes.Role).Select(c => c.Value)), currentUser.Department, id, requestDto.RequestUserID, requestDto.RequestDepartment);
                    return Forbid("You do not have permission to view this request.");
                }

                return Ok(requestDto);
            }
            catch (TDFAPI.Exceptions.UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt in GetRequestById for ID {RequestId}", id);
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving request with ID {RequestId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        // POST: api/requests
        [HttpPost("")]
        public async Task<ActionResult<RequestResponseDto>> CreateRequest([FromBody] RequestCreateDto createDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                int userId = GetCurrentUserId();
                if (userId == 0) return Unauthorized("User ID could not be determined.");

                // Validate and sanitize input using shared validation service
                if (_validationService.ContainsDangerousPatterns(createDto.RequestReason))
                {
                    _logger.LogWarning("User {UserId} attempted to create request with potentially dangerous input", userId);
                    return BadRequest("Invalid input detected. Please check your request details.");
                }

                createDto.RequestReason = _validationService.SanitizeInput(createDto.RequestReason);

                _logger.LogInformation("User {UserId} attempting to create request: {@CreateDto}", userId, createDto);

                var createdRequestDto = await _requestService.CreateAsync(createDto, userId);
                if (createdRequestDto == null)
                {
                    _logger.LogError("Failed to create request for user {UserId}", userId);
                    return StatusCode(StatusCodes.Status500InternalServerError, "Failed to create request.");
                }

                return CreatedAtAction(nameof(GetRequestById), new { id = createdRequestDto.RequestID }, createdRequestDto);
            }
            catch (TDFShared.Exceptions.ValidationException ex)
            {
                _logger.LogWarning(ex, "Validation error creating request.");
                return BadRequest(ex.Message);
            }
            catch (TDFShared.Exceptions.BusinessRuleException ex)
            {
                _logger.LogWarning(ex, "Business logic error creating request.");
                return BadRequest(ex.Message);
            }
            catch (TDFAPI.Exceptions.UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt in CreateRequest");
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating request");
                return StatusCode(500, "Internal server error");
            }
        }

        // PUT: api/requests/{id}
        [HttpPut("{id:int}")]
        public async Task<ActionResult<RequestResponseDto>> UpdateRequest(int id, [FromBody] RequestUpdateDto updateDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                int userId = GetCurrentUserId();
                if (userId == 0) return Unauthorized("User ID could not be determined.");

                // Check if the request exists and if user can edit it
                var existingRequest = await _requestService.GetByIdAsync(id);
                if (existingRequest == null)
                {
                    _logger.LogWarning("Attempted to update non-existent request {RequestId}", id);
                    return NotFound($"Request with ID {id} not found.");
                }

                var currentUser = await _userService.GetUserByIdAsync(userId);
                if (currentUser == null) return Unauthorized("User not found.");

                // Use RequestStateManager to check edit rights
                bool isOwner = existingRequest.RequestUserID == userId;
                bool canEdit = RequestStateManager.CanEdit(existingRequest, currentUser.IsAdmin ?? false, isOwner);
                if (!canEdit)
                {
                    _logger.LogWarning("User {UserId} tried to update request {RequestId} belonging to {OwnerId} but lacks permission",
                        userId, id, existingRequest.RequestUserID);
                    return Forbid("You do not have permission to edit this request.");
                }

                // Sanitize input using shared validation service
                updateDto.RequestReason = _validationService.SanitizeInput(updateDto.RequestReason);

                _logger.LogInformation("User {UserId} attempting to update request ID {RequestId} with data: {@UpdateDto}", userId, id, updateDto);

                var result = await _requestService.UpdateAsync(id, updateDto, userId);
                bool success = result != null;

                if (!success)
                {
                    var request = await _requestService.GetByIdAsync(id);
                    if (request == null)
                    {
                        _logger.LogWarning("Attempted to update non-existent request {RequestId}", id);
                        return NotFound($"Request with ID {id} not found.");
                    }
                    else
                    {
                        _logger.LogWarning("Failed to update request {RequestId} by user {UserId}. Might be concurrency or other DB issue.", id, userId);
                        return BadRequest("Failed to update the request. It might have been modified or deleted by another user.");
                    }
                }

                return Ok(await _requestService.GetByIdAsync(id));
            }
            catch (TDFShared.Exceptions.ValidationException ex)
            {
                _logger.LogWarning(ex, "Validation error updating request ID {RequestId}.", id);
                return BadRequest(ex.Message);
            }
            catch (TDFShared.Exceptions.BusinessRuleException ex)
            {
                _logger.LogWarning(ex, "Business logic error updating request ID {RequestId}.", id);
                return BadRequest(ex.Message);
            }
            catch (EntityNotFoundException ex)
            {
                _logger.LogWarning("Update failed: Request with ID {RequestId} not found.", id);
                return NotFound(ex.Message);
            }
            catch (TDFAPI.Exceptions.UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt in UpdateRequest for ID {RequestId}", id);
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating request ID {RequestId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred while updating the request.");
            }
        }

        // DELETE: api/requests/{id}
        [HttpDelete("{id:int}")]
        [Route(ApiRoutes.Requests.Delete)]
        public async Task<IActionResult> DeleteRequest(int id)
        {
            try
            {
                int currentUserId = GetCurrentUserId();
                var currentUser = await _userService.GetUserByIdAsync(currentUserId);
                if (currentUser == null) return Unauthorized("User not found.");

                var requestDto = await _requestService.GetByIdAsync(id);
                if (requestDto == null)
                    return NotFound();

                // Use RequestStateManager to check delete rights
                bool isOwner = requestDto.RequestUserID == currentUserId;
                bool canDelete = RequestStateManager.CanDelete(requestDto, currentUser.IsAdmin ?? false, isOwner);
                if (!canDelete)
                {
                    _logger.LogWarning("User {UserId} tried to delete request {RequestId} belonging to {OwnerId} but lacks permission",
                        currentUserId, id, requestDto.RequestUserID);
                    return Forbid("You do not have permission to delete this request.");
                }

                if (await _requestService.DeleteAsync(id, currentUserId))
                    return NoContent();

                _logger.LogWarning("Failed to delete request {RequestId} by user {UserId}", id, currentUserId);
                return BadRequest("Failed to delete request");
            }
            catch (TDFShared.Exceptions.ValidationException ex)
            {
                _logger.LogWarning(ex, "Validation error deleting request {RequestId}: {ErrorMessage}", id, ex.Message);
                return BadRequest(ex.Message);
            }
            catch (TDFAPI.Exceptions.UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt in DeleteRequest for ID {RequestId}", id);
                return Forbid();
            }
            catch (TDFShared.Exceptions.BusinessRuleException ex)
            {
                _logger.LogWarning(ex, "Business logic error deleting request {RequestId}: {ErrorMessage}", id, ex.Message);
                return BadRequest(ex.Message);
            }
            catch (EntityNotFoundException ex)
            {
                _logger.LogWarning("Delete failed: Request with ID {RequestId} not found.", id);
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting request with ID {RequestId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        #region Helper Methods

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? User.FindFirstValue("nameid");
            if (int.TryParse(userIdClaim, out int userId))
            {
                return userId;
            }
            _logger.LogWarning("Could not find valid User ID claim in token.");
            return 0;
        }

        /// <summary>
        /// Gets user data with caching for better performance
        /// </summary>
        private async Task<TDFShared.DTOs.Users.UserDto?> GetCachedUserAsync(int userId)
        {
            var cacheKey = $"user_{userId}";
            return await _cacheService.GetOrCreateAsync(cacheKey,
                async () => await _userService.GetUserByIdAsync(userId),
                absoluteExpirationMinutes: 15, // Cache for 15 minutes
                slidingExpirationMinutes: 5);   // Extend if accessed within 5 minutes
        }

        /// <summary>
        /// Creates authorization context with caching for better performance
        /// </summary>
        private TDFShared.Validation.BusinessRuleContext CreateCachedAuthorizationContext()
        {
            return AuthorizationUtilities.CreateAuthorizationContext(
                async (requestId) => await _requestService.GetByIdAsync(requestId),
                async (userId) => await GetCachedUserAsync(userId));
        }

        #endregion
    }
}
