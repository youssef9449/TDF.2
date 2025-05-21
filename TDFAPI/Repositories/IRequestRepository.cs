using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TDFShared.Models.Request;
using TDFShared.DTOs.Requests;
using TDFShared.DTOs.Common;
using TDFShared.Enums;

namespace TDFAPI.Repositories
{
    public interface IRequestRepository
    {
        Task<RequestEntity> GetByIdAsync(int requestId);
        Task<IEnumerable<RequestEntity>> GetAllAsync();
        Task<PaginatedResult<RequestEntity>> GetAllAsync(RequestPaginationDto pagination);
        Task<IEnumerable<RequestEntity>> GetByUserIdAsync(int userId);
        Task<PaginatedResult<RequestEntity>> GetByUserIdAsync(int userId, RequestPaginationDto pagination);
        Task<IEnumerable<RequestEntity>> GetByDepartmentAsync(string department);
        Task<PaginatedResult<RequestEntity>> GetByDepartmentAsync(string department, RequestPaginationDto pagination);
        Task<int> CreateAsync(RequestEntity request);
        Task<bool> UpdateAsync(RequestEntity request);
        Task<bool> DeleteAsync(int requestId);
        Task<Dictionary<string, int>> GetLeaveBalancesAsync(int userId);
        Task<bool> UpdateLeaveBalanceAsync(int userId, LeaveType leaveType, int days, bool isAdding);
        Task<int> GetPermissionUsedAsync(int userId);
        Task<int> GetPendingDaysCountAsync(int userId, LeaveType requestType);
        Task<bool> HasConflictingRequestsAsync(int userId, DateTime startDate, DateTime endDate, int requestId = 0);
    }
}