using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using TDFShared.DTOs.Requests;
using TDFShared.DTOs.Common;

namespace TDFMAUI.Services;

public interface IRequestService
{
    Task<ApiResponse<PaginatedResult<RequestResponseDto>>> GetRequestsForApprovalAsync(
        int pageNumber = 1,
        int pageSize = 10,
        string? status = null,
        string? type = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? department = null);

    Task<ApiResponse<RequestResponseDto>> GetRequestByIdAsync(int requestId);
    Task<ApiResponse<RequestResponseDto>> CreateRequestAsync(RequestCreateDto requestDto);
    Task<ApiResponse<RequestResponseDto>> UpdateRequestAsync(int requestId, RequestUpdateDto requestDto);
    Task<ApiResponse<PaginatedResult<RequestResponseDto>>> GetMyRequestsAsync(RequestPaginationDto pagination);
    Task<ApiResponse<PaginatedResult<RequestResponseDto>>> GetAllRequestsAsync(RequestPaginationDto pagination);
    Task<ApiResponse<PaginatedResult<RequestResponseDto>>> GetRequestsByDepartmentAsync(string department, RequestPaginationDto pagination);
    Task<ApiResponse<bool>> DeleteRequestAsync(int requestId);
    Task<ApiResponse<RequestResponseDto>> ManagerApproveRequestAsync(int requestId, ManagerApprovalDto approvalDto);
    Task<ApiResponse<RequestResponseDto>> HRApproveRequestAsync(int requestId, HRApprovalDto approvalDto);
    Task<ApiResponse<RequestResponseDto>> ManagerRejectRequestAsync(int requestId, ManagerRejectDto rejectDto);
    Task<ApiResponse<RequestResponseDto>> HRRejectRequestAsync(int requestId, HRRejectDto rejectDto);
    Task<List<RequestResponseDto>> GetRecentDashboardRequestsAsync();
    Task<int> GetPendingDashboardRequestCountAsync();
}