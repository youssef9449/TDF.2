using Microsoft.Extensions.Logging;
using TDFShared.Constants;
using TDFShared.DTOs.Common;
using TDFShared.Services;

namespace TDFMAUI.Services.Api
{
    public class LookupApiService : ILookupApiService
    {
        private readonly IHttpClientService _httpClientService;
        private readonly ILogger<LookupApiService> _logger;

        public LookupApiService(
            IHttpClientService httpClientService,
            ILogger<LookupApiService> logger)
        {
            _httpClientService = httpClientService;
            _logger = logger;
        }

        public async Task<ApiResponse<List<LookupItem>>> GetDepartmentsAsync(bool queueIfUnavailable = true)
        {
            try
            {
                var response = await _httpClientService.GetAsync<ApiResponse<List<LookupItem>>>(ApiRoutes.Lookups.GetDepartments);
                return response ?? new ApiResponse<List<LookupItem>> { Success = false, Message = "Failed to get departments" };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "LookupApiService: Error getting departments: {Message}", ex.Message);
                return new ApiResponse<List<LookupItem>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<List<LookupItem>> GetLeaveTypesAsync(bool queueIfUnavailable = true)
        {
            try
            {
                var response = await _httpClientService.GetAsync<ApiResponse<List<LookupItem>>>(ApiRoutes.Lookups.GetLeaveTypes);
                return response?.Data ?? new List<LookupItem>();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "LookupApiService: Error getting leave types: {Message}", ex.Message);
                return new List<LookupItem>();
            }
        }
    }
}
