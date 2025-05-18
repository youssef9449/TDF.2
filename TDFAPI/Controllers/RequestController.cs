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

        public RequestController(
            IRequestService requestService,
            IUserService userService,
            ILogger<RequestController> logger)
        {
            _requestService = requestService;
            _userService = userService;
            _logger = logger;
        }

        // GET: api/requests
        [HttpGet("")]
        [Route(ApiRoutes.Requests.GetAll)]
        [Authorize(Roles = "Admin,HR")]
        public async Task<ActionResult<PaginatedResult<RequestResponseDto>>> GetAllRequests([FromQuery] RequestPaginationDto pagination)
        {
            try
            {
                _logger.LogInformation("Admin/HR getting all requests with pagination: {@Pagination}", pagination);
                var result = await _requestService.GetAllAsync(pagination);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all requests");
                return StatusCode(500, new { message = "Internal server error retrieving all requests." });
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
                _logger.LogInformation("Getting requests for department '{Department}' with pagination: {@Pagination}", department, pagination);
                var result = await _requestService.GetByDepartmentAsync(department, pagination);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving requests for department {Department}", department);
                return StatusCode(500, new { message = $"Internal server error retrieving requests for department {department}." });
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
            catch (UnauthorizedAccessException ex)
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
                 _logger.LogInformation("Admin/HR/Manager getting requests for user {TargetUserId} with pagination: {@Pagination}", userId, pagination);
                var result = await _requestService.GetByUserIdAsync(userId, pagination);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving requests for user ID {UserId}", userId);
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/requests/{id}
        [HttpGet("{id:guid}")]
        [Route(ApiRoutes.Requests.GetById)]
        public async Task<ActionResult<RequestResponseDto>> GetRequestById(Guid id)
        {
            try
            {
                _logger.LogInformation("Attempting to retrieve request with GUID {RequestId}", id);
                RequestResponseDto requestDto = await _requestService.GetByIdAsync(id);

                if (requestDto == null)
                {
                    _logger.LogWarning("Request with GUID {RequestId} not found.", id);
                    return NotFound();
                }

                int currentUserId = GetCurrentUserId();
                var currentUser = await _userService.GetUserByIdAsync(currentUserId);
                if (currentUser == null) return Unauthorized("User not found.");

                bool isAdmin = User.IsInRole("Admin");
                bool isHR = User.IsInRole("HR");
                bool isManager = User.IsInRole("Manager");

                if (requestDto.RequestUserID != currentUserId && !isAdmin && !isHR)
                {
                    if (isManager && currentUser.Department == requestDto.RequestDepartment)
                    {
                        // Manager in the same department can view
                    }
                    else
                    {
                         _logger.LogWarning("User {UserId} (Roles: {Roles}, Dept: {Dept}) tried to access request {RequestId} belonging to {OwnerId} in dept {RequestDept}",
                            currentUserId, string.Join(',', User.FindAll(ClaimTypes.Role).Select(c=>c.Value)), currentUser.Department, id, requestDto.RequestUserID, requestDto.RequestDepartment);
                         return Forbid();
                    }
                }

                return Ok(requestDto);
            }
            catch (UnauthorizedAccessException ex)
            {
                 _logger.LogWarning(ex, "Unauthorized access attempt in GetRequestById for GUID {RequestId}", id);
                 return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving request with GUID {RequestId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        // POST: api/requests
        [HttpPost("")]
        [Route(ApiRoutes.Requests.Create)]
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

                _logger.LogInformation("User {UserId} attempting to create request: {@CreateDto}", userId, createDto);

                var createdRequestDto = await _requestService.CreateAsync(createDto, userId);
                if (createdRequestDto == null)
                {
                    _logger.LogError("Failed to create request for user {UserId}", userId);
                    return StatusCode(StatusCodes.Status500InternalServerError, "Failed to create request.");
                }

                return CreatedAtAction(nameof(GetRequestById), new { id = createdRequestDto.Id }, createdRequestDto);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Validation error creating request.");
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                 _logger.LogWarning(ex, "Business logic error creating request.");
                return BadRequest(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
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
        [HttpPut("{id:guid}")]
        [Route(ApiRoutes.Requests.Update)]
        public async Task<ActionResult<RequestResponseDto>> UpdateRequest(Guid id, [FromBody] RequestUpdateDto updateDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                int userId = GetCurrentUserId();
                if (userId == 0) return Unauthorized("User ID could not be determined.");

                _logger.LogInformation("User {UserId} attempting to update request GUID {RequestId} with data: {@UpdateDto}", userId, id, updateDto);

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
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Validation error updating request GUID {RequestId}.", id);
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Business logic error updating request GUID {RequestId}.", id);
                return BadRequest(ex.Message);
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning("Update failed: Request with GUID {RequestId} not found.", id);
                return NotFound(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                 _logger.LogWarning(ex, "Unauthorized access attempt in UpdateRequest for GUID {RequestId}", id);
                 return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating request GUID {RequestId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred while updating the request.");
            }
        }

        // DELETE: api/requests/{id}
        [HttpDelete("{id:guid}")]
        [Route(ApiRoutes.Requests.Delete)]
        public async Task<IActionResult> DeleteRequest(Guid id)
        {
            try
            {
                int currentUserId = GetCurrentUserId();

                var requestDto = await _requestService.GetByIdAsync(id);
                if (requestDto == null)
                    return NotFound();

                bool isAdmin = User.IsInRole("Admin");

                if (requestDto.RequestUserID != currentUserId && !isAdmin)
                {
                    _logger.LogWarning("User {UserId} tried to delete request {RequestId} belonging to {OwnerId}", currentUserId, id, requestDto.RequestUserID);
                    return Forbid();
                }

                if (await _requestService.DeleteAsync(id, currentUserId))
                    return NoContent();

                _logger.LogWarning("Failed to delete request {RequestId} by user {UserId}", id, currentUserId);
                return BadRequest("Failed to delete request");
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Validation error deleting request {RequestId}: {ErrorMessage}", id, ex.Message);
                return BadRequest(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                 _logger.LogWarning(ex, "Unauthorized access attempt in DeleteRequest for GUID {RequestId}", id);
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                 _logger.LogWarning(ex, "Business logic error deleting request {RequestId}: {ErrorMessage}", id, ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting request with GUID {RequestId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        // POST: api/requests/{id}/approve
        [HttpPost("{id:guid}/approve")]
        [Route(ApiRoutes.Requests.Approve)]
        [Authorize(Roles = "Admin,Manager,HR")]
        public async Task<IActionResult> ApproveRequest(Guid id, [FromBody] RequestApprovalDto approvalDto)
        {
            try
            {
                int approverId = GetCurrentUserId();
                var approver = await _userService.GetUserByIdAsync(approverId);
                if (approver == null) return Unauthorized("Approver not found.");

                // Ensure approver has roles before accessing them
                bool isHR = approver.Role?.Contains("HR") ?? false;
                _logger.LogInformation("{Role} {ApproverName} ({ApproverId}) attempting to approve request {RequestId}: {@ApprovalDto}",
                    (isHR ? "HR" : "Manager"), approver.FullName, approverId, id, approvalDto);

                bool success = await _requestService.ApproveRequestAsync(id, approvalDto, approverId, approver.FullName, isHR);

                if (!success)
                {
                    var request = await _requestService.GetByIdAsync(id);
                    if (request == null)
                    {
                        _logger.LogWarning("Attempted to approve non-existent request {RequestId}", id);
                        return NotFound($"Request with ID {id} not found.");
                    }
                    else
                    {
                         _logger.LogWarning("Failed to approve request {RequestId}. Request status might prevent approval.", id);
                         return BadRequest("Failed to approve request. It might be in a state that cannot be approved (e.g., already finalized).");
                    }
                }

                return Ok(new { message = "Request approved successfully." });
            }
            catch (ArgumentException ex)
            {
                 _logger.LogWarning(ex, "Validation error approving request {RequestId}: {ErrorMessage}", id, ex.Message);
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                 _logger.LogWarning(ex, "Business logic error approving request {RequestId}: {ErrorMessage}", id, ex.Message);
                return BadRequest(ex.Message);
            }
             catch (UnauthorizedAccessException ex)
            {
                 _logger.LogWarning(ex, "Unauthorized access attempt in ApproveRequest for GUID {RequestId}", id);
                 return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving request with GUID {RequestId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        // POST: api/requests/{id}/reject
        [HttpPost("{id:guid}/reject")]
        [Route(ApiRoutes.Requests.Reject)]
        [Authorize(Roles = "Admin,Manager,HR")]
        public async Task<IActionResult> RejectRequest(Guid id, [FromBody] RequestRejectDto rejectDto)
        {
            try
            {
                int rejecterId = GetCurrentUserId();
                var rejecter = await _userService.GetUserByIdAsync(rejecterId);
                if (rejecter == null) return Unauthorized("Rejecter not found.");

                // Ensure rejecter has roles before accessing them
                bool isHR = rejecter.Role?.Contains("HR") ?? false;
                _logger.LogInformation("{Role} {RejecterName} ({RejecterId}) attempting to reject request {RequestId}: {@RejectDto}",
                    (isHR ? "HR" : "Manager"), rejecter.FullName, rejecterId, id, rejectDto);

                bool success = await _requestService.RejectRequestAsync(id, rejectDto, rejecterId, rejecter.FullName, isHR);

                if (!success)
                {
                    var request = await _requestService.GetByIdAsync(id);
                    if (request == null)
                    {
                        _logger.LogWarning("Attempted to reject non-existent request {RequestId}", id);
                        return NotFound($"Request with ID {id} not found.");
                    }
                     else
                    {
                         _logger.LogWarning("Failed to reject request {RequestId}. Request status might prevent rejection.", id);
                         return BadRequest("Failed to reject request. It might be in a state that cannot be rejected (e.g., already finalized).");
                    }
                }

                return Ok(new { message = "Request rejected successfully." });
            }
            catch (ArgumentException ex)
            {
                 _logger.LogWarning(ex, "Validation error rejecting request {RequestId}: {ErrorMessage}", id, ex.Message);
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                 _logger.LogWarning(ex, "Business logic error rejecting request {RequestId}: {ErrorMessage}", id, ex.Message);
                return BadRequest(ex.Message);
            }
             catch (UnauthorizedAccessException ex)
            {
                 _logger.LogWarning(ex, "Unauthorized access attempt in RejectRequest for GUID {RequestId}", id);
                 return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting request with GUID {RequestId}", id);
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

        #endregion
    }
}
