using System;
using System.Threading.Tasks;
using TDFShared.DTOs.Requests;
using TDFShared.Enums;
using TDFShared.Exceptions;

namespace TDFShared.Services
{
    /// <summary>
    /// Provides business rules and validation for request operations
    /// </summary>
    public static class RequestBusinessRuleService
    {
        /// <summary>
        /// Checks if a request is in a state that can be approved.
        /// </summary>
        public static bool CanApprove(RequestStatus currentStatus, RequestStatus hrStatus, bool isHR)
        {
            return isHR ? 
                currentStatus == RequestStatus.Approved && hrStatus == RequestStatus.Pending :
                currentStatus == RequestStatus.Pending;
        }

        /// <summary>
        /// Checks if a request is in a state that can be rejected.
        /// </summary>
        public static bool CanReject(RequestStatus currentStatus, RequestStatus hrStatus, bool isHR)
        {
            return isHR ? 
                hrStatus == RequestStatus.Pending :
                currentStatus == RequestStatus.Pending;
        }

        /// <summary>
        /// Checks if a request can be edited based on its current status.
        /// </summary>
        public static bool CanEdit(RequestStatus currentStatus, RequestStatus hrStatus)
        {
            return currentStatus == RequestStatus.Pending && hrStatus == RequestStatus.Pending;
        }

        /// <summary>
        /// Checks if a request can be deleted based on its current status.
        /// </summary>
        public static bool CanDelete(RequestStatus currentStatus, RequestStatus hrStatus)
        {
            return currentStatus == RequestStatus.Pending && hrStatus == RequestStatus.Pending;
        }

        /// <summary>
        /// Calculates the number of business days between two dates.
        /// </summary>
        public static int CalculateBusinessDays(DateTime startDate, DateTime? endDate)
        {
            DateTime end = endDate ?? startDate;
            int days = 0;
            for (var date = startDate.Date; date <= end.Date; date = date.AddDays(1))
            {
                if (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday)
                {
                    days++;
                }
            }
            return days;
        }

        /// <summary>
        /// Validates and updates leave balance for request approval
        /// </summary>
        public static async Task<bool> ValidateAndUpdateBalance(
            int userId, 
            LeaveType leaveType, 
            int requestedDays,
            Func<int, LeaveType, int, Task<bool>> updateBalanceAsync)
        {
            if (!RequiresBalance(leaveType)) return true;
            return await updateBalanceAsync(userId, leaveType, requestedDays);
        }

        /// <summary>
        /// Checks if a request will be fully approved after this action
        /// </summary>
        public static bool WillBeFullyApproved(RequestStatus currentStatus, RequestStatus hrStatus, bool isHR)
        {
            return (isHR && currentStatus == RequestStatus.Approved) ||
                   (!isHR && hrStatus == RequestStatus.Approved);
        }

        /// <summary>
        /// Gets the balance type for a leave type
        /// </summary>
        public static string? GetBalanceType(LeaveType leaveType)
        {
            return leaveType switch
            {
                LeaveType.Annual => "annual",
                LeaveType.Emergency => "casual",
                LeaveType.Permission => "permission",
                LeaveType.Unpaid => "unpaid",
                _ => null // ExternalAssignment and WorkFromHome do not affect balances
            };
        }

        /// <summary>
        /// Determines if a leave type requires balance tracking
        /// </summary>
        public static bool RequiresBalance(LeaveType leaveType)
        {
            return leaveType switch
            {
                LeaveType.Annual => true,
                LeaveType.Emergency => true,
                LeaveType.Permission => true,
                _ => false
            };
        }

        /// <summary>
        /// Validates request conflicts against existing requests
        /// </summary>
        public static async Task ValidateConflicts(
            int userId, 
            DateTime startDate, 
            DateTime? endDate,
            Func<int, DateTime, DateTime, int, Task<bool>> hasConflictsAsync,
            int excludeRequestId = 0)
        {
            DateTime effectiveEndDate = endDate ?? startDate;
            
            if (await hasConflictsAsync(userId, startDate, effectiveEndDate, excludeRequestId))
            {
                throw new BusinessRuleException("Another request exists for the specified date range");
            }
        }
    }
}
