using System;
using System.Threading.Tasks;
using TDFAPI.Repositories;
using TDFShared.DTOs.Common;
using TDFShared.DTOs.Requests;
using TDFShared.Models.Request;
using TDFShared.Enums;
using TDFShared.Exceptions;
using TDFShared.Services;
using TDFShared.DTOs.Users;
using EntityNotFoundException = TDFAPI.Exceptions.EntityNotFoundException;
using RequestStatusEnum = TDFShared.Enums.RequestStatus;

namespace TDFAPI.Services
{
    public class RequestService : IRequestService
    {
        private readonly IRequestRepository _requestRepository;
        private readonly IUserRepository _userRepository;
        private readonly INotificationService _notificationService;
        private readonly ILogger<RequestService> _logger;
        private readonly TDFShared.Validation.IValidationService _validationService;
        private readonly TDFShared.Validation.IBusinessRulesService _businessRulesService;
        private readonly IRoleService _roleService;

        // Known leave request types (now using enum)
        private static readonly LeaveType[] _supportedLeaveTypes =
        {
            LeaveType.Annual,
            LeaveType.Emergency,
            LeaveType.Unpaid,
            LeaveType.Permission,
            LeaveType.ExternalAssignment,
            LeaveType.WorkFromHome
        };

        public RequestService(
            IRequestRepository requestRepository,
            IUserRepository userRepository,
            INotificationService notificationService,
            ILogger<RequestService> logger,
            TDFShared.Validation.IValidationService validationService,
            TDFShared.Validation.IBusinessRulesService businessRulesService,
            IRoleService roleService)
        {
            _requestRepository = requestRepository;
            _userRepository = userRepository;
            _notificationService = notificationService;
            _logger = logger;
            _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
            _businessRulesService = businessRulesService ?? throw new ArgumentNullException(nameof(businessRulesService));
            _roleService = roleService ?? throw new ArgumentNullException(nameof(roleService));
        }

        /// <summary>
        /// Gets a request by its unique identifier.
        /// </summary>
        /// <param name="id">The request integer ID.</param>
        /// <returns>The request response DTO.</returns>
        /// <exception cref="EntityNotFoundException">Thrown if the request does not exist.</exception>
        public async Task<RequestResponseDto> GetByIdAsync(int id)
        {
            var request = await _requestRepository.GetByIdAsync(id);
            if (request == null)
                throw new EntityNotFoundException("Request", id);
            return await MapToResponseDto(request);
        }

        /// <summary>
        /// Gets paginated requests for a user.
        /// </summary>
        public async Task<PaginatedResult<RequestResponseDto>> GetByUserIdAsync(int userId, RequestPaginationDto pagination)
        {
            var paginatedResult = await _requestRepository.GetByUserIdAsync(userId, pagination);
            return await MapToPaginatedResponseDto(paginatedResult);
        }

        /// <summary>
        /// Gets all paginated requests.
        /// </summary>
        public async Task<PaginatedResult<RequestResponseDto>> GetAllAsync(RequestPaginationDto pagination)
        {
             var paginatedResult = await _requestRepository.GetAllAsync(pagination);
             return await MapToPaginatedResponseDto(paginatedResult);
        }

        /// <summary>
        /// Gets paginated requests by department.
        /// </summary>
        public async Task<PaginatedResult<RequestResponseDto>> GetByDepartmentAsync(string department, RequestPaginationDto pagination)
        {
             var paginatedResult = await _requestRepository.GetByDepartmentAsync(department, pagination);
             return await MapToPaginatedResponseDto(paginatedResult);
        }

        /// <summary>
        /// Gets paginated requests for a manager (their own requests + requests from users in their department).
        /// </summary>
        public async Task<PaginatedResult<RequestResponseDto>> GetRequestsForManagerAsync(int managerId, string department, RequestPaginationDto pagination)
        {
             var paginatedResult = await _requestRepository.GetRequestsForManagerAsync(managerId, department, pagination);
             return await MapToPaginatedResponseDto(paginatedResult);
        }

        /// <summary>
        /// Creates a new request for a user.
        /// </summary>
        /// <remarks>
        /// Both the API and the shared validation service validate DTOs. This is intentional for defense-in-depth:
        /// - API validation provides fast feedback and security at the boundary.
        /// - Shared service validation ensures business rules are enforced regardless of entry point.
        /// </remarks>
        /// <exception cref="ValidationException">Thrown if validation fails.</exception>
        /// <exception cref="BusinessRuleException">Thrown if business rules are violated.</exception>
        public async Task<RequestResponseDto> CreateAsync(RequestCreateDto createDto, int userId)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                    throw new EntityNotFoundException("User", userId);

                // Validate using shared validation service
                _validationService.ValidateAndThrow(createDto);

                // Create business rule context
                var context = new TDFShared.Validation.BusinessRuleContext
                {
                    GetLeaveBalanceAsync = async (uid, leaveType) =>
                    {
                        var balances = await _requestRepository.GetLeaveBalancesAsync(uid);
                        var key = LeaveTypeHelper.GetBalanceKey(leaveType);
                        return key != null && balances.TryGetValue(key, out var balance) ? balance : 0;
                    },
                    HasConflictingRequestsAsync = _requestRepository.HasConflictingRequestsAsync,
                    MinAdvanceNoticeDays = 1,
                    MaxRequestDurationDays = 30
                };

                // Validate business rules
                var businessRuleResult = await _businessRulesService.ValidateLeaveRequestAsync(createDto, userId, context);
                if (!businessRuleResult.IsValid)
                {
                    throw new BusinessRuleException(string.Join("; ", businessRuleResult.Errors));
                }

                // Calculate business days (still needed for entity creation)
                int numberOfDays = CalculateBusinessDays(
                    createDto.RequestStartDate,
                    createDto.RequestEndDate ?? createDto.RequestStartDate);

                var request = new RequestEntity
                {
                    RequestUserID = userId,
                    RequestUserFullName = user.FullName ?? string.Empty,
                    RequestDepartment = user.Department,
                    RequestType = createDto.LeaveType,
                    RequestReason = createDto.RequestReason ?? string.Empty,
                    RequestFromDay = createDto.RequestStartDate,
                    RequestToDay = createDto.RequestEndDate,
                    RequestBeginningTime = createDto.RequestBeginningTime,
                    RequestEndingTime = createDto.RequestEndingTime,
                    RequestManagerStatus = RequestStatusEnum.Pending,
                    RequestHRStatus = RequestStatusEnum.Pending,
                    RequestNumberOfDays = numberOfDays,
                    CreatedAt = DateTime.UtcNow
                };

                int requestId = await _requestRepository.CreateAsync(request);
                var createdRequestEntity = await _requestRepository.GetByIdAsync(requestId);

                if (createdRequestEntity == null)
                {
                    _logger.LogError("Failed to fetch created request with ID {RequestId}", requestId);
                    throw new BusinessRuleException("Failed to retrieve the created request after saving.");
                }

                await NotifyDepartmentManagers(request.RequestDepartment,
                    $"New {request.RequestType} request from {user.FullName}", userId);

                return await MapToResponseDto(createdRequestEntity);
            }
            catch (ValidationException ex)
            {
                _logger.LogWarning(ex, "Validation error creating request for user {UserId}: {ErrorMessage}", userId, ex.Message);
                throw;
            }
            catch (BusinessRuleException ex)
            {
                _logger.LogWarning(ex, "Business rule error creating request for user {UserId}: {ErrorMessage}", userId, ex.Message);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating request for user {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// Updates an existing request.
        /// </summary>
        public async Task<RequestResponseDto> UpdateAsync(int id, RequestUpdateDto updateDto, int userId)
        {
            try
            {
                var existingRequest = await _requestRepository.GetByIdAsync(id);
                if (existingRequest == null)
                    throw new EntityNotFoundException("Request", id);

                await EnsureUserCanModifyRequest(existingRequest, userId);

                if (!CanEdit(existingRequest.RequestManagerStatus, existingRequest.RequestHRStatus))
                    throw new BusinessRuleException("Request cannot be edited in its current state.");

                // Validate using shared validation service
                _validationService.ValidateAndThrow(updateDto);

                // Create business rule context
                var context = new TDFShared.Validation.BusinessRuleContext
                {
                    GetLeaveBalanceAsync = async (uid, leaveType) =>
                    {
                        var balances = await _requestRepository.GetLeaveBalancesAsync(uid);
                        return balances.TryGetValue(leaveType.ToString().ToLower(), out var balance) ? balance : 0;
                    },
                    HasConflictingRequestsAsync = _requestRepository.HasConflictingRequestsAsync,
                    MinAdvanceNoticeDays = 0, // Allow same-day updates for existing requests
                    MaxRequestDurationDays = 30
                };

                // Validate business rules for update
                var businessRuleResult = await _businessRulesService.ValidateLeaveRequestUpdateAsync(updateDto, id, userId, context);
                if (!businessRuleResult.IsValid)
                {
                    throw new BusinessRuleException(string.Join("; ", businessRuleResult.Errors));
                }

                int numberOfDays = CalculateBusinessDays(
                    updateDto.RequestStartDate,
                    updateDto.RequestEndDate ?? updateDto.RequestStartDate);

                // Update the request
                existingRequest.RequestType = updateDto.LeaveType;
                existingRequest.RequestReason = updateDto.RequestReason ?? string.Empty;
                existingRequest.RequestFromDay = updateDto.RequestStartDate;
                existingRequest.RequestToDay = updateDto.RequestEndDate;
                existingRequest.RequestBeginningTime = updateDto.RequestBeginningTime;
                existingRequest.RequestEndingTime = updateDto.RequestEndingTime;
                existingRequest.RequestNumberOfDays = numberOfDays;
                existingRequest.UpdatedAt = DateTime.UtcNow;

                await _requestRepository.UpdateAsync(existingRequest);

                var updatedRequestEntity = await _requestRepository.GetByIdAsync(id);
                if (updatedRequestEntity == null)
                {
                    _logger.LogError("Failed to fetch updated request with ID {RequestId}", id);
                    throw new BusinessRuleException("Failed to retrieve the updated request after saving.");
                }

                await NotifyDepartmentManagers(updatedRequestEntity.RequestDepartment,
                    $"Request from {updatedRequestEntity.RequestUserFullName} was updated", userId);

                return await MapToResponseDto(updatedRequestEntity);
            }
            catch (ValidationException ex)
            {
                _logger.LogWarning(ex, "Validation error updating request {RequestId} by user {UserId}: {ErrorMessage}", id, userId, ex.Message);
                throw;
            }
            catch (BusinessRuleException ex)
            {
                _logger.LogWarning(ex, "Business rule error updating request {RequestId} by user {UserId}: {ErrorMessage}", id, userId, ex.Message);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating request {RequestId} by user {UserId}", id, userId);
                throw;
            }
        }

        /// <summary>
        /// Deletes a request if allowed by business rules.
        /// </summary>
        public async Task<bool> DeleteAsync(int id, int userId)
        {
            try
            {
                var existingRequest = await _requestRepository.GetByIdAsync(id);
                if (existingRequest == null)
                    throw new EntityNotFoundException("Request", id);

                await EnsureUserCanModifyRequest(existingRequest, userId);

                if (!CanDelete(existingRequest.RequestManagerStatus, existingRequest.RequestHRStatus))
                    throw new BusinessRuleException("Only requests that are pending both manager and HR approval can be deleted.");

                bool deleted = await _requestRepository.DeleteAsync(id);

                if (deleted)
                {
                    await NotifyDepartmentManagers(existingRequest.RequestDepartment, $"{existingRequest.RequestUserFullName} deleted a pending {existingRequest.RequestType} request.", userId);
                }

                return deleted;
            }
            catch (BusinessRuleException ex)
            {
                _logger.LogWarning(ex, "Business rule error deleting request {RequestId} by user {UserId}: {ErrorMessage}", id, userId, ex.Message);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting request {RequestId} by user {UserId}", id, userId);
                throw;
            }
        }

        /// <summary>
        /// Approves a request as manager or HR, enforcing business rules and leave balance.
        /// </summary>
        /// <exception cref="EntityNotFoundException">Thrown if the request does not exist.</exception>
        /// <exception cref="BusinessRuleException">Thrown if the request cannot be approved.</exception>
        public async Task<bool> ApproveRequestAsync(int requestId, int approverId, bool isHR)
        {
            try
            {
                var request = await _requestRepository.GetByIdAsync(requestId);
                if (request == null)
                    throw new EntityNotFoundException("Request", requestId);

                // Only allow approval if the request is pending (for manager), HR can approve at any status
                if (!isHR && request.RequestManagerStatus != RequestStatusEnum.Pending)
                    throw new BusinessRuleException($"Request is not pending approval. Current status: {request.RequestManagerStatus}");

                if (!isHR)
                {
                    request.RequestManagerStatus = RequestStatusEnum.ManagerApproved;
                    request.ManagerApproverId = approverId;
                }
                else
                {
                    // HR can approve at any status
                    request.RequestManagerStatus = RequestStatusEnum.HRApproved;
                    request.HRApproverId = approverId;
                }

                request.UpdatedAt = DateTime.UtcNow;
                await _requestRepository.UpdateAsync(request);

                // Optionally update leave balance if fully approved (HRApproved)
                if (request.RequestManagerStatus == RequestStatusEnum.HRApproved)
                {
                    string? balanceType = GetBalanceType(request.RequestType);
                    if (balanceType != null)
                    {
                        bool balanceUpdated = await _requestRepository.UpdateLeaveBalanceAsync(
                            request.RequestUserID,
                            Enum.Parse<LeaveType>(balanceType, true),
                            request.RequestNumberOfDays,
                            false);
                        if (!balanceUpdated)
                        {
                            // Auto-reject if balance update fails
                            request.RequestManagerStatus = RequestStatusEnum.Rejected;
                            await _requestRepository.UpdateAsync(request);
                            await _notificationService.CreateNotificationAsync(
                                request.RequestUserID,
                                $"Your {request.RequestType} request was automatically rejected due to insufficient balance.");
                            return false;
                        }
                    }
                    await _notificationService.CreateNotificationAsync(
                        request.RequestUserID,
                        $"Your {request.RequestType} request has been fully approved.");
                }
                else
                {
                    // Notify next approver (HR)
                    if (!isHR)
                    {
                        await NotifyHR($"{request.RequestUserFullName}'s request has been approved by manager and requires HR review.", approverId);
                    }
                }

                return true;
            }
            catch (BusinessRuleException ex)
            {
                _logger.LogWarning(ex, "Business rule error approving request {RequestId} by approver {ApproverId}: {ErrorMessage}", requestId, approverId, ex.Message);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving request {RequestId} by approver {ApproverId} (IsHR: {IsHR})", requestId, approverId, isHR);
                throw;
            }
        }

        /// <summary>
        /// Rejects a request as manager or HR, enforcing business rules.
        /// </summary>
        /// <exception cref="EntityNotFoundException">Thrown if the request does not exist.</exception>
        /// <exception cref="BusinessRuleException">Thrown if the request cannot be rejected.</exception>
        public async Task<bool> RejectRequestAsync(int requestId, int rejecterId, bool isHR, string? remarks = null)
        {
            try
            {
                var request = await _requestRepository.GetByIdAsync(requestId);
                if (request == null)
                    throw new EntityNotFoundException("Request", requestId);

                // Only allow rejection if the request is pending (for manager), HR can reject at any status
                if (!isHR && request.RequestManagerStatus != RequestStatusEnum.Pending)
                    throw new BusinessRuleException("Only pending requests can be rejected by manager.");

                request.RequestManagerStatus = RequestStatusEnum.Rejected;
                if (isHR)
                {
                    request.HRApproverId = rejecterId;
                    request.HRRemarks = remarks;
                }
                else
                {
                    request.ManagerApproverId = rejecterId;
                    request.ManagerRemarks = remarks;
                }
                request.UpdatedAt = DateTime.UtcNow;
                await _requestRepository.UpdateAsync(request);

                string rejectionBy = isHR ? "HR" : "your manager";
                string rejectionReason = isHR ? (request.HRRemarks ?? "No remarks provided") : (request.ManagerRemarks ?? "No remarks provided");
                string notificationMsg = $"Your {request.RequestType} request has been rejected by {rejectionBy}. Reason: {rejectionReason}";
                await _notificationService.CreateNotificationAsync(
                    request.RequestUserID,
                    notificationMsg);

                if (!isHR)
                {
                    await NotifyHR($"Request from {request.RequestUserFullName} rejected by manager.", rejecterId);
                }
                else
                {
                    await NotifyDepartmentManagers(request.RequestDepartment, $"Request from {request.RequestUserFullName} rejected by HR.", rejecterId);
                }

                return true;
            }
            catch (BusinessRuleException ex)
            {
                _logger.LogWarning(ex, "Business rule error rejecting request {RequestId} by user {RejecterId}: {ErrorMessage}", requestId, rejecterId, ex.Message);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting request {RequestId} by user {RejecterId}", requestId, rejecterId);
                throw;
            }
        }

        #region Helper Methods

        private async Task EnsureUserCanModifyRequest(RequestEntity request, int userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                 throw new UnauthorizedAccessException("User not found.");

            _roleService.AssignRoles(user);
            // Allow if user is the owner OR if user is an Admin
            if (request.RequestUserID != userId && !_roleService.HasRole(user, "Admin"))
            {
                 _logger.LogWarning("User {UserId} (IsAdmin: {IsAdmin}) attempted to modify request {RequestId} owned by {OwnerId}", userId, user.IsAdmin, request.RequestID, request.RequestUserID);
                throw new UnauthorizedAccessException("User is not authorized to modify this request.");
            }
        }

        private async Task NotifyDepartmentManagers(string department, string message, int excludedUserId = 0)
        {
            // Get users by department and role
            var usersInDepartment = await _userRepository.GetUsersByDepartmentAsync(department);
            var managers = await _userRepository.GetUsersByRoleAsync("Manager");
            
            // Find managers in the specific department
            var departmentManagers = managers.Where(m => 
                usersInDepartment.Any(u => u.UserID == m.UserID) && 
                m.UserID != excludedUserId);

            foreach (var manager in departmentManagers)
            {
                await _notificationService.CreateNotificationAsync(
                    receiverId: manager.UserID,
                    message: message);
            }
        }

        private async Task NotifyHR(string message, int excludedUserId = 0)
        {
            var hrUsers = await _userRepository.GetUsersByRoleAsync("HR");
            foreach (var hr in hrUsers.Where(h => h.UserID != excludedUserId))
            {
                await _notificationService.CreateNotificationAsync(
                    receiverId: hr.UserID,
                    message: message);
            }
        }

        // --- Centralized Validation Helpers ---
        private LeaveType ParseLeaveType(string leaveType)
        {
            return LeaveTypeHelper.Parse(leaveType);
        }

        // ----- Mappers -----
        private async Task<RequestResponseDto> MapToResponseDto(RequestEntity? request)
        {
            if (request == null)
                throw new EntityNotFoundException("Request", "null");

            // Fetch balances using the potentially updated method returning int dictionary
            Dictionary<string, int> balances = await GetLeaveBalancesAsync(request.RequestUserID);
            string? balanceType = GetBalanceType(request.RequestType);
            int? remainingBalance = null;

            if (balanceType != null && balances.TryGetValue(balanceType, out int balanceValue))
            {
                remainingBalance = balanceValue;
            }


            return new RequestResponseDto
            {
                RequestID = request.RequestID,
                RequestUserID = request.RequestUserID,
                UserName = request.RequestUserFullName,
                LeaveType = request.RequestType,
                RequestReason = request.RequestReason,
                RequestStartDate = request.RequestFromDay,
                RequestEndDate = request.RequestToDay,
                RequestBeginningTime = request.RequestBeginningTime,
                RequestEndingTime = request.RequestEndingTime,
                CreatedDate = request.CreatedAt,
                LastModifiedDate = request.UpdatedAt,
                RequestNumberOfDays = request.RequestNumberOfDays,
                RemainingBalance = remainingBalance,
                RowVersion = request.RowVersion
            };
        }

         private async Task<PaginatedResult<RequestResponseDto>> MapToPaginatedResponseDto(PaginatedResult<RequestEntity> paginatedRequests)
        {
            if (paginatedRequests == null) throw new EntityNotFoundException("Request", "null");

            // Map items asynchronously if MapToResponseDto becomes async (due to balance fetch)
            var mappedItems = new List<RequestResponseDto>();
            if (paginatedRequests.Items != null)
            {
                 foreach(var item in paginatedRequests.Items)
                 {
                     mappedItems.Add(await MapToResponseDto(item));
                 }
            }

            return new PaginatedResult<RequestResponseDto>
            {
                Items = mappedItems,
                TotalCount = paginatedRequests.TotalCount,
                PageNumber = paginatedRequests.PageNumber,
                PageSize = paginatedRequests.PageSize
            };
        }

        #endregion

        #region Private Methods

        // Only keep helpers that are actually used by the main logic
        private static int CalculateBusinessDays(DateTime startDate, DateTime endDate)
        {
            if (startDate > endDate) return 0;
            int businessDays = 0;
            DateTime current = startDate.Date;
            while (current <= endDate.Date)
            {
                if (current.DayOfWeek != DayOfWeek.Saturday && current.DayOfWeek != DayOfWeek.Sunday)
                {
                    businessDays++;
                }
                current = current.AddDays(1);
            }
            return businessDays;
        }

        private static bool CanEdit(RequestStatus status, RequestStatus hrStatus)
        {
            return status == RequestStatus.Pending && hrStatus == RequestStatus.Pending;
        }

        private static bool CanDelete(RequestStatus status, RequestStatus hrStatus)
        {
            return status == RequestStatus.Pending && hrStatus == RequestStatus.Pending;
        }

        private static string? GetBalanceType(string leaveType)
        {
            var parsed = LeaveTypeHelper.Parse(leaveType);
            return parsed is LeaveType.Annual or LeaveType.Emergency or LeaveType.Permission ? leaveType : null;
        }

        private static string? GetBalanceType(LeaveType? leaveType)
        {
            return leaveType is LeaveType.Annual or LeaveType.Emergency or LeaveType.Permission ? leaveType.ToString() : null;
        }

        private static bool CanManageDepartment(IRoleService roleService, UserDto user, string? requestDepartment)
        {
            if (user == null || string.IsNullOrEmpty(requestDepartment)) return false;
            roleService.AssignRoles(user);
            if (roleService.HasAnyRole(user, "Admin", "HR")) return true;
            if (!roleService.HasRole(user, "Manager") || string.IsNullOrEmpty(user.Department)) return false;
            return RequestStateManager.CanManageDepartment(user, requestDepartment);
        }
        #endregion

        public async Task<bool> ManagerApproveRequestAsync(int id, ManagerApprovalDto approvalDto, int approverId, string approverName)
        {
            var request = await _requestRepository.GetByIdAsync(id);
            if (request == null)
                throw new EntityNotFoundException("Request", id);
            if (request.RequestManagerStatus != RequestStatusEnum.Pending)
                throw new BusinessRuleException("Request is not pending manager approval.");
            request.RequestManagerStatus = RequestStatusEnum.ManagerApproved;
            request.ManagerApproverId = approverId;
            request.ManagerRemarks = approvalDto.ManagerRemarks;
            request.UpdatedAt = DateTime.UtcNow;
            await _requestRepository.UpdateAsync(request);
            await NotifyHR($"Request from {request.RequestUserFullName} approved by manager.", approverId);
            return true;
        }

        public async Task<bool> HRApproveRequestAsync(int id, HRApprovalDto approvalDto, int approverId, string approverName)
        {
            var request = await _requestRepository.GetByIdAsync(id);
            if (request == null)
                throw new EntityNotFoundException("Request", id);
            if (request.RequestManagerStatus != RequestStatusEnum.ManagerApproved)
                throw new BusinessRuleException("Request must be manager approved before HR approval.");
            request.RequestManagerStatus = RequestStatusEnum.HRApproved;
            request.HRApproverId = approverId;
            request.HRRemarks = approvalDto.HRRemarks;
            request.UpdatedAt = DateTime.UtcNow;
            await _requestRepository.UpdateAsync(request);
            await _notificationService.CreateNotificationAsync(request.RequestUserID, $"Your {request.RequestType} request has been fully approved.");
            return true;
        }

        public async Task<bool> ManagerRejectRequestAsync(int id, ManagerRejectDto rejectDto, int rejecterId, string rejecterName)
        {
            var request = await _requestRepository.GetByIdAsync(id);
            if (request == null)
                throw new EntityNotFoundException("Request", id);
            if (request.RequestManagerStatus != RequestStatusEnum.Pending)
                throw new BusinessRuleException("Request is not pending manager approval.");
            request.RequestManagerStatus = RequestStatusEnum.Rejected;
            request.ManagerApproverId = rejecterId;
            request.ManagerRemarks = rejectDto.ManagerRemarks;
            request.UpdatedAt = DateTime.UtcNow;
            await _requestRepository.UpdateAsync(request);
            await NotifyHR($"Request from {request.RequestUserFullName} rejected by manager.", rejecterId);
            await _notificationService.CreateNotificationAsync(request.RequestUserID, $"Your {request.RequestType} request was rejected by your manager. Reason: {rejectDto.ManagerRemarks}");
            return true;
        }

        public async Task<bool> HRRejectRequestAsync(int id, HRRejectDto rejectDto, int rejecterId, string rejecterName)
        {
            var request = await _requestRepository.GetByIdAsync(id);
            if (request == null)
                throw new EntityNotFoundException("Request", id);
            if (request.RequestManagerStatus != RequestStatusEnum.ManagerApproved)
                throw new BusinessRuleException("Request must be manager approved before HR rejection.");
            request.RequestManagerStatus = RequestStatusEnum.Rejected;
            request.HRApproverId = rejecterId;
            request.HRRemarks = rejectDto.HRRemarks;
            request.UpdatedAt = DateTime.UtcNow;
            await _requestRepository.UpdateAsync(request);
            await _notificationService.CreateNotificationAsync(request.RequestUserID, $"Your {request.RequestType} request was rejected by HR. Reason: {rejectDto.HRRemarks}");
            await NotifyDepartmentManagers(request.RequestDepartment, $"Request from {request.RequestUserFullName} rejected by HR.", rejecterId);
            return true;
        }

        public async Task<Dictionary<string, int>> GetLeaveBalancesAsync(int userId)
        {
            return await _requestRepository.GetLeaveBalancesAsync(userId);
        }

        public async Task<int> GetPermissionUsedAsync(int userId)
        {
            return await _requestRepository.GetPermissionUsedAsync(userId);
        }

        public async Task<int> GetPendingDaysCountAsync(int userId, string requestType)
        {
            LeaveType leaveType = ParseLeaveType(requestType);
            return await _requestRepository.GetPendingDaysCountAsync(userId, leaveType);
        }

        public async Task<bool> HasConflictingRequestsAsync(int userId, DateTime startDate, DateTime endDate, int requestId = 0)
        {
            return await _requestRepository.HasConflictingRequestsAsync(userId, startDate, endDate, requestId);
        }
    }
}