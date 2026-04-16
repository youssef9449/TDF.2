using TDFShared.Constants;
using TDFShared.DTOs.Common;
using TDFShared.DTOs.Requests;
using TDFShared.Services;
using Microsoft.Extensions.Logging;

namespace TDFMAUI.Services.Api
{
    public class RequestApiService : IRequestApiService
    {
        private readonly IHttpClientService _httpClientService;
        private readonly ILogger<RequestApiService> _logger;
        private readonly IConnectivityService _connectivityService;

        public RequestApiService(
            IHttpClientService httpClientService,
            ILogger<RequestApiService> logger,
            IConnectivityService connectivityService)
        {
            _httpClientService = httpClientService;
            _logger = logger;
            _connectivityService = connectivityService;
        }

        public async Task<ApiResponse<RequestResponseDto>> GetRequestByIdAsync(int requestId, bool queueIfUnavailable = true)
        {
            try
            {
                string endpoint = string.Format(ApiRoutes.Requests.GetById, requestId);
                var response = await _httpClientService.GetAsync<ApiResponse<RequestResponseDto>>(endpoint);
                return response ?? new ApiResponse<RequestResponseDto> { Success = false, Message = "Failed to get request" };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "RequestApiService: Error getting request {RequestId}: {Message}", requestId, ex.Message);
                return new ApiResponse<RequestResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<RequestResponseDto>> CreateRequestAsync(RequestCreateDto requestDto)
        {
            try
            {
                var response = await _httpClientService.PostAsync<RequestCreateDto, ApiResponse<RequestResponseDto>>(ApiRoutes.Requests.Base, requestDto);
                return response ?? new ApiResponse<RequestResponseDto> { Success = false, Message = "Failed to create request" };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "RequestApiService: Error creating request: {Message}", ex.Message);
                return new ApiResponse<RequestResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<RequestResponseDto>> UpdateRequestAsync(int requestId, RequestUpdateDto requestDto)
        {
            try
            {
                string endpoint = string.Format(ApiRoutes.Requests.Update, requestId);
                var response = await _httpClientService.PutAsync<RequestUpdateDto, ApiResponse<RequestResponseDto>>(endpoint, requestDto);
                return response ?? new ApiResponse<RequestResponseDto> { Success = false, Message = "Failed to update request" };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "RequestApiService: Error updating request {RequestId}: {Message}", requestId, ex.Message);
                return new ApiResponse<RequestResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<bool>> DeleteRequestAsync(int requestId)
        {
            try
            {
                string endpoint = string.Format(ApiRoutes.Requests.Delete, requestId);
                var response = await _httpClientService.DeleteAsync(endpoint);
                return new ApiResponse<bool> { Success = response.IsSuccessStatusCode, Data = response.IsSuccessStatusCode };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "RequestApiService: Error deleting request {RequestId}: {Message}", requestId, ex.Message);
                return new ApiResponse<bool> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<bool>> ManagerApproveRequestAsync(int requestId, ManagerApprovalDto approvalDto)
        {
            try
            {
                string endpoint = string.Format(ApiRoutes.Requests.ManagerApprove, requestId);
                var response = await _httpClientService.PostAsync<ManagerApprovalDto, ApiResponse<bool>>(endpoint, approvalDto);
                return response ?? new ApiResponse<bool> { Success = false, Message = "Failed to approve request" };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "RequestApiService: Error approving request {RequestId} as manager: {Message}", requestId, ex.Message);
                return new ApiResponse<bool> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<bool>> HRApproveRequestAsync(int requestId, HRApprovalDto approvalDto)
        {
            try
            {
                string endpoint = string.Format(ApiRoutes.Requests.HRApprove, requestId);
                var response = await _httpClientService.PostAsync<HRApprovalDto, ApiResponse<bool>>(endpoint, approvalDto);
                return response ?? new ApiResponse<bool> { Success = false, Message = "Failed to approve request" };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "RequestApiService: Error approving request {RequestId} as HR: {Message}", requestId, ex.Message);
                return new ApiResponse<bool> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<bool>> ManagerRejectRequestAsync(int requestId, ManagerRejectDto rejectDto)
        {
            try
            {
                string endpoint = string.Format(ApiRoutes.Requests.ManagerReject, requestId);
                var response = await _httpClientService.PostAsync<ManagerRejectDto, ApiResponse<bool>>(endpoint, rejectDto);
                return response ?? new ApiResponse<bool> { Success = false, Message = "Failed to reject request" };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "RequestApiService: Error rejecting request {RequestId} as manager: {Message}", requestId, ex.Message);
                return new ApiResponse<bool> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<bool>> HRRejectRequestAsync(int requestId, HRRejectDto rejectDto)
        {
            try
            {
                string endpoint = string.Format(ApiRoutes.Requests.HRReject, requestId);
                var response = await _httpClientService.PostAsync<HRRejectDto, ApiResponse<bool>>(endpoint, rejectDto);
                return response ?? new ApiResponse<bool> { Success = false, Message = "Failed to reject request" };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "RequestApiService: Error rejecting request {RequestId} as HR: {Message}", requestId, ex.Message);
                return new ApiResponse<bool> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<PaginatedResult<RequestResponseDto>>> GetRequestsAsync(RequestPaginationDto pagination, int? userId = null, string? department = null, bool queueIfUnavailable = true)
        {
            try
            {
                string endpoint = ApiRoutes.Requests.Base;
                var queryParams = new List<string>();
                if (userId.HasValue) queryParams.Add($"userId={userId.Value}");
                if (!string.IsNullOrEmpty(department)) queryParams.Add($"department={Uri.EscapeDataString(department)}");
                if (pagination != null)
                {
                    queryParams.Add($"page={pagination.Page}");
                    queryParams.Add($"pageSize={pagination.PageSize}");
                    if (!string.IsNullOrEmpty(pagination.SortBy)) queryParams.Add($"sortBy={Uri.EscapeDataString(pagination.SortBy)}");
                    queryParams.Add($"ascending={pagination.Ascending}");
                    if (pagination.FilterStatus.HasValue) queryParams.Add($"filterStatus={pagination.FilterStatus.Value}");
                }
                if (queryParams.Any()) endpoint += "?" + string.Join("&", queryParams);

                var response = await _httpClientService.GetAsync<ApiResponse<PaginatedResult<RequestResponseDto>>>(endpoint);
                return response ?? new ApiResponse<PaginatedResult<RequestResponseDto>> { Success = false, Message = "Failed to get requests" };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "RequestApiService: Error getting requests: {Message}", ex.Message);
                return new ApiResponse<PaginatedResult<RequestResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<PaginatedResult<RequestResponseDto>>> GetRequestsForApprovalAsync(RequestPaginationDto pagination, bool queueIfUnavailable = true)
        {
            try
            {
                string endpoint = ApiRoutes.Requests.GetForApproval;
                var queryParams = new List<string>();
                if (pagination != null)
                {
                    queryParams.Add($"page={pagination.Page}");
                    queryParams.Add($"pageSize={pagination.PageSize}");
                    if (pagination.FilterStatus.HasValue) queryParams.Add($"filterStatus={pagination.FilterStatus.Value}");
                }
                if (queryParams.Any()) endpoint += "?" + string.Join("&", queryParams);

                var response = await _httpClientService.GetAsync<ApiResponse<PaginatedResult<RequestResponseDto>>>(endpoint);
                return response ?? new ApiResponse<PaginatedResult<RequestResponseDto>> { Success = false, Message = "Failed to get requests for approval" };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "RequestApiService: Error getting requests for approval: {Message}", ex.Message);
                return new ApiResponse<PaginatedResult<RequestResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<Dictionary<string, int>>> GetLeaveBalancesAsync(int userId, bool queueIfUnavailable = true)
        {
            try
            {
                string endpoint = string.Format(ApiRoutes.Requests.GetUserBalances, userId);
                var response = await _httpClientService.GetAsync<ApiResponse<Dictionary<string, int>>>(endpoint);
                return response ?? new ApiResponse<Dictionary<string, int>> { Success = false, Message = "Failed to get leave balances" };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "RequestApiService: Error getting leave balances: {Message}", ex.Message);
                return new ApiResponse<Dictionary<string, int>> { Success = false, Message = ex.Message };
            }
        }
    }
}
