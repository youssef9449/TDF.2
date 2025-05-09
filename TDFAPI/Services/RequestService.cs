using System;
using TDFAPI.Repositories;
using TDFShared.DTOs.Common;
using TDFShared.DTOs.Requests;
using TDFShared.Models.Request;
using Microsoft.EntityFrameworkCore;

namespace TDFAPI.Services
{
    public class RequestService : IRequestService
    {
        private readonly IRequestRepository _requestRepository;
        private readonly IUserRepository _userRepository;
        private readonly INotificationService _notificationService;
        private readonly ILogger<RequestService> _logger;

        // Known leave request types
        private readonly string[] _leaveTypes = { "Annual", "Work From Home", "Unpaid", "Emergency", "Permission", "External Assignment" };

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

        // Returns Response DTO
        public async Task<RequestResponseDto> GetByIdAsync(Guid id)
        {
            var request = await _requestRepository.GetByIdAsync(id);
            return await MapToResponseDto(request); // Pass userId if needed for balance
        }

        // Uses DTO for pagination, returns Paginated Response DTOs
        public async Task<PaginatedResult<RequestResponseDto>> GetByUserIdAsync(int userId, RequestPaginationDto pagination)
        {
            var paginatedResult = await _requestRepository.GetByUserIdAsync(userId, pagination);
            return await MapToPaginatedResponseDto(paginatedResult); 
        }

        public async Task<PaginatedResult<RequestResponseDto>> GetAllAsync(RequestPaginationDto pagination)
        {
             var paginatedResult = await _requestRepository.GetAllAsync(pagination);
             return await MapToPaginatedResponseDto(paginatedResult);
        }

        public async Task<PaginatedResult<RequestResponseDto>> GetByDepartmentAsync(string department, RequestPaginationDto pagination)
        {
             var paginatedResult = await _requestRepository.GetByDepartmentAsync(department, pagination);
             return await MapToPaginatedResponseDto(paginatedResult);
        }

        public async Task<RequestResponseDto> CreateAsync(RequestCreateDto createDto, int userId)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                    throw new ArgumentException($"User with ID {userId} does not exist");

                // Use DTO properties for validation
                DateTime effectiveEndDate = await ValidateRequestDates(
                    createDto.RequestStartDate,
                    createDto.RequestEndDate,
                    userId,
                    createDto.LeaveType);

                // Calculate days using integers
                int numberOfDays = CalculateBusinessDays(
                    createDto.RequestStartDate,
                    effectiveEndDate); // Removed time arguments

                // Map Create DTO to Request Model
                var request = new RequestEntity
                {
                    Id = Guid.NewGuid(),
                    RequestUserID = userId,
                    RequestUserFullName = user.FullName,
                    RequestDepartment = user.Department,
                    RequestType = createDto.LeaveType,
                    RequestReason = createDto.RequestReason,
                    RequestFromDay = createDto.RequestStartDate,
                    RequestToDay = createDto.RequestEndDate,
                    RequestBeginningTime = createDto.RequestBeginningTime,
                    RequestEndingTime = createDto.RequestEndingTime,
                    RequestStatus = "Pending",
                    RequestHRStatus = "Pending",
                    RequestNumberOfDays = numberOfDays, // Assign int value
                    CreatedAt = DateTime.UtcNow
                };

                Guid requestId = await _requestRepository.CreateAsync(request);

                if (requestId != Guid.Empty)
                {
                    await NotifyDepartmentManagers(user.Department, $"New request from {user.FullName}: {createDto.LeaveType} request", userId);
                    await NotifyHR($"New request from {user.FullName} in {user.Department}: {createDto.LeaveType} request", userId);
                }

                var createdRequestEntity = await _requestRepository.GetByIdAsync(requestId);
                if (createdRequestEntity == null)
                {
                    _logger.LogError("Failed to fetch newly created request with ID {RequestId}", requestId);
                    throw new InvalidOperationException("Failed to retrieve the created request after saving.");
                }

                return await MapToResponseDto(createdRequestEntity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating request for user {UserId}", userId);
                throw;
            }
        }

        // Uses Update DTO for input
        public async Task<RequestResponseDto> UpdateAsync(Guid id, RequestUpdateDto updateDto, int userId)
        {
            try
            {
                var existingRequest = await _requestRepository.GetByIdAsync(id);
                if (existingRequest == null)
                    throw new KeyNotFoundException($"Request with ID {id} does not exist");

                await EnsureUserCanModifyRequest(existingRequest, userId);
                
                // Optimistic concurrency check
                if (updateDto.RowVersion != null && !existingRequest.RowVersion.SequenceEqual(updateDto.RowVersion))
                {
                    throw new DbUpdateConcurrencyException($"The request has been modified by another user. Please refresh and try again.");
                }

                // Use DTO properties for validation
                DateTime effectiveEndDate = await ValidateRequestDates(
                    updateDto.RequestStartDate,
                    updateDto.RequestEndDate,
                    existingRequest.RequestUserID,
                    updateDto.LeaveType,
                    id);

                // Recalculate days using integers
                int numberOfDays = CalculateBusinessDays(
                    updateDto.RequestStartDate,
                    effectiveEndDate); // Removed time arguments

                // Update fields from DTO
                existingRequest.RequestType = updateDto.LeaveType;
                existingRequest.RequestReason = updateDto.RequestReason;
                existingRequest.RequestFromDay = updateDto.RequestStartDate;
                existingRequest.RequestToDay = updateDto.RequestEndDate;
                existingRequest.RequestBeginningTime = updateDto.RequestBeginningTime;
                existingRequest.RequestEndingTime = updateDto.RequestEndingTime;
                existingRequest.RequestNumberOfDays = numberOfDays; // Assign int value
                
                bool statusChanged = existingRequest.RequestStatus != "Pending" || existingRequest.RequestHRStatus != "Pending";
                if (statusChanged)
                {
                    existingRequest.RequestStatus = "Pending";
                    existingRequest.RequestHRStatus = "Pending";
                    existingRequest.RequestCloser = null;
                    existingRequest.RequestHRCloser = null;
                    existingRequest.RequestRejectReason = null;
                    existingRequest.Remarks = updateDto.Remarks;
                }

                existingRequest.UpdatedAt = DateTime.UtcNow;
                // Update the row version on save
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
                    throw new InvalidOperationException("Failed to retrieve the updated request after saving.");
                }

                return await MapToResponseDto(updatedRequestEntity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating request {RequestId} by user {UserId}", id, userId);
                throw;
            }
        }

        public async Task<bool> DeleteAsync(Guid id, int userId)
        {
             try
            {
                var existingRequest = await _requestRepository.GetByIdAsync(id);
                if (existingRequest == null)
                    throw new KeyNotFoundException($"Request with ID {id} does not exist");

                await EnsureUserCanModifyRequest(existingRequest, userId);

                // Check both statuses before allowing deletion
                if (existingRequest.RequestStatus != "Pending" || existingRequest.RequestHRStatus != "Pending") 
                    throw new InvalidOperationException("Only requests that are pending both manager and HR approval can be deleted.");

                bool deleted = await _requestRepository.DeleteAsync(id);

                if (deleted)
                {
                     // Notify managers (and optionally HR) that the request was deleted
                     await NotifyDepartmentManagers(existingRequest.RequestDepartment, $"{existingRequest.RequestUserFullName} deleted a pending {existingRequest.RequestType} request.", userId);
                     // Optionally notify HR
                     // await NotifyHR($"{existingRequest.RequestUserFullName} deleted a pending request in {existingRequest.RequestDepartment}.", userId);
                }

                return deleted;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting request {RequestId} by user {UserId}", id, userId);
                throw;
            }
        }

        // Uses Approval DTO for input
        public async Task<bool> ApproveRequestAsync(Guid requestId, RequestApprovalDto approvalDto, int approverId, string approverName, bool isHR)
        {
            try
            {
                var request = await _requestRepository.GetByIdAsync(requestId);
                if (request == null)
                    throw new KeyNotFoundException($"Request with ID {requestId} not found.");

                // Logic to determine which status to update and check current state
                string currentStatus = isHR ? request.RequestHRStatus : request.RequestStatus;
                if (currentStatus != "Pending")
                    throw new InvalidOperationException($"Request is not pending {(isHR ? "HR" : "Manager")} approval. Current status: {currentStatus}");

                // Check if both manager and HR have approved the request
                bool fullyApproved = false;
                if (isHR && request.RequestStatus == "Approved") 
                {
                    // HR is approving and manager has already approved
                    fullyApproved = true;
                }
                else if (!isHR && request.RequestHRStatus == "Approved")
                {
                    // Manager is approving and HR has already approved
                    fullyApproved = true;
                }

                // Handle balance deduction if it's a leave type and fully approved
                string? balanceType = GetBalanceTypeForRequest(request.RequestType);
                
                if (balanceType != null && fullyApproved)
                {
                    _logger.LogInformation("Request {RequestId} fully approved. Updating leave balance for {LeaveType}.", requestId, balanceType);
                    // Pass the integer number of days
                    bool balanceUpdated = await _requestRepository.UpdateLeaveBalanceAsync(
                        request.RequestUserID,
                        balanceType,
                        request.RequestNumberOfDays, // Pass int value
                        false); // isAdding = false (deducting)

                    if (!balanceUpdated)
                    {
                        // If balance update fails (e.g., insufficient funds), reject the request instead
                        _logger.LogWarning($"Insufficient balance for request {requestId}. Auto-rejecting.");
                        await RejectRequestAsync(requestId, new RequestRejectDto { RejectReason = "Insufficient leave balance." }, approverId, "System", isHR);
                        
                        // Notify user about auto-rejection due to balance
                        await _notificationService.CreateNotificationAsync(
                                request.RequestUserID,
                                $"Your {request.RequestType} request was automatically rejected due to insufficient balance.");
                        return false; // Indicate failure due to balance
                    }
                    
                    // Balance updated successfully
                    _logger.LogInformation($"Leave balance updated for user {request.RequestUserID}, type {balanceType}, days {request.RequestNumberOfDays}");
                }

                // Update the status
                bool statusUpdated = await UpdateRequestStatus(request, "Approved", approverName, isHR, approvalDto.Comment);

                if (statusUpdated)
                {
                    // Notify the user who made the request
                    await _notificationService.CreateNotificationAsync(
                        request.RequestUserID,
                        $"Your {request.RequestType} request has been {(isHR ? "processed by HR" : "reviewed by your manager")}: Approved.");

                    // If manager approved, notify HR
                    if (!isHR)
                    {
                        await NotifyHR($"{request.RequestUserFullName}'s request has been approved by manager and requires HR review.", approverId);
                    }
                    // If HR approved (and manager already approved), notify user of final approval
                    else if (isHR && request.RequestStatus == "Approved")
                    {
                        await _notificationService.CreateNotificationAsync(
                            request.RequestUserID,
                            $"Your {request.RequestType} request has been fully approved.");
                    }
                }

                return statusUpdated;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving request {RequestId} by approver {ApproverId} (IsHR: {IsHR})", requestId, approverId, isHR);
                throw;
            }
        }

        // Helper to determine which balance to update
        private string? GetBalanceTypeForRequest(string requestType)
        {
            // Case-insensitive comparison is safer
            if (requestType.Equals("Annual", StringComparison.OrdinalIgnoreCase))
            {
                return "annual"; 
            }
            if (requestType.Equals("Emergency", StringComparison.OrdinalIgnoreCase) || 
                requestType.Equals("Casual Leave", StringComparison.OrdinalIgnoreCase))
            {
                 return "casual";
            }
            if (requestType.Equals("Permission", StringComparison.OrdinalIgnoreCase))
            {
                return "permission";
            }
            if (requestType.Equals("Unpaid", StringComparison.OrdinalIgnoreCase))
            {
                return "unpaid";
            }
            // Work from home and External assignment don't affect balances
            
            // Return null if the type doesn't affect a known balance
            return null; 
        }

        // Uses Reject DTO for input
        public async Task<bool> RejectRequestAsync(Guid requestId, RequestRejectDto rejectDto, int rejecterId, string rejecterName, bool isHR)
        {
            try
            {
                var request = await _requestRepository.GetByIdAsync(requestId);
                if (request == null)
                    throw new KeyNotFoundException($"Request with ID {requestId} not found.");

                // Check current status
                string currentStatus = isHR ? request.RequestHRStatus : request.RequestStatus;
                if (currentStatus != "Pending")
                    throw new InvalidOperationException($"Request is not pending {(isHR ? "HR" : "Manager")} approval. Current status: {currentStatus}");

                // Update the status with rejection reason from DTO
                bool updated = await UpdateRequestStatus(
                    request, 
                    "Rejected", 
                    rejecterName, 
                    isHR, 
                    rejectDto.RejectReason);

                if (updated)
                {
                    // Notify user of rejection
                    await _notificationService.CreateNotificationAsync(
                        request.RequestUserID,
                        $"Your {request.RequestType} request has been rejected by {(isHR ? "HR" : "your manager")}. Reason: {rejectDto.RejectReason}");

                    // Notify other party (HR if manager rejected, Manager if HR rejected)
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting request {RequestId} by user {RejecterId}", requestId, rejecterId);
                throw;
            }
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
            return await _requestRepository.GetPendingDaysCountAsync(userId, requestType);
        }
        
        public async Task<bool> HasConflictingRequestsAsync(int userId, DateTime startDate, DateTime endDate, Guid requestId = default)
        {
             return await _requestRepository.HasConflictingRequestsAsync(userId, startDate, endDate, requestId);
        }

        #region Helper Methods

        private async Task EnsureUserCanModifyRequest(RequestEntity request, int userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                 throw new UnauthorizedAccessException("User not found.");

            // Allow if user is the owner OR if user is an Admin (check Role property)
            if (request.RequestUserID != userId && user.Role != "Admin") // Changed from user.Roles.Contains
            {
                 _logger.LogWarning("User {UserId} (Role: {Role}) attempted to modify request {RequestId} owned by {OwnerId}", userId, user.Role, request.Id, request.RequestUserID); // Changed from user.Roles
                throw new UnauthorizedAccessException("User is not authorized to modify this request.");
            }
        }
        
        private async Task<DateTime> ValidateRequestDates(DateTime startDate, DateTime? endDate, int userId, string requestType, Guid existingRequestId = default)
        {
            if (startDate.Date < DateTime.UtcNow.Date)
                throw new ArgumentException("Start date cannot be in the past.");

            DateTime effectiveEndDate = endDate ?? startDate;

            if (effectiveEndDate.Date < startDate.Date)
                throw new ArgumentException("End date cannot be before the start date.");
            
            // Check for conflicts only for leave types
            if (_leaveTypes.Contains(requestType) && await _requestRepository.HasConflictingRequestsAsync(userId, startDate, effectiveEndDate, existingRequestId))
            {
                throw new InvalidOperationException("There is a conflicting request during the selected dates.");
            }
            
            return effectiveEndDate;
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

        private async Task<bool> UpdateRequestStatus(RequestEntity request, string newStatus, string actorName, bool isHR, string? comment)
        {
             // Prevent updating already finalized requests
            if ((request.RequestStatus == "Approved" && request.RequestHRStatus == "Approved") ||
                request.RequestStatus == "Rejected" || request.RequestHRStatus == "Rejected")
            {
                 _logger.LogWarning("Attempted to update status of already finalized request {RequestId}", request.Id);
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
                    if (newStatus == "Approved")
                    {
                        request.ApprovedAt = DateTime.UtcNow;
                    }
                    else if (newStatus == "Rejected")
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
                    if (newStatus == "Approved")
                    {
                        // If HR already approved, then this is the final approval
                        if (request.RequestHRStatus == "Approved")
                        {
                            request.ApprovedAt = DateTime.UtcNow;
                        }
                    }
                    else if (newStatus == "Rejected")
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
        
        // ----- Mappers ----- 
        
        private async Task<RequestResponseDto> MapToResponseDto(RequestEntity? request)
        {
            if (request == null)
                return null; // Or throw appropriate exception

            // Fetch balances using the potentially updated method returning int dictionary
            Dictionary<string, int> balances = await GetLeaveBalancesAsync(request.RequestUserID);
            string? balanceType = GetBalanceTypeForRequest(request.RequestType);
            int? remainingBalance = null;

            if (balanceType != null && balances.TryGetValue(balanceType, out int balanceValue))
            {
                remainingBalance = balanceValue;
            }

            // Fetch user details for RequestUserFullName if needed (should already be on the request object)
            var user = await _userRepository.GetByIdAsync(request.RequestUserID);

            return new RequestResponseDto
            {
                Id = request.Id,
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
            if (paginatedRequests == null) return null;
            
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