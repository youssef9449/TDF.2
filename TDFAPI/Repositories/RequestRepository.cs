using System;
using Microsoft.EntityFrameworkCore;
using TDFAPI.Data;
using TDFShared.Models.Request;
using TDFShared.DTOs.Common;
using TDFShared.DTOs.Requests;
using TDFShared.Models.User; // Required for dynamic sortin
using TDFShared.DTOs.Users;
using TDFShared.Enums;
using TDFAPI.Services;
using TDFShared.Services;

namespace TDFAPI.Repositories
{
    public class RequestRepository : IRequestRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<RequestRepository> _logger;
        private readonly IRoleService _roleService;

        public RequestRepository(ApplicationDbContext context, ILogger<RequestRepository> logger, IRoleService roleService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _roleService = roleService ?? throw new ArgumentNullException(nameof(roleService));
        }

        public async Task<RequestEntity> GetByIdAsync(int requestId)
        {
            try
            {
                var request = await _context.Requests
                    .Include(r => r.User)
                    .ThenInclude(u => u.AnnualLeave)
                    .FirstOrDefaultAsync(r => r.RequestID == requestId);

                if (request?.User != null)
                {
                    request.UserDto = MapUserToDto(request.User);
                }

                return request;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting request {RequestId}", requestId);
                throw;
            }
        }

        public async Task<IEnumerable<RequestEntity>> GetAllAsync()
        {
            var requests = await _context.Requests
                .Include(r => r.User)
                .ThenInclude(u => u.AnnualLeave)
                .OrderByDescending(r => r.RequestFromDay)
                .ToListAsync();

            // Map User entities to UserDto for responses
            foreach (var request in requests.Where(r => r.User != null))
            {
                request.UserDto = MapUserToDto(request.User);
            }

            return requests;
        }

        public async Task<PaginatedResult<RequestEntity>> GetAllAsync(RequestPaginationDto pagination
        )
        {
            var baseQuery = _context.Requests
                .Include(r => r.User)
                .ThenInclude(u => u.AnnualLeave)
                .AsQueryable();
            return await ExecutePaginatedQueryAsync(baseQuery, pagination);
        }

        public async Task<IEnumerable<RequestEntity>> GetByUserIdAsync(int userId)
        {
            var requests = await _context.Requests
                .Where(r => r.RequestUserID == userId)
                .Include(r => r.User)
                .ThenInclude(u => u.AnnualLeave)
                .OrderByDescending(r => r.RequestFromDay)
                .ToListAsync();

            // Map User entities to UserDto for responses
            foreach (var request in requests.Where(r => r.User != null))
            {
                request.UserDto = MapUserToDto(request.User);
            }

            return requests;
        }

        public async Task<PaginatedResult<RequestEntity>> GetByUserIdAsync(int userId, RequestPaginationDto pagination)
        {
            var baseQuery = _context.Requests
                .Where(r => r.RequestUserID == userId)
                .Include(r => r.User)
                .ThenInclude(u => u.AnnualLeave)
                .AsQueryable();

            return await ExecutePaginatedQueryAsync(baseQuery, pagination);
        }

        public async Task<IEnumerable<RequestEntity>> GetByDepartmentAsync(string department)
        {
            var requests = await _context.Requests
                .Where(r => r.RequestDepartment == department)
                .Include(r => r.User)
                .ThenInclude(u => u.AnnualLeave)
                .OrderByDescending(r => r.RequestFromDay)
                .ToListAsync();

            // Map User entities to UserDto for responses
            foreach (var request in requests.Where(r => r.User != null))
            {
                request.UserDto = MapUserToDto(request.User);
            }

            return requests;
        }

        public async Task<PaginatedResult<RequestEntity>> GetByDepartmentAsync(string department, RequestPaginationDto pagination)
        {
            var baseQuery = _context.Requests
                .Where(r => r.RequestDepartment == department)
                .Include(r => r.User)
                .ThenInclude(u => u.AnnualLeave)
                .AsQueryable();

            return await ExecutePaginatedQueryAsync(baseQuery, pagination);
        }

        public async Task<PaginatedResult<RequestEntity>> GetRequestsForManagerAsync(int managerId, string department, RequestPaginationDto pagination)
        {
            var baseQuery = _context.Requests
                .Where(r => r.RequestUserID == managerId || r.RequestDepartment == department)
                .Include(r => r.User)
                .ThenInclude(u => u.AnnualLeave)
                .AsQueryable();

            return await ExecutePaginatedQueryAsync(baseQuery, pagination);
        }

        public async Task<int> CreateAsync(RequestEntity request)
        {
            try
            {
                await _context.Requests.AddAsync(request);
                await _context.SaveChangesAsync();
                return request.RequestID;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "DB error creating request for user {UserId}", request.RequestUserID);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error creating request for user {UserId}", request.RequestUserID);
                throw;
            }
        }

        public async Task<bool> UpdateAsync(RequestEntity request)
        {
            try
            {
                _context.Requests.Update(request);
                int affected = await _context.SaveChangesAsync();
                return affected > 0;
            }
            catch (DbUpdateConcurrencyException)
            {
                // Request not found or modified by another user
                var exists = await _context.Requests.AnyAsync(r => r.RequestID == request.RequestID);
                if (!exists)
                {
                    return false;
                }
                throw;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "DB error updating request {RequestId} for user {UserId}",
                    request.RequestID, request.RequestUserID);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating request {RequestId}", request.RequestID);
                throw;
            }
        }

        public async Task<bool> DeleteAsync(int requestId)
        {
            try
            {
                var request = await _context.Requests.FindAsync(requestId);
                if (request == null)
                    return false;

                _context.Requests.Remove(request);
                int affected = await _context.SaveChangesAsync();
                return affected > 0;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "DB error deleting request {RequestId}", requestId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error deleting request {RequestId}", requestId);
                throw;
            }
        }

        public async Task<Dictionary<string, int>> GetLeaveBalancesAsync(int userId)
        {
            try
            {
                var user = await _context.Users
                    .Include(u => u.AnnualLeave)
                    .FirstOrDefaultAsync(u => u.UserID == userId);

                if (user == null)
                {
                    _logger.LogWarning("Leave balance requested for non-existent user {UserId}", userId);
                    throw new KeyNotFoundException($"User with ID {userId} not found.");
                }

                if (user.AnnualLeave == null)
                {
                    _logger.LogWarning("Annual leave record not found for user {UserId}", userId);
                    throw new KeyNotFoundException($"Annual leave record for user with ID {userId} not found.");
                }

                var balances = new Dictionary<string, int>
                {
                    { "AnnualBalance", user.AnnualLeave.GetAnnualBalance() },
                    { "EmergencyBalance", user.AnnualLeave.GetEmergencyBalance() },
                    { "PermissionBalance", user.AnnualLeave.GetPermissionsBalance() },
                    { "AnnualUsed", await GetLeaveUsedAsync(userId, LeaveType.Annual) },
                    { "EmergencyUsed", await GetLeaveUsedAsync(userId, LeaveType.Emergency) },
                    { "WorkFromHomeUsed", await GetLeaveUsedAsync(userId, LeaveType.WorkFromHome) },
                    { "PermissionUsed", await GetLeaveUsedAsync(userId, LeaveType.Permission) },
                    { "UnpaidUsed", await GetLeaveUsedAsync(userId, LeaveType.Unpaid) }
                };

                return balances;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting leave balances for user {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> UpdateLeaveBalanceAsync(int userId, LeaveType leaveType, int days, bool isAdding)
        {
            try
            {
                // Get the existing AnnualLeave record for user
                var leaveRecord = await _context.AnnualLeaves
                    .FirstOrDefaultAsync(al => al.UserID == userId);

                if (leaveRecord == null)
                {
                    // No AnnualLeave record found - this shouldn't happen as records are created at registration
                    _logger.LogWarning("No AnnualLeave record found for user {UserId}. Records should be created at registration.", userId);
                    throw new KeyNotFoundException($"Leave record for user with ID {userId} not found.");
                }

                // Update the appropriate balance column based on leave type
                switch (leaveType)
                {
                    case LeaveType.Annual:
                        leaveRecord.AnnualUsed += isAdding ? -days : days;
                        break;
                    case LeaveType.Emergency:
                        leaveRecord.EmergencyUsed += isAdding ? -days : days;
                        break;
                    case LeaveType.Permission:
                        leaveRecord.PermissionsUsed += isAdding ? -days : days;
                        break;
                    case LeaveType.Unpaid:
                        leaveRecord.UnpaidUsed += isAdding ? -days : days;
                        break;
                    case LeaveType.WorkFromHome:
                        leaveRecord.WorkFromHomeUsed += isAdding ? -days : days;
                        if (!isAdding && leaveRecord.WorkFromHomeUsed < 0)
                        {
                            leaveRecord.WorkFromHomeUsed = 0;
                        }
                        break;
                    case LeaveType.ExternalAssignment:
                        // These don't affect leave balances
                        return true;
                    default:
                        _logger.LogWarning("Attempted to update balance for unhandled leave type: {LeaveType}", leaveType.ToString());
                        return false;
                }

                // Validate updated balances (don't allow negative available balance)
                if (!isAdding)
                {
                    // Check if we're trying to use more days than available
                    int annualBalance = leaveRecord.Annual - leaveRecord.AnnualUsed;
                    int emergencyBalance = leaveRecord.EmergencyLeave - leaveRecord.EmergencyUsed;
                    int permissionsBalance = leaveRecord.Permissions - leaveRecord.PermissionsUsed;

                    bool hasInsufficientBalance = false;

                    switch (leaveType)
                    {
                        case LeaveType.Annual:
                            hasInsufficientBalance = annualBalance < 0;
                            break;
                        case LeaveType.Emergency:
                            hasInsufficientBalance = emergencyBalance < 0;
                            break;
                        case LeaveType.Permission:
                            hasInsufficientBalance = permissionsBalance < 0;
                            break;
                    }

                    if (hasInsufficientBalance)
                    {
                        _logger.LogWarning("Insufficient balance for user {UserId}, leave type {LeaveType}", userId, leaveType.ToString());
                        return false;
                    }
                }

                int affected = await _context.SaveChangesAsync();
                return affected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating leave balance for user {UserId}", userId);
                throw;
            }
        }

        public async Task<int> GetPermissionUsedAsync(int userId)
        {
            try
            {
                // Count permission-type requests for this month for this user
                DateTime startOfMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                DateTime endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

                int permissionsUsed = await _context.Requests
                    .CountAsync(r =>
                        r.RequestUserID == userId &&
                        r.RequestType == LeaveType.Permission &&
                        r.RequestFromDay >= startOfMonth &&
                        r.RequestFromDay <= endOfMonth &&
                        r.RequestManagerStatus == RequestStatus.HRApproved &&
                        r.RequestHRStatus == RequestStatus.ManagerApproved);

                return permissionsUsed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting permissions used for user {UserId}", userId);
                throw;
            }
        }

        public async Task<int> GetPendingDaysCountAsync(int userId, LeaveType requestType)
        {
            try
            {
                // Calculate total days for pending requests of specified type
                var pendingRequests = await _context.Requests
                    .Where(r =>
                        r.RequestUserID == userId &&
                        r.RequestType == requestType &&
                        r.RequestManagerStatus == RequestStatus.Pending &&
                        r.RequestHRStatus == RequestStatus.Pending)
                    .ToListAsync();

                int totalDays = pendingRequests.Sum(r => r.RequestNumberOfDays ?? 0);
                return totalDays;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pending days count for user {UserId} and type {RequestType}",
                    userId, requestType.ToString());
                throw;
            }
        }

        public async Task<bool> HasConflictingRequestsAsync(int userId, DateTime startDate, DateTime endDate, int requestId = 0)
        {
            try
            {
                // Check for overlapping date ranges for same user, excluding the current request if it has an ID
                var conflictingRequestsCount = await _context.Requests
                    .CountAsync(r =>
                        r.RequestUserID == userId &&
                        r.RequestID != requestId &&
                        (r.RequestType == LeaveType.Annual || r.RequestType == LeaveType.WorkFromHome ||
                        r.RequestType == LeaveType.Unpaid || r.RequestType == LeaveType.Emergency) &&
                        r.RequestManagerStatus != RequestStatus.Rejected && r.RequestHRStatus != RequestStatus.Rejected &&
                        ((r.RequestFromDay <= endDate && r.RequestToDay >= startDate) ||
                         (r.RequestToDay == null && r.RequestFromDay >= startDate && r.RequestFromDay <= endDate)));

                return conflictingRequestsCount > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for conflicting requests for user {UserId}", userId);
                throw;
            }
        }

        #region Query Builder Methods

        // Executes a paginated query with filtering and sorting
        private async Task<PaginatedResult<RequestEntity>> ExecutePaginatedQueryAsync(
            IQueryable<RequestEntity> baseQuery,
            RequestPaginationDto pagination)
        {
            try
            {
                // Apply filters
                var filteredQuery = ApplyFilters(baseQuery, pagination);

                // Count total items before pagination (for total count)
                var totalCount = await filteredQuery.CountAsync();

                // Apply sorting
                var sortedQuery = ApplySorting(filteredQuery, pagination);

                // Apply pagination
                var items = await sortedQuery
                    .Skip((pagination.Page - 1) * pagination.PageSize)
                    .Take(pagination.PageSize)
                    .ToListAsync();

                // Map User entities to UserDto for responses
                foreach (var request in items.Where(r => r.User != null))
                {
                    request.UserDto = MapUserToDto(request.User);
                }

                return new PaginatedResult<RequestEntity>
                {
                    Items = items,
                    TotalCount = totalCount,
                    PageNumber = pagination.Page,
                    PageSize = pagination.PageSize
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing paginated query with pagination {Page}/{PageSize}",
                    pagination.Page, pagination.PageSize);
                throw;
            }
        }

        // Applies filters based on pagination parameters
        private IQueryable<RequestEntity> ApplyFilters(
            IQueryable<RequestEntity> query,
            RequestPaginationDto pagination)
        {
            // Filter by status if specified
            if (pagination.FilterStatus.HasValue)
            {
                query = query.Where(r =>
                    r.RequestManagerStatus == pagination.FilterStatus.Value ||
                    r.RequestHRStatus == pagination.FilterStatus.Value);
            }

            // Filter by type if specified
            if (pagination.FilterType.HasValue)
            {
                query = query.Where(r => r.RequestType == pagination.FilterType.Value);
            }

            // Filter by date range if specified
            if (pagination.FromDate.HasValue)
            {
                var startDate = pagination.FromDate.Value.Date;
                query = query.Where(r => (r.RequestToDay ?? r.RequestFromDay).Date >= startDate);
            }

            if (pagination.ToDate.HasValue)
            {
                var endDate = pagination.ToDate.Value.Date.AddDays(1).AddSeconds(-1); // End of the day
                query = query.Where(r => r.RequestFromDay.Date <= endDate);
            }

            return query;
        }

        // Applies sorting based on pagination parameters
        private IQueryable<RequestEntity> ApplySorting(
            IQueryable<RequestEntity> query,
            RequestPaginationDto pagination)
        {
            // Use reflection to get the property to sort by
            var property = typeof(RequestEntity).GetProperty(
                GetRequestPropertyName(pagination.SortBy) ?? "CreatedAt");

            if (property == null)
            {
                // Default to CreatedAt if property not found
                return pagination.Ascending ? query.OrderBy(r => r.CreatedAt) : query.OrderByDescending(r => r.CreatedAt);
            }

            // Create a dynamic expression for sorting
            var parameter = System.Linq.Expressions.Expression.Parameter(typeof(RequestEntity), "r");
            var propertyAccess = System.Linq.Expressions.Expression.MakeMemberAccess(parameter, property);
            var lambdaExp = System.Linq.Expressions.Expression.Lambda(propertyAccess, parameter);

            // Apply the sorting
            var methodName = pagination.Ascending ? "OrderBy" : "OrderByDescending";
            var orderByExp = System.Linq.Expressions.Expression.Call(
                typeof(Queryable),
                methodName,
                new Type[] { typeof(RequestEntity), property.PropertyType },
                query.Expression,
                System.Linq.Expressions.Expression.Quote(lambdaExp));

            return query.Provider.CreateQuery<RequestEntity>(orderByExp);
        }

        // Maps DTO property names to entity property names
        private string GetRequestPropertyName(string dtoPropertyName)
        {
            var propertyMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "StartDate", "RequestFromDay" },
                { "EndDate", "RequestToDay" },
                { "Type", "RequestType" },
                { "Status", "RequestStatus" },
                { "Department", "RequestDepartment" },
                { "UserID", "RequestUserID" },
                { "CreatedAt", "CreatedAt" },
                { "UpdatedAt", "UpdatedAt" }
            };

            if (propertyMap.TryGetValue(dtoPropertyName, out var entityProperty))
            {
                return entityProperty;
            }

            // Check if the property exists on the entity directly
            if (typeof(RequestEntity).GetProperty(dtoPropertyName) != null)
            {
                return dtoPropertyName;
            }

            return null;
        }

        #endregion

        #region Helper Methods

        private async Task<int> GetLeaveUsedAsync(int userId, LeaveType leaveType)
        {
            // Count approved leave days for the current year
            DateTime startOfYear = new DateTime(DateTime.Now.Year, 1, 1);
            DateTime endOfYear = new DateTime(DateTime.Now.Year, 12, 31);

            var approvedRequests = await _context.Requests
                .Where(r =>
                    r.RequestUserID == userId &&
                    r.RequestType == leaveType &&
                    r.RequestFromDay >= startOfYear &&
                    r.RequestFromDay <= endOfYear &&
                    r.RequestManagerStatus == RequestStatus.HRApproved &&
                    r.RequestHRStatus == RequestStatus.ManagerApproved)
                .ToListAsync();

            int totalDays = approvedRequests.Sum(r => r.RequestNumberOfDays ?? 0);
            return totalDays;
        }

        /// <summary>
        /// Maps a User entity to UserDto
        /// </summary>
        private UserDto MapUserToDto(UserEntity user)
        {
            var dto = new UserDto
            {
                UserID = user.UserID,
                UserName = user.UserName,
                FullName = user.FullName,
                Department = user.Department,
                Title = user.Title,
                IsActive = user.IsActive,
                IsAdmin = user.IsAdmin,
                IsManager = user.IsManager,
                IsHR = user.IsHR,
                LastLoginDate = user.LastLoginDate,
                LastLoginIp = user.LastLoginIp,
                IsLocked = user.IsLocked,
                FailedLoginAttempts = user.FailedLoginAttempts,
                Roles = new List<string>()
            };

            // Assign roles using RoleService
            _roleService.AssignRoles(dto);

            return dto;
        }

        #endregion
    }
}