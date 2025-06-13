using TDFShared.DTOs.Requests;
using TDFShared.DTOs.Users;
using TDFShared.Enums;
using TDFShared.Services;
using TDFShared.Utilities;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace TDFShared.Validation
{
    /// <summary>
    /// Comprehensive business rules validation service
    /// Centralizes all business logic validation across the application
    /// </summary>
    public class BusinessRulesService : IBusinessRulesService
    {
        private readonly IValidationService _validationService;

        public BusinessRulesService(IValidationService validationService)
        {
            _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
        }

        public async Task<BusinessRuleValidationResult> ValidateLeaveRequestAsync(
            RequestCreateDto request,
            int userId,
            BusinessRuleContext context)
        {
            context.ValidateForLeaveRequest();

            var errors = new List<string>();

            // Basic validation first
            var basicValidation = _validationService.ValidateObject(request);
            if (!basicValidation.IsValid)
            {
                errors.AddRange(basicValidation.Errors);
            }

            // Weekend validation - start date cannot be a weekend
            if (IsWeekend(request.RequestStartDate))
            {
                errors.Add("Start date cannot be on a weekend (Friday or Saturday). Please select a working day.");
            }

            // Check if the date range contains only weekends (for multi-day requests)
            if (request.RequestEndDate.HasValue && 
                request.RequestEndDate.Value.Date != request.RequestStartDate.Date &&
                CalculateWorkingDays(request.RequestStartDate, request.RequestEndDate.Value) == 0)
            {
                errors.Add("The selected date range contains only weekends. Please select working days.");
            }

            // Leave balance validation
            if (RequiresBalanceCheck(request.LeaveType))
            {
                var balanceResult = await ValidateLeaveBalanceAsync(userId, request.LeaveType,
                    CalculateRequestDays(request.RequestStartDate, request.RequestEndDate), context);
                if (!balanceResult.IsValid)
                {
                    errors.AddRange(balanceResult.Errors);
                }
            }

            // Conflict validation
            var conflictResult = await ValidateRequestConflictsAsync(userId, request.RequestStartDate,
                request.RequestEndDate, 0, context);
            if (!conflictResult.IsValid)
            {
                errors.AddRange(conflictResult.Errors);
            }

            return errors.Any()
                ? BusinessRuleValidationResult.Failure(errors)
                : BusinessRuleValidationResult.Success();
        }

        public async Task<BusinessRuleValidationResult> ValidateLeaveRequestUpdateAsync(
            RequestUpdateDto request,
            int requestId,
            int userId,
            BusinessRuleContext context)
        {
            context.ValidateForLeaveRequest();

            var errors = new List<string>();

            // Basic validation
            var basicValidation = _validationService.ValidateObject(request);
            if (!basicValidation.IsValid)
            {
                errors.AddRange(basicValidation.Errors);
            }

            // Weekend validation - start date cannot be a weekend
            if (IsWeekend(request.RequestStartDate))
            {
                errors.Add("Start date cannot be on a weekend (Friday or Saturday). Please select a working day.");
            }

            // Check if the date range contains only weekends (for multi-day requests)
            if (request.RequestEndDate.HasValue && 
                request.RequestEndDate.Value.Date != request.RequestStartDate.Date &&
                CalculateWorkingDays(request.RequestStartDate, request.RequestEndDate.Value) == 0)
            {
                errors.Add("The selected date range contains only weekends. Please select working days.");
            }

            // Get existing request to check if it can be modified
            if (context.GetRequestAsync != null)
            {
                var getRequestAsync = context.GetRequestAsync;
                var existingRequest = await getRequestAsync(requestId);
                if (existingRequest == null)
                {
                    errors.Add("Request not found.");
                }
                else if (existingRequest.Status != RequestStatus.Pending)
                {
                    errors.Add("Only pending requests can be modified.");
                }
            }

            // Conflict validation (excluding current request)
            var conflictResult = await ValidateRequestConflictsAsync(userId, request.RequestStartDate,
                request.RequestEndDate, requestId, context);
            if (!conflictResult.IsValid)
            {
                errors.AddRange(conflictResult.Errors);
            }

            return errors.Any()
                ? BusinessRuleValidationResult.Failure(errors)
                : BusinessRuleValidationResult.Success();
        }

        public async Task<BusinessRuleValidationResult> ValidateUserCreationAsync(
            CreateUserRequest user,
            BusinessRuleContext context)
        {
            context.ValidateForUserCreation();

            var errors = new List<string>();

            // Basic validation
            var basicValidation = _validationService.ValidateObject(user);
            if (!basicValidation.IsValid)
            {
                errors.AddRange(basicValidation.Errors);
            }

            // Username uniqueness
            if (context.UsernameExistsAsync != null)
            {
                var usernameExistsAsync = context.UsernameExistsAsync;
                bool usernameExists = await usernameExistsAsync(user.Username);
                if (usernameExists)
                {
                    errors.Add($"Username '{user.Username}' is already taken.");
                }
            }

            // Password validation
            var passwordResult = _validationService.ValidatePassword(user.Password);
            if (!passwordResult.IsValid)
            {
                errors.AddRange(passwordResult.Errors);
            }

            return errors.Any()
                ? BusinessRuleValidationResult.Failure(errors)
                : BusinessRuleValidationResult.Success();
        }

        public async Task<BusinessRuleValidationResult> ValidateRequestApprovalAsync(
            int requestId,
            int approverId,
            BusinessRuleContext context)
        {
            context.ValidateForRequestApproval();

            var errors = new List<string>();

            // Get request details
            if (context.GetRequestAsync == null)
            {
                return BusinessRuleValidationResult.Failure("Request validation not available.");
            }

            var getRequestAsync = context.GetRequestAsync;
            var request = await getRequestAsync(requestId);
            if (request == null)
            {
                return BusinessRuleValidationResult.Failure("Request not found.");
            }

            // Check if request is in approvable state
            if (request.Status != RequestStatus.Pending)
            {
                errors.Add("Only pending requests can be approved.");
            }

            // Get approver details
            if (context.GetUserAsync == null)
            {
                return BusinessRuleValidationResult.Failure("User validation not available.");
            }

            var getUserAsync = context.GetUserAsync;
            var approver = await getUserAsync(approverId);
            if (approver == null)
            {
                errors.Add("Approver not found.");
            }
            else
            {
                // Check if approver has permission (manager or admin)
                if (!(approver.IsManager ?? false) && !(approver.IsAdmin ?? false))
                {
                    errors.Add("Only managers and administrators can approve requests.");
                }

                // Check if approver is not the same as requester
                if (request.RequestUserID == approverId)
                {
                    errors.Add("Users cannot approve their own requests.");
                }
            }

            return errors.Any()
                ? BusinessRuleValidationResult.Failure(errors)
                : BusinessRuleValidationResult.Success();
        }

        public async Task<BusinessRuleValidationResult> ValidateLeaveBalanceAsync(
            int userId,
            LeaveType leaveType,
            int requestedDays,
            BusinessRuleContext context)
        {
            if (!RequiresBalanceCheck(leaveType))
            {
                return BusinessRuleValidationResult.Success();
            }

            if (context.GetLeaveBalanceAsync == null)
            {
                return BusinessRuleValidationResult.Failure("Leave balance validation not available.");
            }

            var getLeaveBalanceAsync = context.GetLeaveBalanceAsync;
            int availableBalance = await getLeaveBalanceAsync(userId, leaveType);

            if (availableBalance < requestedDays)
            {
                return BusinessRuleValidationResult.Failure(
                    $"Insufficient {leaveType} leave balance. Available: {availableBalance}, Requested: {requestedDays}");
            }

            var result = BusinessRuleValidationResult.Success();

            // Add warning if balance will be low after this request
            int remainingBalance = availableBalance - requestedDays;
            if (remainingBalance <= 2)
            {
                result.AddWarning($"This request will leave you with only {remainingBalance} {leaveType} days remaining.");
            }

            return result;
        }

        public async Task<BusinessRuleValidationResult> ValidateRequestConflictsAsync(
            int userId,
            DateTime startDate,
            DateTime? endDate,
            int excludeRequestId,
            BusinessRuleContext context)
        {
            if (context.HasConflictingRequestsAsync == null)
            {
                return BusinessRuleValidationResult.Success();
            }

            DateTime effectiveEndDate = endDate ?? startDate;

            var hasConflictingRequestsAsync = context.HasConflictingRequestsAsync;
            bool hasConflicts = await hasConflictingRequestsAsync(userId, startDate, effectiveEndDate, excludeRequestId);

            return hasConflicts
                ? BusinessRuleValidationResult.Failure("There is a conflicting request during the selected dates.")
                : BusinessRuleValidationResult.Success();
        }

        public async Task<BusinessRuleValidationResult> ValidateDepartmentRulesAsync(
            string departmentId,
            DateTime startDate,
            DateTime? endDate,
            BusinessRuleContext context)
        {
            if (context.GetDepartmentRequestCountAsync == null)
            {
                return BusinessRuleValidationResult.Success();
            }

            DateTime effectiveEndDate = endDate ?? startDate;
            var getDepartmentRequestCountAsync = context.GetDepartmentRequestCountAsync;
            int concurrentRequests = await getDepartmentRequestCountAsync(departmentId, startDate, effectiveEndDate);

            if (concurrentRequests >= context.MaxConcurrentDepartmentRequests)
            {
                return BusinessRuleValidationResult.Failure(
                    $"Department has reached the maximum of {context.MaxConcurrentDepartmentRequests} concurrent requests for this period.");
            }

            return BusinessRuleValidationResult.Success();
        }

        public async Task<BusinessRuleValidationResult> ValidateRequestAccessAsync(
            int requestId,
            int userId,
            BusinessRuleContext context)
        {
            if (context.GetRequestAsync == null || context.GetUserAsync == null)
            {
                return BusinessRuleValidationResult.Failure("Required validation dependencies not available.");
            }

            var getRequestAsync = context.GetRequestAsync;
            var request = await getRequestAsync(requestId);
            if (request == null)
            {
                return BusinessRuleValidationResult.Failure("Request not found.");
            }

            var getUserAsync = context.GetUserAsync;
            var user = await getUserAsync(userId);
            if (user == null)
            {
                return BusinessRuleValidationResult.Failure("User not found.");
            }

            bool canAccess = RequestStateManager.CanViewRequest(request, user);

            return canAccess
                ? BusinessRuleValidationResult.Success()
                : BusinessRuleValidationResult.Failure("You do not have permission to access this request.");
        }

        public async Task<BusinessRuleValidationResult> ValidateDepartmentAccessAsync(
            string department,
            int userId,
            BusinessRuleContext context)
        {
            if (context.GetUserAsync == null)
            {
                return BusinessRuleValidationResult.Failure("Required validation dependencies not available.");
            }

            var getUserAsync = context.GetUserAsync;
            var user = await getUserAsync(userId);
            if (user == null)
            {
                return BusinessRuleValidationResult.Failure("User not found.");
            }

            bool canAccess = AuthorizationUtilities.CanAccessDepartment(user, department);

            return canAccess
                ? BusinessRuleValidationResult.Success()
                : BusinessRuleValidationResult.Failure($"You do not have permission to access department '{department}'.");
        }

        // Helper methods
        private static bool RequiresBalanceCheck(LeaveType leaveType)
        {
            return leaveType == LeaveType.Annual || leaveType == LeaveType.Emergency;
        }

        private static int CalculateRequestDays(DateTime startDate, DateTime? endDate)
        {
            DateTime effectiveEndDate = endDate ?? startDate;
            return CalculateWorkingDays(startDate.Date, effectiveEndDate.Date);
        }

        /// <summary>
        /// Calculates the number of working days (excluding weekends) between two dates
        /// Weekends are Friday and Saturday
        /// </summary>
        /// <param name="startDate">Start date (inclusive)</param>
        /// <param name="endDate">End date (inclusive)</param>
        /// <returns>Number of working days</returns>
        private static int CalculateWorkingDays(DateTime startDate, DateTime endDate)
        {
            if (endDate < startDate)
                return 0;

            int workingDays = 0;
            var current = startDate;

            while (current <= endDate)
            {
                // Friday = 5, Saturday = 6 in DayOfWeek enum
                if (current.DayOfWeek != DayOfWeek.Friday && current.DayOfWeek != DayOfWeek.Saturday)
                {
                    workingDays++;
                }
                current = current.AddDays(1);
            }

            return workingDays;
        }

        /// <summary>
        /// Checks if a date falls on a weekend (Friday or Saturday)
        /// </summary>
        /// <param name="date">The date to check</param>
        /// <returns>True if the date is Friday or Saturday</returns>
        private static bool IsWeekend(DateTime date)
        {
            return date.DayOfWeek == DayOfWeek.Friday || date.DayOfWeek == DayOfWeek.Saturday;
        }
    }
}
