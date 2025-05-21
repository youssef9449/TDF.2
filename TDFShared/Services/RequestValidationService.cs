using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TDFShared.Enums;
using TDFShared.DTOs.Requests;
using TDFShared.Exceptions;

namespace TDFShared.Services
{
    /// <summary>
    /// Delegate for checking if there are conflicting requests for a date range.
    /// </summary>
    /// <param name="userId">The ID of the user making the request.</param>
    /// <param name="startDate">The start date of the request.</param>
    /// <param name="endDate">The end date of the request.</param>
    /// <param name="ignoredRequestId">Optional ID of a request to ignore in the check (e.g. when updating).</param>
    /// <returns>True if there are conflicts, false otherwise.</returns>
    public delegate Task<bool> ConflictingRequestsDelegate(int userId, DateTime startDate, DateTime endDate, int ignoredRequestId = 0);

    /// <summary>
    /// Centralized validation service for requests. Contains all business rules and validation logic.
    /// </summary>
    public static class RequestValidationService
    {
        private static readonly Dictionary<LeaveType, bool> RequiresTime = new()
        {
            { LeaveType.Permission, true },
            { LeaveType.ExternalAssignment, true },
        };

        /// <summary>
        /// Validates a leave request's fields based on type-specific rules.
        /// </summary>
        public static List<string> ValidateRequest(DateTime startDate, DateTime? endDate, LeaveType leaveType, TimeSpan? startTime = null, TimeSpan? endTime = null)
        {
            var errors = new List<string>();
            errors.AddRange(ValidateRequestDates(startDate, endDate));
            errors.AddRange(ValidateRequestTime(leaveType, startDate, endDate, startTime, endTime));
            return errors;
        }

        /// <summary>
        /// Validates time fields for a request based on leave type.
        /// </summary>
        private static List<string> ValidateRequestTime(LeaveType leaveType, DateTime startDate, DateTime? endDate, TimeSpan? beginningTime, TimeSpan? endingTime)
        {
            var errors = new List<string>();
            bool typeRequiresTime = RequiresTime.TryGetValue(leaveType, out bool requiresTime) ? requiresTime : false;

            // Time validation for Permission and External Assignment
            if (typeRequiresTime)
            {
                if (!beginningTime.HasValue || !endingTime.HasValue)
                {
                    errors.Add($"{leaveType} requires both beginning and ending times.");
                }
                else if (endingTime <= beginningTime)
                {
                    errors.Add("Ending time must be after beginning time.");
                }

                // For Permission and External Assignment, must be same day
                if (endDate.HasValue && endDate.Value.Date != startDate.Date)
                {
                    errors.Add($"{leaveType} must start and end on the same day.");
                }
            }
            // For leaves that don't use time
            else if (beginningTime.HasValue || endingTime.HasValue)
            {
                errors.Add($"{leaveType} cannot have specific times, only full days are allowed.");
            }

            return errors;
        }

        /// <summary>
        /// Validates basic date fields for a request.
        /// </summary>
        private static List<string> ValidateRequestDates(DateTime startDate, DateTime? endDate)
        {
            var errors = new List<string>();

            // Start date validation
            if (startDate.Date < DateTime.Today)
            {
                errors.Add("Start date cannot be in the past.");
            }

            // End date validation
            if (endDate.HasValue && endDate.Value.Date < startDate.Date)
            {
                errors.Add("End date cannot be before the start date.");
            }

            return errors;
        }        /// <summary>
        /// Validates a request creation DTO.
        /// </summary>
        public static void ValidateCreateDto(RequestCreateDto request)
        {
            if (request == null) throw new ValidationException("Request data is required.");

            var errors = ValidateRequest(
                request.RequestStartDate,
                request.RequestEndDate,
                request.LeaveType,
                request.RequestBeginningTime,
                request.RequestEndingTime
            );

            if (errors.Count > 0)
            {
                throw new ValidationException(string.Join("; ", errors));
            }
        }

        /// <summary>
        /// Validates a request update DTO.
        /// </summary>
        public static void ValidateUpdateDto(RequestUpdateDto request)
        {
            if (request == null) throw new ValidationException("Request data is required.");

            var errors = ValidateRequest(
                request.RequestStartDate,
                request.RequestEndDate,
                request.LeaveType,
                request.RequestBeginningTime,
                request.RequestEndingTime
            );

            if (errors.Count > 0)
            {
                throw new ValidationException(string.Join("; ", errors));
            }
        }

        /// <summary>
        /// Validates request conflicts against existing requests.
        /// </summary>
        public static async Task ValidateConflictingRequests(DateTime startDate, DateTime? endDate, int userId, ConflictingRequestsDelegate hasConflictingRequests, int existingRequestId = 0)
        {
            DateTime effectiveEndDate = endDate ?? startDate;
            
            if (await hasConflictingRequests(userId, startDate, effectiveEndDate, existingRequestId))
            {
                throw new BusinessRuleException("There is a conflicting request during the selected dates.");
            }
        }
    }
}
