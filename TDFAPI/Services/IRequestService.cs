using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using TDFShared.DTOs.Requests;
using TDFShared.DTOs.Common;

namespace TDFAPI.Services
{
    public interface IRequestService
    {
        // Updated to use Guid and return RequestResponseDto
        Task<RequestResponseDto> CreateAsync(RequestCreateDto createDto, int userId);
        Task<RequestResponseDto> UpdateAsync(Guid id, RequestUpdateDto updateDto, int userId);
        Task<RequestResponseDto> GetByIdAsync(Guid id);
        
        // Uses DTO for pagination, returns Paginated Response DTOs
        Task<PaginatedResult<RequestResponseDto>> GetByUserIdAsync(int userId, RequestPaginationDto pagination);
        Task<PaginatedResult<RequestResponseDto>> GetAllAsync(RequestPaginationDto pagination);
        Task<PaginatedResult<RequestResponseDto>> GetByDepartmentAsync(string department, RequestPaginationDto pagination);
        
        // Updated to use Guid
        Task<bool> DeleteAsync(Guid id, int userId);
        
        // Uses Approval/Reject DTOs for input
        Task<bool> ApproveRequestAsync(Guid id, RequestApprovalDto approvalDto, int approverId, string approverName, bool isHRApproval);
        Task<bool> RejectRequestAsync(Guid id, RequestRejectDto rejectDto, int rejecterId, string rejecterName, bool isHRRejection);
        
        // Balance/Utility methods (return types likely remain the same)
        Task<Dictionary<string, int>> GetLeaveBalancesAsync(int userId);
        Task<int> GetPermissionUsedAsync(int userId);
        Task<int> GetPendingDaysCountAsync(int userId, string requestType);
        Task<bool> HasConflictingRequestsAsync(int userId, DateTime startDate, DateTime endDate, Guid requestId = default);
    }
} 