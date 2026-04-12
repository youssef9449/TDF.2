using System.Collections.Generic;
using System.Threading.Tasks;
using TDFShared.DTOs.Common;
using TDFShared.DTOs.Requests;

namespace TDFMAUI.Services
{
    public interface IRequestApiService
    {
        Task<ApiResponse<RequestResponseDto>> GetRequestByIdAsync(int requestId, bool queueIfUnavailable = true);
        Task<ApiResponse<RequestResponseDto>> CreateRequestAsync(RequestCreateDto requestDto);
        Task<ApiResponse<RequestResponseDto>> UpdateRequestAsync(int requestId, RequestUpdateDto requestDto);
        Task<ApiResponse<bool>> DeleteRequestAsync(int requestId);
        Task<ApiResponse<bool>> ManagerApproveRequestAsync(int requestId, ManagerApprovalDto approvalDto);
        Task<ApiResponse<bool>> HRApproveRequestAsync(int requestId, HRApprovalDto approvalDto);
        Task<ApiResponse<bool>> ManagerRejectRequestAsync(int requestId, ManagerRejectDto rejectDto);
        Task<ApiResponse<bool>> HRRejectRequestAsync(int requestId, HRRejectDto rejectDto);
        Task<ApiResponse<PaginatedResult<RequestResponseDto>>> GetRequestsAsync(RequestPaginationDto pagination, int? userId = null, string department = null, bool queueIfUnavailable = true);
        Task<ApiResponse<PaginatedResult<RequestResponseDto>>> GetRequestsForApprovalAsync(RequestPaginationDto pagination, bool queueIfUnavailable = true);
        Task<ApiResponse<Dictionary<string, int>>> GetLeaveBalancesAsync(int userId, bool queueIfUnavailable = true);
    }
}
