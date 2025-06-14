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
        Task<bool> ManagerApproveRequestAsync(int id, ManagerApprovalDto approvalDto, int approverId, string approverName);
        Task<bool> HRApproveRequestAsync(int id, HRApprovalDto approvalDto, int approverId, string approverName);
        Task<bool> ManagerRejectRequestAsync(int id, ManagerRejectDto rejectDto, int rejecterId, string rejecterName);
        Task<bool> HRRejectRequestAsync(int id, HRRejectDto rejectDto, int rejecterId, string rejecterName);

        // Remove obsolete generic approval/reject methods
        // Task<bool> ApproveRequestAsync(int id, RequestApprovalDto approvalDto, int approverId, bool isHRApproval);
        // Task<bool> RejectRequestAsync(int id, RequestRejectDto rejectDto, int rejecterId, bool isHRRejection);

        // Balance/Utility methods (return types likely remain the same)
        Task<Dictionary<string, int>> GetLeaveBalancesAsync(int userId);
        Task<int> GetPermissionUsedAsync(int userId);
        Task<int> GetPendingDaysCountAsync(int userId, string requestType);
        Task<bool> HasConflictingRequestsAsync(int userId, DateTime startDate, DateTime endDate, int requestId = 0);
        
        // Dashboard methods
        Task<List<RequestResponseDto>> GetPendingRequestsByUserIdAsync(int userId);
        Task<List<RequestResponseDto>> GetPendingRequestsByDepartmentAsync(string department);
        Task<List<RequestResponseDto>> GetAllPendingRequestsAsync();
    }
}