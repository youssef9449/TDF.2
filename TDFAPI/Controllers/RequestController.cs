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
using Microsoft.EntityFrameworkCore;
using TDFShared.Enums;
using MediatR;
using TDFAPI.CQRS.Queries;
using TDFAPI.CQRS.Commands;

namespace TDFAPI.Controllers
{
    [ApiController]
    [Authorize]
    [Route(ApiRoutes.Requests.Base)]
    [ApiVersion("1.0")]
    public class RequestController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<RequestController> _logger;

        public RequestController(
            IMediator mediator,
            ILogger<RequestController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        // GET: api/requests
        [HttpGet("")]
        public async Task<ActionResult<PaginatedResult<RequestResponseDto>>> GetAllRequests([FromQuery] RequestPaginationDto pagination)
        {
            try
            {
                var result = await _mediator.Send(new GetRequestsQuery
                {
                    CurrentUserId = GetCurrentUserId(),
                    Pagination = pagination
                });
                return Ok(result);
            }
            catch (System.UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving requests");
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/requests/my
        [HttpGet("my")]
        public async Task<ActionResult<PaginatedResult<RequestResponseDto>>> GetMyRequests([FromQuery] RequestPaginationDto pagination)
        {
            // Reusing GetRequestsQuery as it handles "Own" access level correctly
            return await GetAllRequests(pagination);
        }

        // GET: api/requests/{id}
        [HttpGet("{id:int}")]
        public async Task<ActionResult<RequestResponseDto>> GetRequestById(int id)
        {
            try
            {
                var result = await _mediator.Send(new GetRequestByIdQuery
                {
                    RequestId = id,
                    CurrentUserId = GetCurrentUserId()
                });
                return Ok(result);
            }
            catch (EntityNotFoundException)
            {
                return NotFound();
            }
            catch (System.UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving request {RequestId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        // POST: api/requests
        [HttpPost("")]
        public async Task<ActionResult<RequestResponseDto>> CreateRequest([FromBody] RequestCreateDto createDto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                var result = await _mediator.Send(new CreateRequestCommand
                {
                    CreateDto = createDto,
                    UserId = GetCurrentUserId()
                });
                return CreatedAtAction(nameof(GetRequestById), new { id = result.RequestID }, result);
            }
            catch (TDFShared.Exceptions.ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (TDFShared.Exceptions.BusinessRuleException ex)
            {
                return BadRequest(ex.Message);
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
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                var result = await _mediator.Send(new UpdateRequestCommand
                {
                    RequestId = id,
                    UpdateDto = updateDto,
                    UserId = GetCurrentUserId()
                });
                return Ok(result);
            }
            catch (EntityNotFoundException)
            {
                return NotFound();
            }
            catch (System.UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (TDFShared.Exceptions.ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating request {RequestId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        // DELETE: api/requests/{id}
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteRequest(int id)
        {
            try
            {
                var success = await _mediator.Send(new DeleteRequestCommand
                {
                    RequestId = id,
                    UserId = GetCurrentUserId()
                });
                if (success) return NoContent();
                return BadRequest("Failed to delete request");
            }
            catch (EntityNotFoundException)
            {
                return NotFound();
            }
            catch (System.UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting request {RequestId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/requests/recent-dashboard
        [HttpGet(ApiRoutes.Requests.GetRecentDashboard)]
        public async Task<ActionResult<List<RequestResponseDto>>> GetRecentRequestsForDashboard()
        {
            try
            {
                var result = await _mediator.Send(new GetRecentDashboardRequestsQuery { UserId = GetCurrentUserId() });
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving recent dashboard requests");
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/requests/pending-dashboard-count
        [HttpGet(ApiRoutes.Requests.GetPendingDashboardCount)]
        public async Task<ActionResult<int>> GetPendingRequestsCountForDashboard()
        {
            try
            {
                var result = await _mediator.Send(new GetPendingRequestsCountQuery { UserId = GetCurrentUserId() });
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving pending dashboard requests count");
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/requests/approval
        [HttpGet(ApiRoutes.Requests.GetForApproval)]
        [Authorize(Roles = "Manager,HR,Admin")]
        public async Task<ActionResult<PaginatedResult<RequestResponseDto>>> GetRequestsForApproval([FromQuery] RequestPaginationDto pagination)
        {
            try
            {
                var result = await _mediator.Send(new GetRequestsForApprovalQuery
                {
                    UserId = GetCurrentUserId(),
                    Pagination = pagination
                });
                return Ok(result);
            }
            catch (System.UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving requests for approval");
                return StatusCode(500, "Internal server error");
            }
        }

        // POST: api/requests/{id}/manager/approve
        [HttpPost(ApiRoutes.Requests.ManagerApproveTemplate)]
        [Authorize(Roles = "Manager,Admin")]
        public async Task<ActionResult<bool>> ManagerApproveRequest(int id, [FromBody] ManagerApprovalDto approvalDto)
        {
            try
            {
                var result = await _mediator.Send(new ApproveRequestCommand
                {
                    RequestId = id,
                    ApproverId = GetCurrentUserId(),
                    IsHR = false,
                    Remarks = approvalDto.ManagerRemarks
                });
                return Ok(result);
            }
            catch (EntityNotFoundException)
            {
                return NotFound();
            }
            catch (System.UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (TDFShared.Exceptions.BusinessRuleException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in manager approval for request {RequestId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        // POST: api/requests/{id}/hr/approve
        [HttpPost(ApiRoutes.Requests.HRApproveTemplate)]
        [Authorize(Roles = "HR,Admin")]
        public async Task<ActionResult<bool>> HRApproveRequest(int id, [FromBody] HRApprovalDto approvalDto)
        {
            try
            {
                var result = await _mediator.Send(new ApproveRequestCommand
                {
                    RequestId = id,
                    ApproverId = GetCurrentUserId(),
                    IsHR = true,
                    Remarks = approvalDto.HRRemarks
                });
                return Ok(result);
            }
            catch (EntityNotFoundException)
            {
                return NotFound();
            }
            catch (System.UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (TDFShared.Exceptions.BusinessRuleException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in HR approval for request {RequestId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        // POST: api/requests/{id}/manager/reject
        [HttpPost(ApiRoutes.Requests.ManagerRejectTemplate)]
        [Authorize(Roles = "Manager,Admin")]
        public async Task<ActionResult<bool>> ManagerRejectRequest(int id, [FromBody] ManagerRejectDto rejectDto)
        {
            try
            {
                var result = await _mediator.Send(new RejectRequestCommand
                {
                    RequestId = id,
                    RejecterId = GetCurrentUserId(),
                    IsHR = false,
                    Remarks = rejectDto.ManagerRemarks
                });
                return Ok(result);
            }
            catch (EntityNotFoundException)
            {
                return NotFound();
            }
            catch (System.UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (TDFShared.Exceptions.BusinessRuleException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in manager rejection for request {RequestId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        // POST: api/requests/{id}/hr/reject
        [HttpPost(ApiRoutes.Requests.HRRejectTemplate)]
        [Authorize(Roles = "HR,Admin")]
        public async Task<ActionResult<bool>> HRRejectRequest(int id, [FromBody] HRRejectDto rejectDto)
        {
            try
            {
                var result = await _mediator.Send(new RejectRequestCommand
                {
                    RequestId = id,
                    RejecterId = GetCurrentUserId(),
                    IsHR = true,
                    Remarks = rejectDto.HRRemarks
                });
                return Ok(result);
            }
            catch (EntityNotFoundException)
            {
                return NotFound();
            }
            catch (System.UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (TDFShared.Exceptions.BusinessRuleException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in HR rejection for request {RequestId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? User.FindFirstValue("nameid");
            if (int.TryParse(userIdClaim, out int userId)) return userId;
            return 0;
        }
    }
}
