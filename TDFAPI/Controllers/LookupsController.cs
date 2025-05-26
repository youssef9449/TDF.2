using Microsoft.AspNetCore.Mvc;
using TDFAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using TDFShared.DTOs.Common;
using Microsoft.AspNetCore.RateLimiting;
using TDFShared.Constants;

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
        [Route(ApiRoutes.Lookups.GetAll)]
        [ResponseCache(Duration = 3600)] // Cache for 1 hour
        public async Task<ActionResult<ApiResponse<LookupResponseDto>>> GetAllLookups()
        {
            try
            {
                _logger.LogInformation("Getting all lookup data");
                Response.Headers.Append("Cache-Control", "public, max-age=3600");
                var allLookups = await _lookupService.GetAllLookupsAsync();
                return Ok(ApiResponse<LookupResponseDto>.SuccessResponse(allLookups));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all lookup data: {Message}", ex.Message);
                return StatusCode(500, ApiResponse<LookupResponseDto>.ErrorResponse("An error occurred retrieving lookup data"));
            }
        }

        [HttpGet("departments")]
        [Route(ApiRoutes.Lookups.GetDepartments)]
        [ResponseCache(Duration = 3600)] // Cache for 1 hour
        public async Task<ActionResult<ApiResponse<List<LookupItem>>>> GetDepartments()
        {
            _logger.LogInformation("DIAGNOSTIC: GetDepartments endpoint called at {Time}", DateTime.UtcNow);

            try
            {
                Response.Headers.Append("Cache-Control", "public, max-age=3600");
                var departments = await _lookupService.GetDepartmentsAsync();
                _logger.LogInformation("DIAGNOSTIC: GetDepartments returning {Count} departments", departments?.Count ?? 0);

                // Ensure we never pass null to SuccessResponse
                departments ??= new List<LookupItem>();

                var response = ApiResponse<List<LookupItem>>.SuccessResponse(departments);
                
                // Log the actual JSON that will be returned
                var jsonOptions = new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                };
                var jsonResponse = System.Text.Json.JsonSerializer.Serialize(response, jsonOptions);
                _logger.LogInformation("DIAGNOSTIC: JSON Response: {JsonResponse}", jsonResponse);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving departments: {Message}", ex.Message);
                return StatusCode(500, ApiResponse<List<LookupItem>>.ErrorResponse("An error occurred retrieving departments"));
            }
        }

        [HttpGet("titles/{department}")]
        public async Task<ActionResult<ApiResponse<List<string>>>> GetTitlesByDepartment(string department)
        {
            try
            {
                var titles = await _lookupService.GetTitlesByDepartmentAsync(department);
                return Ok(ApiResponse<List<string>>.SuccessResponse(titles));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving titles for department {Department}", department);
                return StatusCode(500, ApiResponse<List<string>>.ErrorResponse($"Error retrieving titles: {ex.Message}"));
            }
        }

        [HttpGet("leave-types")]
        [ResponseCache(Duration = 3600)] // Cache for 1 hour
        public async Task<ActionResult<ApiResponse<List<LookupItem>>>> GetLeaveTypes()
        {
            try
            {
                Response.Headers.Append("Cache-Control", "public, max-age=3600");
                var leaveTypes = await _lookupService.GetLeaveTypesAsync();
                return Ok(ApiResponse<List<LookupItem>>.SuccessResponse(leaveTypes));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving leave types: {Message}", ex.Message);
                return StatusCode(500, ApiResponse<List<LookupItem>>.ErrorResponse("An error occurred retrieving leave types"));
            }
        }

        [HttpGet("requesttypes")]
        [ResponseCache(Duration = 3600)] // Cache for 1 hour
        public async Task<ActionResult<ApiResponse<List<string>>>> GetRequestTypes()
        {
            try
            {
                Response.Headers.Append("Cache-Control", "public, max-age=3600");
                var requestTypes = await _lookupService.GetRequestTypesAsync();
                return Ok(ApiResponse<List<string>>.SuccessResponse(requestTypes));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving request types: {Message}", ex.Message);
                return StatusCode(500, ApiResponse<List<string>>.ErrorResponse("An error occurred retrieving request types"));
            }
        }

        [HttpGet("status-codes")]
        [ResponseCache(Duration = 3600)] // Cache for 1 hour
        public async Task<ActionResult<ApiResponse<List<LookupItem>>>> GetStatusCodes()
        {
            try
            {
                Response.Headers.Append("Cache-Control", "public, max-age=3600");
                var statusCodes = await _lookupService.GetStatusCodesAsync();
                return Ok(ApiResponse<List<LookupItem>>.SuccessResponse(statusCodes));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving status codes: {Message}", ex.Message);
                return StatusCode(500, ApiResponse<List<LookupItem>>.ErrorResponse("An error occurred retrieving status codes"));
            }
        }
    }
}