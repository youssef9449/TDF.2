using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using TDFShared.DTOs.Requests;
using TDFShared.DTOs.Common;

namespace TDFAPI.Services
{
    public interface IRequestService
    {
        // Updated to use int and return RequestResponseDto
        Task<RequestResponseDto> CreateAsync(RequestCreateDto createDto, int userId);
        Task<RequestResponseDto> UpdateAsync(int id, RequestUpdateDto updateDto, int userId);
        Task<RequestResponseDto> GetByIdAsync(int id);

        // Uses DTO for pagination, returns Paginated Response DTOs
        Task<PaginatedResult<RequestResponseDto>> GetByUserIdAsync(int userId, RequestPaginationDto pagination);
        Task<PaginatedResult<RequestResponseDto>> GetAllAsync(RequestPaginationDto pagination);
        Task<PaginatedResult<RequestResponseDto>> GetByDepartmentAsync(string department, RequestPaginationDto pagination);
        Task<PaginatedResult<RequestResponseDto>> GetRequestsForManagerAsync(int managerId, string department, RequestPaginationDto pagination);

        // Updated to use int
        Task<bool> DeleteAsync(int id, int userId);

        // Uses Approval/Reject DTOs for input
        Task<bool> ApproveRequestAsync(int id, RequestApprovalDto approvalDto, int approverId, string approverName, bool isHRApproval);
        Task<bool> RejectRequestAsync(int id, RequestRejectDto rejectDto, int rejecterId, string rejecterName, bool isHRRejection);

        // Balance/Utility methods (return types likely remain the same)
        Task<Dictionary<string, int>> GetLeaveBalancesAsync(int userId);
        Task<int> GetPermissionUsedAsync(int userId);
        Task<int> GetPendingDaysCountAsync(int userId, string requestType);
        Task<bool> HasConflictingRequestsAsync(int userId, DateTime startDate, DateTime endDate, int requestId = 0);
    }
}