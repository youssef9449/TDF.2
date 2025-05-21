using System;
using System.Threading.Tasks;
using TDFAPI.Repositories;
using TDFShared.DTOs.Common;
using TDFShared.DTOs.Requests;
using TDFShared.Models.Request;
using TDFShared.Enums;
using TDFShared.Exceptions;
using TDFShared.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
            ILogger<RequestService> logger)
        {
            _requestRepository = requestRepository;
            _userRepository = userRepository;
            _notificationService = notificationService;
            _logger = logger;
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
        /// Creates a new request for a user.
        /// </summary>
        /// <exception cref="ValidationException">Thrown if validation fails.</exception>
        /// <exception cref="BusinessRuleException">Thrown if business rules are violated.</exception>
        public async Task<RequestResponseDto> CreateAsync(RequestCreateDto createDto, int userId)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                    throw new EntityNotFoundException("User", userId);

                RequestValidationService.ValidateCreateDto(createDto);
                await ValidateRequestDates(
                    createDto.RequestStartDate,
                    createDto.RequestEndDate,
                    userId,
                    createDto.LeaveType);

                int numberOfDays = CalculateBusinessDays(
                    createDto.RequestStartDate,
                    createDto.RequestEndDate ?? createDto.RequestStartDate);

                var request = new RequestEntity
                {
                    RequestUserID = userId,
                    RequestUserFullName = user.FullName,
                    RequestDepartment = user.Department,
                    RequestType = createDto.LeaveType,
                    RequestReason = createDto.RequestReason ?? string.Empty,
                    RequestFromDay = createDto.RequestStartDate,
                    RequestToDay = createDto.RequestEndDate,
                    RequestBeginningTime = createDto.RequestBeginningTime,
                    RequestEndingTime = createDto.RequestEndingTime,
                    RequestStatus = RequestStatusEnum.Pending,
                    RequestHRStatus = RequestStatusEnum.Pending,
                    RequestNumberOfDays = numberOfDays,
                    CreatedAt = DateTime.UtcNow
                };

                int requestId = await _requestRepository.CreateAsync(request);

                if (requestId > 0)
                {
                    await NotifyDepartmentManagers(user.Department, $"New request from {user.FullName}: {createDto.LeaveType.ToString()} request", userId);
                    await NotifyHR($"New request from {user.FullName} in {user.Department}: {createDto.LeaveType.ToString()} request", userId);
                }

                var createdRequestEntity = await _requestRepository.GetByIdAsync(requestId);
                if (createdRequestEntity == null)
                {
                    _logger.LogError("Failed to fetch newly created request with ID {RequestId}", requestId);
                    throw new BusinessRuleException("Failed to retrieve the created request after saving.");
                }

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

                if (updateDto.RowVersion != null && (existingRequest.RowVersion == null || !existingRequest.RowVersion.SequenceEqual(updateDto.RowVersion)))
                {
                    throw new DbUpdateConcurrencyException($"The request has been modified by another user. Please refresh and try again.");
                }

                ValidateUpdateDto(updateDto);
                await ValidateRequestDates(
                    updateDto.RequestStartDate,
                    updateDto.RequestEndDate,
                    existingRequest.RequestUserID,
                    updateDto.LeaveType,
                    id);

                int numberOfDays = CalculateBusinessDays(
                    updateDto.RequestStartDate,
                    updateDto.RequestEndDate ?? updateDto.RequestStartDate);

                existingRequest.RequestType = updateDto.LeaveType;
                existingRequest.RequestReason = updateDto.RequestReason ?? string.Empty;
                existingRequest.RequestFromDay = updateDto.RequestStartDate;
                existingRequest.RequestToDay = updateDto.RequestEndDate;
                existingRequest.RequestBeginningTime = updateDto.RequestBeginningTime;
                existingRequest.RequestEndingTime = updateDto.RequestEndingTime;
                existingRequest.RequestNumberOfDays = numberOfDays;

                bool statusChanged = existingRequest.RequestStatus != RequestStatusEnum.Pending || existingRequest.RequestHRStatus != RequestStatusEnum.Pending;
                if (statusChanged)
                {
                    existingRequest.RequestStatus = RequestStatusEnum.Pending;
                    existingRequest.RequestHRStatus = RequestStatusEnum.Pending;
                    existingRequest.RequestCloser = null;
                    existingRequest.RequestHRCloser = null;
                    existingRequest.RequestRejectReason = null;
                    existingRequest.Remarks = updateDto.Remarks;
                }

                existingRequest.UpdatedAt = DateTime.UtcNow;
                existingRequest.RowVersion = Guid.NewGuid().ToByteArray();

                bool updated = await _requestRepository.UpdateAsync(existingRequest);

                if (updated && statusChanged)
                {
                    await NotifyDepartmentManagers(existingRequest.RequestDepartment, $"{existingRequest.RequestUserFullName} updated a request. Status reset to pending.", userId);
                    await NotifyHR($"{existingRequest.RequestUserFullName} updated a request in {existingRequest.RequestDepartment}. Status reset to pending.", userId);
                }

                var updatedRequestEntity = await _requestRepository.GetByIdAsync(id);
                if (updatedRequestEntity == null)
                {
                    _logger.LogError("Failed to fetch updated request with ID {RequestId}", id);
                    throw new BusinessRuleException("Failed to retrieve the updated request after saving.");
                }

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

                if (!IsPending(existingRequest))
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
        public async Task<bool> ApproveRequestAsync(int requestId, RequestApprovalDto approvalDto, int approverId, string approverName, bool isHR)
        {
            try
            {
                var request = await _requestRepository.GetByIdAsync(requestId);
                if (request == null)
                    throw new EntityNotFoundException("Request", requestId);

                if (!IsPendingForApproval(request, isHR))
                    throw new BusinessRuleException($"Request is not pending {(isHR ? "HR" : "Manager")} approval. Current status: {(isHR ? request.RequestHRStatus : request.RequestStatus)}");

                bool fullyApproved = IsFullyApprovedAfterThisAction(request, isHR);
                string? balanceType = GetBalanceTypeForRequest(request.RequestType);

                if (balanceType != null && fullyApproved)
                {
                    _logger.LogInformation("Request {RequestId} fully approved. Updating leave balance for {LeaveType}.", requestId, balanceType);
                    bool balanceUpdated = await _requestRepository.UpdateLeaveBalanceAsync(
                        request.RequestUserID,
                        Enum.Parse<LeaveType>(balanceType, true),
                        request.RequestNumberOfDays,
                        false);

                    if (!balanceUpdated)
                    {
                        _logger.LogWarning($"Insufficient balance for request {requestId}. Auto-rejecting.");
                        await RejectRequestAsync(requestId, new RequestRejectDto { RejectReason = "Insufficient leave balance." }, approverId, "System", isHR);
                        await _notificationService.CreateNotificationAsync(
                                request.RequestUserID,
                                $"Your {request.RequestType} request was automatically rejected due to insufficient balance.");
                        return false;
                    }
                }

                bool statusUpdated = await UpdateRequestStatus(request, RequestStatusEnum.Approved, approverName, isHR, approvalDto.Comment);

                if (statusUpdated)
                {
                    await _notificationService.CreateNotificationAsync(
                        request.RequestUserID,
                        $"Your {request.RequestType} request has been {(isHR ? "processed by HR" : "reviewed by your manager")}: Approved.");

                    if (!isHR)
                    {
                        await NotifyHR($"{request.RequestUserFullName}'s request has been approved by manager and requires HR review.", approverId);
                    }
                    else if (isHR && request.RequestStatus == RequestStatusEnum.Approved)
                    {
                        await _notificationService.CreateNotificationAsync(
                            request.RequestUserID,
                            $"Your {request.RequestType} request has been fully approved.");
                    }
                }

                return statusUpdated;
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
        public async Task<bool> RejectRequestAsync(int requestId, RequestRejectDto rejectDto, int rejecterId, string rejecterName, bool isHR)
        {
            try
            {
                var request = await _requestRepository.GetByIdAsync(requestId);
                if (request == null)
                    throw new EntityNotFoundException("Request", requestId);

                if (!IsPendingForApproval(request, isHR))
                    throw new BusinessRuleException($"Request is not pending {(isHR ? "HR" : "Manager")} approval. Current status: {(isHR ? request.RequestHRStatus : request.RequestStatus)}");

                bool updated = await UpdateRequestStatus(
                    request,
                    RequestStatusEnum.Rejected,
                    rejecterName,
                    isHR,
                    rejectDto.RejectReason);

                if (updated)
                {
                    await _notificationService.CreateNotificationAsync(
                        request.RequestUserID,
                        $"Your {request.RequestType} request has been rejected by {(isHR ? "HR" : "your manager")}. Reason: {rejectDto.RejectReason}");

                    if (!isHR)
                    {
                        await NotifyHR($"Request from {request.RequestUserFullName} rejected by manager.", rejecterId);
                    }
                    else
                    {
                        await NotifyDepartmentManagers(request.RequestDepartment, $"Request from {request.RequestUserFullName} rejected by HR.", rejecterId);
                    }
                }

                return updated;
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

        public async Task<RequestResponseDto> ProcessRequestApprovalAsync(int requestId, RequestApprovalDto approvalDto, int approverId)
        {
            // Get the approver's details
            var approver = await _userRepository.GetByIdAsync(approverId);
            if (approver == null)
                throw new UnauthorizedAccessException("Approver not found");

            // Get the request
            var request = await _requestRepository.GetByIdAsync(requestId);
            if (request == null)
                throw new EntityNotFoundException("Request", requestId);

            // Validate approver's authorization
            bool canApprove = RequestAuthorizationService.CanManageDepartment(
                approver.IsAdmin,
                approver.IsManager,
                approver.IsHR,
                approver.Department,
                request.RequestDepartment);

            if (!canApprove)
                throw new UnauthorizedAccessException("User is not authorized to approve this request");

            // Validate request state
            if (request.RequestStatus != RequestStatusEnum.Pending)
                throw new BusinessRuleException("Request is not in a state that can be approved");

            // Perform the approval
            var now = DateTime.UtcNow;
            
            // Update request status
            if (approver.IsHR == true)
            {
                request.RequestStatus = RequestStatusEnum.Approved;
                request.HRApprovalDate = now;
                request.HRApproverId = approverId;
                request.HRRemarks = approvalDto.Remarks;
            }
            else
            {
                request.RequestStatus = RequestStatusEnum.ManagerApproved;
                request.ManagerApprovalDate = now;
                request.ManagerApproverId = approverId;
                request.ManagerRemarks = approvalDto.Remarks;
            }

            // Update in database
            await _requestRepository.UpdateAsync(request);

            // Return updated request DTO
            return await GetByIdAsync(requestId);
        }

        public async Task<Dictionary<string, int>> GetLeaveBalancesAsync(int userId)
        {
            // Assuming RequestRepository handles fetching integer balances
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

        #region Helper Methods

        private async Task EnsureUserCanModifyRequest(RequestEntity request, int userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                 throw new UnauthorizedAccessException("User not found.");

            // Allow if user is the owner OR if user is an Admin
            if (request.RequestUserID != userId && user.IsAdmin != true)
            {
                 _logger.LogWarning("User {UserId} (IsAdmin: {IsAdmin}) attempted to modify request {RequestId} owned by {OwnerId}", userId, user.IsAdmin, request.RequestID, request.RequestUserID);
                throw new UnauthorizedAccessException("User is not authorized to modify this request.");
            }
        }
        
        private async Task ValidateRequestDates(DateTime startDate, DateTime? endDate, int userId, LeaveType leaveType, int existingRequestId = 0) 
        {
            await RequestValidationService.ValidateConflictingRequests(startDate, endDate, userId, _requestRepository.HasConflictingRequestsAsync, existingRequestId);
        }

        private int CalculateBusinessDays(DateTime startDate, DateTime endDate)
        {
            int businessDays = 0;
            DateTime currentDate = startDate.Date; // Ignore time part

            while (currentDate <= endDate.Date)
            {
                // Count only weekdays (Monday to Friday)
                if (currentDate.DayOfWeek != DayOfWeek.Saturday && currentDate.DayOfWeek != DayOfWeek.Sunday)
                {
                    businessDays++;
                }
                currentDate = currentDate.AddDays(1);
            }

             // Basic validation: Ensure start date is not after end date
            if (startDate.Date > endDate.Date) {
                 _logger.LogWarning("CalculateBusinessDays called with start date after end date: {StartDate} > {EndDate}", startDate, endDate);
                return 0; // Or throw an exception
            }

            // If start and end date are the same non-weekend day, it counts as 1 day.
            // If they are the same weekend day, it counts as 0 days (handled by the loop).
            // This simple count assumes full days. Partial day logic is removed.

            return businessDays;
        }

        private async Task<bool> UpdateRequestStatus(RequestEntity request, RequestStatusEnum newStatus, string actorName, bool isHR, string? comment)
        {
             // Prevent updating already finalized requests
            if ((request.RequestStatus == RequestStatusEnum.Approved && request.RequestHRStatus == RequestStatusEnum.Approved) ||
                request.RequestStatus == RequestStatusEnum.Rejected || request.RequestHRStatus == RequestStatusEnum.Rejected)
            {
                 _logger.LogWarning("Attempted to update status of already finalized request {RequestId}", request.RequestID);
                throw new InvalidOperationException("Cannot update status of an already finalized request.");
            }

            bool changed = false;
            if (isHR)
            {
                if(request.RequestHRStatus != newStatus)
                {
                    request.RequestHRStatus = newStatus;
                    request.RequestHRCloser = actorName;
                    
                    // Update timestamps appropriately based on status
                    if (newStatus == RequestStatusEnum.Approved)
                    {
                        request.ApprovedAt = DateTime.UtcNow;
                    }
                    else if (newStatus == RequestStatusEnum.Rejected)
                    {
                        request.RejectedAt = DateTime.UtcNow;
                        request.RequestRejectReason = comment ?? "Rejected by HR";
                    }
                    changed = true;
                }
            }
            else // Manager action
            {
                if(request.RequestStatus != newStatus)
                {
                    request.RequestStatus = newStatus;
                    request.RequestCloser = actorName;
                    
                    // Update timestamps appropriately based on status
                    if (newStatus == RequestStatusEnum.Approved)
                    {
                        // If HR already approved, then this is the final approval
                        if (request.RequestHRStatus == RequestStatusEnum.Approved)
                        {
                            request.ApprovedAt = DateTime.UtcNow;
                        }
                    }
                    else if (newStatus == RequestStatusEnum.Rejected)
                    {
                        request.RejectedAt = DateTime.UtcNow;
                        request.RequestRejectReason = comment ?? "Rejected by manager";
                    }
                    changed = true;
                }
            }

            if (changed)
            {
                request.UpdatedAt = DateTime.UtcNow;
                return await _requestRepository.UpdateAsync(request);
            }
            return false;
        }

        private async Task NotifyDepartmentManagers(string department, string message, int excludedUserId = 0)
        {
            // Get users by department, then filter by role
            var usersInDepartment = await _userRepository.GetUsersByDepartmentAsync(department);
            var managers = usersInDepartment.Where(u => u.Role == "Manager"); 
            
            foreach (var manager in managers.Where(m => m.UserID != excludedUserId))
            {
                // Use CreateNotificationAsync instead of SendNotification
                await _notificationService.CreateNotificationAsync(
                    receiverId: manager.UserID, 
                    message: message); // Sender context might vary, using null for now
            }
        }

        private async Task NotifyHR(string message, int excludedUserId = 0)
        {
            var hrUsers = await _userRepository.GetUsersByRoleAsync("HR");
            foreach (var hr in hrUsers.Where(h => h.UserID != excludedUserId))
            {
                 // Use CreateNotificationAsync instead of SendNotification
                 await _notificationService.CreateNotificationAsync(
                     receiverId: hr.UserID, 
                     message: message); // Sender context might vary, using null for now
            }
        }

        // --- Status/Approval Helpers ---
        private bool IsPendingForApproval(RequestEntity request, bool isHR)
        {
            return isHR
                ? request.RequestHRStatus == RequestStatusEnum.Pending && request.RequestStatus == RequestStatusEnum.Approved
                : request.RequestStatus == RequestStatusEnum.Pending;
        }
        private bool IsFullyApprovedAfterThisAction(RequestEntity request, bool isHR)
        {
            return (isHR && request.RequestStatus == RequestStatusEnum.Approved) ||
                   (!isHR && request.RequestHRStatus == RequestStatusEnum.Approved);
        }
        
        // --- Centralized Validation Helpers ---
        private LeaveType ParseLeaveType(string leaveType)
        {
            if (string.IsNullOrWhiteSpace(leaveType))
                throw new ValidationException("Leave type is required.");
            if (Enum.TryParse<LeaveType>(leaveType.Replace(" ", ""), true, out var parsed))
                return parsed;
            // Map aliases
            if (leaveType.Equals("Casual Leave", StringComparison.OrdinalIgnoreCase) || leaveType.Equals("Casual", StringComparison.OrdinalIgnoreCase))
                return LeaveType.Emergency;
            if (leaveType.Equals("External Assignment", StringComparison.OrdinalIgnoreCase))
                return LeaveType.ExternalAssignment;
            if (leaveType.Equals("Work From Home", StringComparison.OrdinalIgnoreCase) || leaveType.Equals("WFH", StringComparison.OrdinalIgnoreCase))
                return LeaveType.WorkFromHome;
            throw new ValidationException($"Unsupported leave type: {leaveType}");
        }

        private void ValidateCreateDto(RequestCreateDto dto)
        {
            // Type support validation
            if (!_supportedLeaveTypes.Contains(dto.LeaveType))
                throw new ValidationException($"Leave type '{dto.LeaveType}' is not supported.");

            // Base validation using shared service
            RequestValidationService.ValidateCreateDto(dto);
        }

        private void ValidateUpdateDto(RequestUpdateDto dto)
        {
            // Type support validation
            if (!_supportedLeaveTypes.Contains(dto.LeaveType))
                throw new ValidationException($"Leave type '{dto.LeaveType}' is not supported.");

            // Base validation using shared service
            RequestValidationService.ValidateUpdateDto(dto);
        }

        private bool IsPending(RequestEntity request)
        {
            return request.RequestStatus == RequestStatusEnum.Pending && request.RequestHRStatus == RequestStatusEnum.Pending;
        }
        // Helper to determine which balance to update
        private string? GetBalanceTypeForRequest(LeaveType leaveType) 
        {
            switch (leaveType)
            {
                case LeaveType.Annual:
                    return "annual";
                case LeaveType.Emergency:
                    return "casual";
                case LeaveType.Permission:
                    return "permission";
                case LeaveType.Unpaid:
                    return "unpaid";
                // ExternalAssignment and WorkFromHome do not affect balances
                default:
                    return null;
            }
        }
        
        // ----- Mappers ----- 
        
        private async Task<RequestResponseDto> MapToResponseDto(RequestEntity? request)
        {
            if (request == null)
                throw new EntityNotFoundException("Request", "null");

            // Fetch balances using the potentially updated method returning int dictionary
            Dictionary<string, int> balances = await GetLeaveBalancesAsync(request.RequestUserID);
            string? balanceType = GetBalanceTypeForRequest(request.RequestType);
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
                Status = request.RequestStatus,
                Remarks = request.Remarks,
                ApproverName = request.RequestCloser,
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
    }
}