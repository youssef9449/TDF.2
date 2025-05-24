using System;
using System.Collections.Generic; 
using System.Threading.Tasks;
using TDFShared.DTOs.Requests;
using TDFShared.DTOs.Common;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using TDFShared.Services;

namespace TDFMAUI.Services 
{
    public class RequestService : IRequestService
    {
        private readonly IApiService _apiService;
        private readonly ILogger<RequestService> _logger;
        private readonly HttpClient _httpClient;
        private readonly IAuthService _authService;

        public RequestService(IApiService apiService, ILogger<RequestService> logger, HttpClient httpClient, IAuthService authService)
        {
            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClient = httpClient;
            _authService = authService;
            _logger.LogInformation("RequestService initialized, using IApiService.");
        }

        public async Task<ApiResponse<RequestResponseDto>> CreateRequestAsync(RequestCreateDto requestDto)
        {
            if (requestDto == null) throw new ArgumentNullException(nameof(requestDto));
            _logger.LogInformation("RequestService: Calling IApiService.CreateRequestAsync");
            try
            {
                var response = await _apiService.CreateRequestAsync(requestDto);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RequestService: Error in CreateRequestAsync: {Message}", ex.Message);
                return new ApiResponse<RequestResponseDto>
                {
                    Success = false,
                    Message = ex.Message
                };
            }
        }

        public async Task<ApiResponse<RequestResponseDto>> UpdateRequestAsync(int requestId, RequestUpdateDto requestDto)
        {
            if (requestDto == null) throw new ArgumentNullException(nameof(requestDto));
            _logger.LogInformation("RequestService: Calling IApiService.UpdateRequestAsync for requestId {RequestId}", requestId);
            try
            {
                var response = await _apiService.UpdateRequestAsync(requestId, requestDto);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RequestService: Error in UpdateRequestAsync for requestId {RequestId}: {Message}", requestId, ex.Message);
                return new ApiResponse<RequestResponseDto>
                {
                    Success = false,
                    Message = ex.Message
                };
            }
        }

        public async Task<ApiResponse<PaginatedResult<RequestResponseDto>>> GetMyRequestsAsync(RequestPaginationDto pagination)
        {
            if (pagination == null) throw new ArgumentNullException(nameof(pagination));
            _logger.LogInformation("RequestService: Attempting to get current user ID for GetMyRequestsAsync.");
            try
            {
                var userIdResponse = await _apiService.GetCurrentUserIdAsync();
                if (!userIdResponse.Success || userIdResponse.Data == 0)
                {
                    _logger.LogWarning("RequestService: GetMyRequestsAsync - Could not determine current user ID or user ID is 0. Returning empty result.");
                    return new ApiResponse<PaginatedResult<RequestResponseDto>>
                    {
                        Success = true,
                        Data = new PaginatedResult<RequestResponseDto> 
                        { 
                            Items = new List<RequestResponseDto>(), 
                            TotalCount = 0, 
                            PageNumber = pagination.Page, 
                            PageSize = pagination.PageSize 
                        }
                    };
                }
                _logger.LogInformation("RequestService: Calling IApiService.GetRequestsAsync for current user ID {UserId}", userIdResponse.Data);
                var response = await _apiService.GetRequestsAsync(pagination, userIdResponse.Data, null);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RequestService: Error in GetMyRequestsAsync: {Message}", ex.Message);
                return new ApiResponse<PaginatedResult<RequestResponseDto>>
                {
                    Success = false,
                    Message = ex.Message
                };
            }
        }

        public async Task<ApiResponse<PaginatedResult<RequestResponseDto>>> GetAllRequestsAsync(RequestPaginationDto pagination)
        {
            if (pagination == null) throw new ArgumentNullException(nameof(pagination));
            _logger.LogInformation("RequestService: Calling IApiService.GetRequestsAsync for all requests");
            try
            {
                var response = await _apiService.GetRequestsAsync(pagination, null, null);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RequestService: Error in GetAllRequestsAsync: {Message}", ex.Message);
                return new ApiResponse<PaginatedResult<RequestResponseDto>>
                {
                    Success = false,
                    Message = ex.Message
                };
            }
        }

        public async Task<ApiResponse<PaginatedResult<RequestResponseDto>>> GetRequestsByDepartmentAsync(string department, RequestPaginationDto pagination)
        {
            if (pagination == null) throw new ArgumentNullException(nameof(pagination));
            _logger.LogInformation("RequestService: Calling IApiService.GetRequestsAsync for department {Department}", department);
            try
            {
                var response = await _apiService.GetRequestsAsync(pagination, null, department);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RequestService: Error in GetRequestsByDepartmentAsync for department {Department}: {Message}", department, ex.Message);
                return new ApiResponse<PaginatedResult<RequestResponseDto>>
                {
                    Success = false,
                    Message = ex.Message
                };
            }
        }

        public async Task<ApiResponse<RequestResponseDto>> GetRequestByIdAsync(int requestId)
        {
            try
            {
                var response = await _apiService.GetRequestByIdAsync(requestId);
                return response;
            }
            catch (Exception ex)
            {
                return new ApiResponse<RequestResponseDto>
                {
                    Success = false,
                    Message = $"Error: {ex.Message}"
                };
            }
        }

        public async Task<ApiResponse<bool>> DeleteRequestAsync(int requestId)
        {
            _logger.LogInformation("RequestService: Calling IApiService.DeleteRequestAsync for requestId {RequestId}", requestId);
            try
            {
                var response = await _apiService.DeleteRequestAsync(requestId);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RequestService: Error in DeleteRequestAsync for requestId {RequestId}: {Message}", requestId, ex.Message);
                return new ApiResponse<bool>
                {
                    Success = false,
                    Message = ex.Message
                };
            }
        }

        public async Task<ApiResponse<RequestResponseDto>> ApproveRequestAsync(int requestId, string comments = null)
        {
            try
            {
                var approvalDto = new RequestApprovalDto 
                { 
                    Comment = comments,
                    ManagerRemarks = comments
                };
                var response = await _apiService.ApproveRequestAsync(requestId, approvalDto);
                if (response.Success)
                {
                    return new ApiResponse<RequestResponseDto> { Success = true, Message = response.Message };
                }
                return new ApiResponse<RequestResponseDto> { Success = false, Message = response.Message };
            }
            catch (Exception ex)
            {
                return new ApiResponse<RequestResponseDto>
                {
                    Success = false,
                    Message = $"Error: {ex.Message}"
                };
            }
        }

        public async Task<ApiResponse<RequestResponseDto>> RejectRequestAsync(int requestId, string comments = null)
        {
            try
            {
                var rejectDto = new RequestRejectDto { RejectReason = comments ?? "No reason provided" };
                var response = await _apiService.RejectRequestAsync(requestId, rejectDto);
                if (response.Success)
                {
                    return new ApiResponse<RequestResponseDto> { Success = true, Message = response.Message };
                }
                return new ApiResponse<RequestResponseDto> { Success = false, Message = response.Message };
            }
            catch (Exception ex)
            {
                return new ApiResponse<RequestResponseDto>
                {
                    Success = false,
                    Message = $"Error: {ex.Message}"
                };
            }
        }

        public async Task<ApiResponse<PaginatedResult<RequestResponseDto>>> GetRequestsForApprovalAsync(
            int pageNumber = 1,
            int pageSize = 10,
            string status = null,
            string type = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string department = null)
        {
            try
            {
                var queryParams = new List<string>
                {
                    $"pageNumber={pageNumber}",
                    $"pageSize={pageSize}"
                };

                if (!string.IsNullOrEmpty(status) && status != "All")
                    queryParams.Add($"status={status}");
                if (!string.IsNullOrEmpty(type) && type != "All")
                    queryParams.Add($"type={type}");
                if (fromDate.HasValue)
                    queryParams.Add($"fromDate={fromDate.Value:yyyy-MM-dd}");
                if (toDate.HasValue)
                    queryParams.Add($"toDate={toDate.Value:yyyy-MM-dd}");
                if (!string.IsNullOrEmpty(department))
                    queryParams.Add($"department={Uri.EscapeDataString(department)}");

                var queryString = string.Join("&", queryParams);
                var response = await _httpClient.GetFromJsonAsync<ApiResponse<PaginatedResult<RequestResponseDto>>>($"api/requests/approval?{queryString}");

                return response ?? new ApiResponse<PaginatedResult<RequestResponseDto>>
                {
                    Success = false,
                    Message = "Failed to get requests"
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<PaginatedResult<RequestResponseDto>>
                {
                    Success = false,
                    Message = $"Error: {ex.Message}"
                };
            }
        }
    }
}