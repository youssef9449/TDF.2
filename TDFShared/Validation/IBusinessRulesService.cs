using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TDFShared.DTOs.Requests;
using TDFShared.DTOs.Users;
using TDFShared.Enums;

namespace TDFShared.Validation
{
    /// <summary>
    /// Interface for business rules validation
    /// Centralizes all business logic validation across the application
    /// </summary>
    public interface IBusinessRulesService
    {
        /// <summary>
        /// Validates leave request business rules
        /// </summary>
        /// <param name="request">Request to validate</param>
        /// <param name="userId">User making the request</param>
        /// <param name="context">Validation context with dependencies</param>
        /// <returns>Business rule validation result</returns>
        Task<BusinessRuleValidationResult> ValidateLeaveRequestAsync(
            RequestCreateDto request,
            int userId,
            BusinessRuleContext context);

        /// <summary>
        /// Validates leave request update business rules
        /// </summary>
        /// <param name="request">Request update to validate</param>
        /// <param name="requestId">ID of the request being updated</param>
        /// <param name="userId">User making the update</param>
        /// <param name="context">Validation context with dependencies</param>
        /// <returns>Business rule validation result</returns>
        Task<BusinessRuleValidationResult> ValidateLeaveRequestUpdateAsync(
            RequestUpdateDto request,
            int requestId,
            int userId,
            BusinessRuleContext context);

        /// <summary>
        /// Validates user creation business rules
        /// </summary>
        /// <param name="user">User to validate</param>
        /// <param name="context">Validation context with dependencies</param>
        /// <returns>Business rule validation result</returns>
        Task<BusinessRuleValidationResult> ValidateUserCreationAsync(
            CreateUserRequest user,
            BusinessRuleContext context);

        /// <summary>
        /// Validates request approval business rules
        /// </summary>
        /// <param name="requestId">Request ID to approve</param>
        /// <param name="approverId">User performing the approval</param>
        /// <param name="context">Validation context with dependencies</param>
        /// <returns>Business rule validation result</returns>
        Task<BusinessRuleValidationResult> ValidateRequestApprovalAsync(
            int requestId,
            int approverId,
            BusinessRuleContext context);

        /// <summary>
        /// Validates leave balance constraints
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="leaveType">Type of leave</param>
        /// <param name="requestedDays">Number of days requested</param>
        /// <param name="context">Validation context with dependencies</param>
        /// <returns>Business rule validation result</returns>
        Task<BusinessRuleValidationResult> ValidateLeaveBalanceAsync(
            int userId,
            LeaveType leaveType,
            int requestedDays,
            BusinessRuleContext context);

        /// <summary>
        /// Validates request conflicts with existing requests
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="startDate">Start date</param>
        /// <param name="endDate">End date</param>
        /// <param name="excludeRequestId">Request ID to exclude from conflict check</param>
        /// <param name="context">Validation context with dependencies</param>
        /// <returns>Business rule validation result</returns>
        Task<BusinessRuleValidationResult> ValidateRequestConflictsAsync(
            int userId,
            DateTime startDate,
            DateTime? endDate,
            int excludeRequestId,
            BusinessRuleContext context);

        /// <summary>
        /// Validates department-specific business rules
        /// </summary>
        /// <param name="departmentId">Department ID</param>
        /// <param name="startDate">Start date</param>
        /// <param name="endDate">End date</param>
        /// <param name="context">Validation context with dependencies</param>
        /// <returns>Business rule validation result</returns>
        Task<BusinessRuleValidationResult> ValidateDepartmentRulesAsync(
            string departmentId,
            DateTime startDate,
            DateTime? endDate,
            BusinessRuleContext context);

        /// <summary>
        /// Validates authorization for request access
        /// </summary>
        /// <param name="requestId">Request ID to access</param>
        /// <param name="userId">User attempting access</param>
        /// <param name="context">Validation context with dependencies</param>
        /// <returns>Business rule validation result</returns>
        Task<BusinessRuleValidationResult> ValidateRequestAccessAsync(
            int requestId,
            int userId,
            BusinessRuleContext context);

        /// <summary>
        /// Validates authorization for department access
        /// </summary>
        /// <param name="department">Department to access</param>
        /// <param name="userId">User attempting access</param>
        /// <param name="context">Validation context with dependencies</param>
        /// <returns>Business rule validation result</returns>
        Task<BusinessRuleValidationResult> ValidateDepartmentAccessAsync(
            string department,
            int userId,
            BusinessRuleContext context);
    }

    /// <summary>
    /// Business rule validation result
    /// </summary>
    public class BusinessRuleValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();

        public static BusinessRuleValidationResult Success() => new()
        {
            IsValid = true
        };

        public static BusinessRuleValidationResult Failure(string error) => new()
        {
            IsValid = false,
            Errors = new List<string> { error }
        };

        public static BusinessRuleValidationResult Failure(List<string> errors) => new()
        {
            IsValid = false,
            Errors = errors
        };

        public BusinessRuleValidationResult AddWarning(string warning)
        {
            Warnings.Add(warning);
            return this;
        }

        public BusinessRuleValidationResult AddMetadata(string key, object value)
        {
            Metadata[key] = value;
            return this;
        }
    }

    /// <summary>
    /// Context for business rule validation containing dependencies
    /// </summary>
    public class BusinessRuleContext
    {
        // Delegates for data access - allows flexibility in implementation
        public Func<int, LeaveType, Task<int>>? GetLeaveBalanceAsync { get; set; }
        public Func<int, DateTime, DateTime, int, Task<bool>>? HasConflictingRequestsAsync { get; set; }
        public Func<string, Task<bool>>? UsernameExistsAsync { get; set; }
        public Func<int, Task<RequestResponseDto?>>? GetRequestAsync { get; set; }
        public Func<string, DateTime, DateTime, Task<int>>? GetDepartmentRequestCountAsync { get; set; }
        public Func<int, Task<UserDto?>>? GetUserAsync { get; set; }

        // Configuration values
        public int MaxConcurrentDepartmentRequests { get; set; } = 3;
        public int MinAdvanceNoticeDays { get; set; } = 1;
        public int MaxRequestDurationDays { get; set; } = 30;
        public bool AllowWeekendRequests { get; set; } = true;
        public bool AllowHolidayRequests { get; set; } = false;

        // Helper methods to validate context
        public void ValidateForLeaveRequest()
        {
            if (GetLeaveBalanceAsync == null)
                throw new InvalidOperationException("GetLeaveBalanceAsync delegate is required for leave request validation");
            if (HasConflictingRequestsAsync == null)
                throw new InvalidOperationException("HasConflictingRequestsAsync delegate is required for leave request validation");
        }

        public void ValidateForUserCreation()
        {
            if (UsernameExistsAsync == null)
                throw new InvalidOperationException("UsernameExistsAsync delegate is required for user creation validation");
        }

        public void ValidateForRequestApproval()
        {
            if (GetRequestAsync == null)
                throw new InvalidOperationException("GetRequestAsync delegate is required for request approval validation");
            if (GetUserAsync == null)
                throw new InvalidOperationException("GetUserAsync delegate is required for request approval validation");
        }
    }
}
