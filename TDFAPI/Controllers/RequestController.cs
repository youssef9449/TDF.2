using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TDFAPI.CQRS.Commands;
using TDFAPI.CQRS.Queries;
using TDFShared.Constants;
using TDFShared.DTOs.Common;
using TDFShared.DTOs.Requests;

namespace TDFAPI.Controllers
{
    [ApiController]
    [Authorize]
    [Route(ApiRoutes.Requests.Base)]
    [ApiVersion("1.0")]
    public class RequestController : ControllerBase
    {
        private readonly IMediator _mediator;

        public RequestController(IMediator mediator)
        {
            _mediator = mediator;
        }

        // GET: api/requests
        [HttpGet("")]
        public async Task<ActionResult<ApiResponse<PaginatedResult<RequestResponseDto>>>> GetAllRequests(
            [FromQuery] RequestPaginationDto pagination)
        {
            var result = await _mediator.Send(new GetRequestsQuery
            {
                CurrentUserId = GetCurrentUserId(),
                Pagination = pagination
            });
            return Ok(ApiResponse<PaginatedResult<RequestResponseDto>>.SuccessResponse(result));
        }

        // GET: api/requests/my
        [HttpGet("my")]
        public Task<ActionResult<ApiResponse<PaginatedResult<RequestResponseDto>>>> GetMyRequests(
            [FromQuery] RequestPaginationDto pagination)
        {
            // Reusing GetRequestsQuery as it handles "Own" access level correctly
            return GetAllRequests(pagination);
        }

        // GET: api/requests/{id}
        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiResponse<RequestResponseDto>>> GetRequestById(int id)
        {
            var result = await _mediator.Send(new GetRequestByIdQuery
            {
                RequestId = id,
                CurrentUserId = GetCurrentUserId()
            });
            return Ok(ApiResponse<RequestResponseDto>.SuccessResponse(result));
        }

        // POST: api/requests
        [HttpPost("")]
        public async Task<ActionResult<ApiResponse<RequestResponseDto>>> CreateRequest(
            [FromBody] RequestCreateDto createDto)
        {
            var result = await _mediator.Send(new CreateRequestCommand
            {
                CreateDto = createDto,
                UserId = GetCurrentUserId()
            });
            var response = ApiResponse<RequestResponseDto>.SuccessResponse(result, "Request created successfully");
            return CreatedAtAction(nameof(GetRequestById), new { id = result.RequestID }, response);
        }

        // PUT: api/requests/{id}
        [HttpPut("{id:int}")]
        public async Task<ActionResult<ApiResponse<RequestResponseDto>>> UpdateRequest(
            int id, [FromBody] RequestUpdateDto updateDto)
        {
            var result = await _mediator.Send(new UpdateRequestCommand
            {
                RequestId = id,
                UpdateDto = updateDto,
                UserId = GetCurrentUserId()
            });
            return Ok(ApiResponse<RequestResponseDto>.SuccessResponse(result, "Request updated successfully"));
        }

        // DELETE: api/requests/{id}
        [HttpDelete("{id:int}")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteRequest(int id)
        {
            var success = await _mediator.Send(new DeleteRequestCommand
            {
                RequestId = id,
                UserId = GetCurrentUserId()
            });
            if (success) return Ok(ApiResponse<bool>.SuccessResponse(true, "Request deleted successfully"));
            return BadRequest(ApiResponse<bool>.ErrorResponse("Failed to delete request"));
        }

        // GET: api/requests/recent-dashboard
        [HttpGet(ApiRoutes.Requests.GetRecentDashboard)]
        public async Task<ActionResult<ApiResponse<List<RequestResponseDto>>>> GetRecentRequestsForDashboard()
        {
            var result = await _mediator.Send(new GetRecentDashboardRequestsQuery { UserId = GetCurrentUserId() });
            return Ok(ApiResponse<List<RequestResponseDto>>.SuccessResponse(result));
        }

        // GET: api/requests/pending-dashboard-count
        [HttpGet(ApiRoutes.Requests.GetPendingDashboardCount)]
        public async Task<ActionResult<ApiResponse<int>>> GetPendingRequestsCountForDashboard()
        {
            var result = await _mediator.Send(new GetPendingRequestsCountQuery { UserId = GetCurrentUserId() });
            return Ok(ApiResponse<int>.SuccessResponse(result));
        }

        // GET: api/requests/approval
        [HttpGet(ApiRoutes.Requests.GetForApproval)]
        [Authorize(Roles = "Manager,HR,Admin")]
        public async Task<ActionResult<ApiResponse<PaginatedResult<RequestResponseDto>>>> GetRequestsForApproval(
            [FromQuery] RequestPaginationDto pagination)
        {
            var result = await _mediator.Send(new GetRequestsForApprovalQuery
            {
                UserId = GetCurrentUserId(),
                Pagination = pagination
            });
            return Ok(ApiResponse<PaginatedResult<RequestResponseDto>>.SuccessResponse(result));
        }

        // POST: api/requests/{id}/manager/approve
        [HttpPost(ApiRoutes.Requests.ManagerApproveTemplate)]
        [Authorize(Roles = "Manager,Admin")]
        public async Task<ActionResult<ApiResponse<bool>>> ManagerApproveRequest(
            int id, [FromBody] ManagerApprovalDto approvalDto)
        {
            var result = await _mediator.Send(new ApproveRequestCommand
            {
                RequestId = id,
                ApproverId = GetCurrentUserId(),
                IsHR = false,
                Remarks = approvalDto.ManagerRemarks
            });
            return Ok(ApiResponse<bool>.SuccessResponse(result, "Request approved by manager"));
        }

        // POST: api/requests/{id}/hr/approve
        [HttpPost(ApiRoutes.Requests.HRApproveTemplate)]
        [Authorize(Roles = "HR,Admin")]
        public async Task<ActionResult<ApiResponse<bool>>> HRApproveRequest(
            int id, [FromBody] HRApprovalDto approvalDto)
        {
            var result = await _mediator.Send(new ApproveRequestCommand
            {
                RequestId = id,
                ApproverId = GetCurrentUserId(),
                IsHR = true,
                Remarks = approvalDto.HRRemarks
            });
            return Ok(ApiResponse<bool>.SuccessResponse(result, "Request approved by HR"));
        }

        // POST: api/requests/{id}/manager/reject
        [HttpPost(ApiRoutes.Requests.ManagerRejectTemplate)]
        [Authorize(Roles = "Manager,Admin")]
        public async Task<ActionResult<ApiResponse<bool>>> ManagerRejectRequest(
            int id, [FromBody] ManagerRejectDto rejectDto)
        {
            var result = await _mediator.Send(new RejectRequestCommand
            {
                RequestId = id,
                RejecterId = GetCurrentUserId(),
                IsHR = false,
                Remarks = rejectDto.ManagerRemarks
            });
            return Ok(ApiResponse<bool>.SuccessResponse(result, "Request rejected by manager"));
        }

        // POST: api/requests/{id}/hr/reject
        [HttpPost(ApiRoutes.Requests.HRRejectTemplate)]
        [Authorize(Roles = "HR,Admin")]
        public async Task<ActionResult<ApiResponse<bool>>> HRRejectRequest(
            int id, [FromBody] HRRejectDto rejectDto)
        {
            var result = await _mediator.Send(new RejectRequestCommand
            {
                RequestId = id,
                RejecterId = GetCurrentUserId(),
                IsHR = true,
                Remarks = rejectDto.HRRemarks
            });
            return Ok(ApiResponse<bool>.SuccessResponse(result, "Request rejected by HR"));
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                              ?? User.FindFirstValue("sub")
                              ?? User.FindFirstValue("nameid");
            if (int.TryParse(userIdClaim, out int userId)) return userId;
            return 0;
        }
    }
}
