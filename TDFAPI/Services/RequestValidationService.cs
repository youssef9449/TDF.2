using System;
using System.Collections.Generic;
using TDFShared.DTOs.Requests;
using TDFShared.Enums;
using TDFShared.Exceptions;

namespace TDFAPI.Services
{
    public class RequestValidationService
    {
        public static void ValidateRequest(RequestCreateDto request)
        {
            var errors = new List<string>();

            if (request.EndDate < request.StartDate)
            {
                throw new ValidationException("End date must be after start date");
            }

            // Validate time range for Permission leave
            if (request.LeaveType == LeaveType.Permission)
            {
                if (!request.RequestBeginningTime.HasValue || !request.RequestEndingTime.HasValue)
                {
                    throw new ValidationException("Beginning and ending times are required for Permission leave");
                }

                if (request.RequestEndingTime <= request.RequestBeginningTime)
                {
                    throw new ValidationException("Ending time must be after beginning time for Permission leave");
                }
            }

            // Work From Home: only allow full days
            if (request.LeaveType == LeaveType.WorkFromHome)
            {
                if (request.RequestBeginningTime.HasValue || request.RequestEndingTime.HasValue)
                {
                    throw new ValidationException("Work From Home leave cannot have specific times; only full days are allowed");
                }
            }

            // Annual Leave: validate against available balance
            if (request.LeaveType == LeaveType.Annual)
            {
                ValidateLeaveBalance(request);
            }
        }

        public static void ValidateLeaveBalance(RequestCreateDto request)
        {
            // This would typically involve checking the user's leave balance
            // and validating against the requested days
            // Implementation depends on your leave balance tracking system
        }

        public static void ValidateRequestUpdate(RequestUpdateDto request)
        {
            if (request.EndDate < request.StartDate)
            {
                throw new ValidationException("End date must be after start date");
            }

            // Similar validations as for create, but specific to updates
            if (request.LeaveType == LeaveType.Permission)
            {
                if (!request.RequestBeginningTime.HasValue || !request.RequestEndingTime.HasValue)
                {
                    throw new ValidationException("Beginning and ending times are required for Permission leave");
                }

                if (request.RequestEndingTime <= request.RequestBeginningTime)
                {
                    throw new ValidationException("Ending time must be after beginning time for Permission leave");
                }
            }

            if (request.LeaveType == LeaveType.WorkFromHome)
            {
                if (request.RequestBeginningTime.HasValue || request.RequestEndingTime.HasValue)
                {
                    throw new ValidationException("Work From Home leave cannot have specific times; only full days are allowed");
                }
            }
        }
    }
}
