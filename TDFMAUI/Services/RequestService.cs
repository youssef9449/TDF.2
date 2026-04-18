using System;
using System.Collections.Generic; 
using System.Threading.Tasks;
using TDFShared.DTOs.Requests;
using TDFShared.DTOs.Common;
using Microsoft.Extensions.Logging;
using TDFShared.Services;
using TDFShared.Contracts;

namespace TDFMAUI.Services 
{
    public class RequestService : IRequestService
    {
        private readonly IRequestApiService _requestApiService;
        private readonly ILogger<RequestService> _logger;
        private readonly IAuthClient _authService;

        public RequestService(
            IRequestApiService requestApiService,
            ILogger<RequestService> logger,
            IAuthClient authService)
        {
            _requestApiService = requestApiService ?? throw new ArgumentNullException(nameof(requestApiService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _authService = authService;
            _logger.LogInformation("RequestService initialized.");
        }

        public async Task<ApiResponse<RequestResponseDto>> CreateRequestAsync(RequestCreateDto requestDto)
        {
            return await _requestApiService.CreateRequestAsync(requestDto);
        }

        public async Task<ApiResponse<RequestResponseDto>> UpdateRequestAsync(int requestId, RequestUpdateDto requestDto)
        {
            return await _requestApiService.UpdateRequestAsync(requestId, requestDto);
        }

        public async Task<ApiResponse<PaginatedResult<RequestResponseDto>>> GetMyRequestsAsync(RequestPaginationDto pagination)
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            if (userId == 0)
            {
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
            return await _requestApiService.GetRequestsAsync(pagination, userId);
        }

        public async Task<ApiResponse<PaginatedResult<RequestResponseDto>>> GetAllRequestsAsync(RequestPaginationDto pagination)
        {
            return await _requestApiService.GetRequestsAsync(pagination);
        }

        public async Task<ApiResponse<PaginatedResult<RequestResponseDto>>> GetRequestsByDepartmentAsync(string department, RequestPaginationDto pagination)
        {
            return await _requestApiService.GetRequestsAsync(pagination, null, department);
        }

        public async Task<ApiResponse<RequestResponseDto>> GetRequestByIdAsync(int requestId)
        {
            return await _requestApiService.GetRequestByIdAsync(requestId);
        }

        public async Task<ApiResponse<bool>> DeleteRequestAsync(int requestId)
        {
            return await _requestApiService.DeleteRequestAsync(requestId);
        }

        public async Task<ApiResponse<RequestResponseDto>> ManagerApproveRequestAsync(int requestId, ManagerApprovalDto approvalDto)
        {
            var response = await _requestApiService.ManagerApproveRequestAsync(requestId, approvalDto);
            if (response.Success)
            {
                return await GetRequestByIdAsync(requestId);
            }
            return new ApiResponse<RequestResponseDto> { Success = false, Message = response.Message };
        }

        public async Task<ApiResponse<RequestResponseDto>> HRApproveRequestAsync(int requestId, HRApprovalDto approvalDto)
        {
            var response = await _requestApiService.HRApproveRequestAsync(requestId, approvalDto);
            if (response.Success)
            {
                return await GetRequestByIdAsync(requestId);
            }
            return new ApiResponse<RequestResponseDto> { Success = false, Message = response.Message };
        }

        public async Task<ApiResponse<RequestResponseDto>> ManagerRejectRequestAsync(int requestId, ManagerRejectDto rejectDto)
        {
            var response = await _requestApiService.ManagerRejectRequestAsync(requestId, rejectDto);
            if (response.Success)
            {
                return await GetRequestByIdAsync(requestId);
            }
            return new ApiResponse<RequestResponseDto> { Success = false, Message = response.Message };
        }

        public async Task<ApiResponse<RequestResponseDto>> HRRejectRequestAsync(int requestId, HRRejectDto rejectDto)
        {
            var response = await _requestApiService.HRRejectRequestAsync(requestId, rejectDto);
            if (response.Success)
            {
                return await GetRequestByIdAsync(requestId);
            }
            return new ApiResponse<RequestResponseDto> { Success = false, Message = response.Message };
        }

        public async Task<ApiResponse<PaginatedResult<RequestResponseDto>>> GetRequestsForApprovalAsync(
            int pageNumber = 1,
            int pageSize = 10,
            string? status = null,
            string? type = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string? department = null,
            int? userId = null)
        {
            var pagination = new RequestPaginationDto
            {
                Page = pageNumber,
                PageSize = pageSize,
                Department = department,
                UserId = userId,
                FromDate = fromDate,
                ToDate = toDate
            };

            if (!string.IsNullOrEmpty(status) && status != "All" && Enum.TryParse<TDFShared.Enums.RequestStatus>(status, true, out var parsedStatus))
                pagination.FilterStatus = parsedStatus;

            if (!string.IsNullOrEmpty(type) && type != "All" && Enum.TryParse<TDFShared.Enums.LeaveType>(type.Replace(" ", ""), true, out var parsedType))
                pagination.FilterType = parsedType;

            return await _requestApiService.GetRequestsForApprovalAsync(pagination);
        }

        public async Task<List<RequestResponseDto>> GetRecentDashboardRequestsAsync()
        {
            var pagination = new RequestPaginationDto { Page = 1, PageSize = 5, SortBy = "CreatedDate", Ascending = false };
            var response = await _requestApiService.GetRequestsAsync(pagination);
            return response.Data?.Items?.ToList() ?? new List<RequestResponseDto>();
        }

        public async Task<int> GetPendingDashboardRequestCountAsync()
        {
            var pagination = new RequestPaginationDto { CountOnly = true, FilterStatus = TDFShared.Enums.RequestStatus.Pending };
            var response = await _requestApiService.GetRequestsAsync(pagination);
            return response.Data?.TotalCount ?? 0;
        }
    }
}
