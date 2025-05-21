using System;
using System.Collections.Generic; 
using System.Threading.Tasks;
using TDFShared.DTOs.Requests;
using TDFShared.DTOs.Common;
using Microsoft.Extensions.Logging;

namespace TDFMAUI.Services 
{
    public class RequestService : IRequestService
    {
        private readonly IApiService _apiService;
        private readonly ILogger<RequestService> _logger;

        public RequestService(IApiService apiService, ILogger<RequestService> logger)
        {
            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _logger.LogInformation("RequestService initialized, using IApiService.");
        }

        public async Task<RequestResponseDto> CreateRequestAsync(RequestCreateDto requestDto)
        {
            if (requestDto == null) throw new ArgumentNullException(nameof(requestDto));
            _logger.LogInformation("RequestService: Calling IApiService.CreateRequestAsync");
            try
            {
                return await _apiService.CreateRequestAsync(requestDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RequestService: Error in CreateRequestAsync: {Message}", ex.Message);
                throw; 
            }
        }

        public async Task<RequestResponseDto> UpdateRequestAsync(int requestId, RequestUpdateDto requestDto)
        {
            if (requestDto == null) throw new ArgumentNullException(nameof(requestDto));
            _logger.LogInformation("RequestService: Calling IApiService.UpdateRequestAsync for requestId {RequestId}", requestId);
            try
            {
                return await _apiService.UpdateRequestAsync(requestId, requestDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RequestService: Error in UpdateRequestAsync for requestId {RequestId}: {Message}", requestId, ex.Message);
                throw;
            }
        }

        public async Task<PaginatedResult<RequestResponseDto>> GetMyRequestsAsync(RequestPaginationDto pagination)
        {
            if (pagination == null) throw new ArgumentNullException(nameof(pagination));
            _logger.LogInformation("RequestService: Attempting to get current user ID for GetMyRequestsAsync.");
            try
            {
                int userId = await _apiService.GetCurrentUserIdAsync();
                if (userId == 0) 
                {
                    _logger.LogWarning("RequestService: GetMyRequestsAsync - Could not determine current user ID or user ID is 0. Returning empty result.");
                    return new PaginatedResult<RequestResponseDto> { Items = new List<RequestResponseDto>(), TotalCount = 0, PageNumber = pagination.Page, PageSize = pagination.PageSize };
                }
                _logger.LogInformation("RequestService: Calling IApiService.GetRequestsAsync for current user ID {UserId}", userId);
                return await _apiService.GetRequestsAsync(pagination, userId, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RequestService: Error in GetMyRequestsAsync: {Message}", ex.Message);
                throw;
            }
        }

        public async Task<PaginatedResult<RequestResponseDto>> GetAllRequestsAsync(RequestPaginationDto pagination)
        {
            if (pagination == null) throw new ArgumentNullException(nameof(pagination));
            _logger.LogInformation("RequestService: Calling IApiService.GetRequestsAsync for all requests");
            try
            {
                return await _apiService.GetRequestsAsync(pagination, null, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RequestService: Error in GetAllRequestsAsync: {Message}", ex.Message);
                throw;
            }
        }

        public async Task<PaginatedResult<RequestResponseDto>> GetRequestsByDepartmentAsync(string department, RequestPaginationDto pagination)
        {
            if (pagination == null) throw new ArgumentNullException(nameof(pagination));
            _logger.LogInformation("RequestService: Calling IApiService.GetRequestsAsync for department {Department}", department);
            try
            {
                return await _apiService.GetRequestsAsync(pagination, null, department);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RequestService: Error in GetRequestsByDepartmentAsync for department {Department}: {Message}", department, ex.Message);
                throw;
            }
        }

        public async Task<RequestResponseDto> GetRequestByIdAsync(int requestId)
        {
            _logger.LogInformation("RequestService: Calling IApiService.GetRequestByIdAsync for requestId {RequestId}", requestId);
            try
            {
                return await _apiService.GetRequestByIdAsync(requestId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RequestService: Error in GetRequestByIdAsync for requestId {RequestId}: {Message}", requestId, ex.Message);
                throw;
            }
        }

        public async Task<bool> DeleteRequestAsync(int requestId)
        {
            _logger.LogInformation("RequestService: Calling IApiService.DeleteRequestAsync for requestId {RequestId}", requestId);
            try
            {
                return await _apiService.DeleteRequestAsync(requestId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RequestService: Error in DeleteRequestAsync for requestId {RequestId}: {Message}", requestId, ex.Message);
                throw;
            }
        }

        public async Task<bool> ApproveRequestAsync(int requestId, RequestApprovalDto approvalDto)
        {
            if (approvalDto == null) throw new ArgumentNullException(nameof(approvalDto));
            _logger.LogInformation("RequestService: Calling IApiService.ApproveRequestAsync for requestId {RequestId}", requestId);
            try
            {
                return await _apiService.ApproveRequestAsync(requestId, approvalDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RequestService: Error in ApproveRequestAsync for requestId {RequestId}: {Message}", requestId, ex.Message);
                throw;
            }
        }

        public async Task<bool> RejectRequestAsync(int requestId, RequestRejectDto rejectDto)
        {
            if (rejectDto == null) throw new ArgumentNullException(nameof(rejectDto));
            _logger.LogInformation("RequestService: Calling IApiService.RejectRequestAsync for requestId {RequestId}", requestId);
            try
            {
                return await _apiService.RejectRequestAsync(requestId, rejectDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RequestService: Error in RejectRequestAsync for requestId {RequestId}: {Message}", requestId, ex.Message);
                throw;
            }
        }
    }
}