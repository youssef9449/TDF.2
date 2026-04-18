using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using TDFAPI.Services;
using TDFShared.Constants;
using TDFShared.DTOs.Common;

namespace TDFAPI.Controllers
{
    [Route(ApiRoutes.Lookups.Base)]
    [ApiController]
    [AllowAnonymous]
    [EnableRateLimiting("api")]
    public class LookupsController : ControllerBase
    {
        private readonly ILookupService _lookupService;
        private readonly ILogger<LookupsController> _logger;

        public LookupsController(ILookupService lookupService, ILogger<LookupsController> logger)
        {
            _lookupService = lookupService;
            _logger = logger;
        }

        [HttpGet("all")]
        [ResponseCache(Duration = 3600)]
        public async Task<ActionResult<ApiResponse<LookupResponseDto>>> GetAllLookups()
        {
            _logger.LogInformation("Getting all lookup data");
            Response.Headers.Append("Cache-Control", "public, max-age=3600");
            var allLookups = await _lookupService.GetAllLookupsAsync();
            return Ok(ApiResponse<LookupResponseDto>.SuccessResponse(allLookups));
        }

        [HttpGet("departments")]
        [ResponseCache(Duration = 3600)]
        public async Task<ActionResult<ApiResponse<List<LookupItem>>>> GetDepartments()
        {
            Response.Headers.Append("Cache-Control", "public, max-age=3600");
            var departments = await _lookupService.GetDepartmentsAsync() ?? new List<LookupItem>();
            _logger.LogInformation("Returning {Count} departments", departments.Count);
            return Ok(ApiResponse<List<LookupItem>>.SuccessResponse(departments));
        }

        [HttpGet("titles/{department}")]
        public async Task<ActionResult<ApiResponse<List<string>>>> GetTitlesByDepartment(string department)
        {
            var titles = await _lookupService.GetTitlesByDepartmentAsync(department);
            return Ok(ApiResponse<List<string>>.SuccessResponse(titles));
        }

        [HttpGet("leave-types")]
        [ResponseCache(Duration = 3600)]
        public async Task<ActionResult<ApiResponse<List<LookupItem>>>> GetLeaveTypes()
        {
            Response.Headers.Append("Cache-Control", "public, max-age=3600");
            var leaveTypes = await _lookupService.GetLeaveTypesAsync();
            return Ok(ApiResponse<List<LookupItem>>.SuccessResponse(leaveTypes));
        }

        [HttpGet("requesttypes")]
        [ResponseCache(Duration = 3600)]
        public async Task<ActionResult<ApiResponse<List<string>>>> GetRequestTypes()
        {
            Response.Headers.Append("Cache-Control", "public, max-age=3600");
            var requestTypes = await _lookupService.GetRequestTypesAsync();
            return Ok(ApiResponse<List<string>>.SuccessResponse(requestTypes));
        }

        [HttpGet("status-codes")]
        [ResponseCache(Duration = 3600)]
        public async Task<ActionResult<ApiResponse<List<LookupItem>>>> GetStatusCodes()
        {
            Response.Headers.Append("Cache-Control", "public, max-age=3600");
            var statusCodes = await _lookupService.GetStatusCodesAsync();
            return Ok(ApiResponse<List<LookupItem>>.SuccessResponse(statusCodes));
        }
    }
}
