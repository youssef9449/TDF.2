using System;
using TDFShared.Enums;
using TDFShared.DTOs.Requests;

namespace TDFShared.Factories
{
    public static class RequestDtoFactory
    {
        public static RequestCreateDto CreateRequestDto(
            LeaveType leaveType,
            DateTime startDate,
            DateTime? endDate,
            TimeSpan? startTime,
            TimeSpan? endTime,
            string reason,
            int userId)
        {
            return new RequestCreateDto
            {
                LeaveType = leaveType,
                RequestStartDate = startDate,
                RequestEndDate = endDate ?? startDate,
                RequestBeginningTime = ShouldIncludeTime(leaveType) ? startTime : null,
                RequestEndingTime = ShouldIncludeTime(leaveType) ? endTime : null,
                RequestReason = reason,
                UserId = userId
            };
        }

        public static RequestUpdateDto CreateUpdateDto(
            LeaveType leaveType,
            DateTime startDate,
            DateTime? endDate,
            TimeSpan? startTime,
            TimeSpan? endTime,
            string reason)
        {
            return new RequestUpdateDto
            {
                LeaveType = leaveType,
                RequestStartDate = startDate,
                RequestEndDate = endDate ?? startDate,
                RequestBeginningTime = ShouldIncludeTime(leaveType) ? startTime : null,
                RequestEndingTime = ShouldIncludeTime(leaveType) ? endTime : null,
                RequestReason = reason
            };
        }

        private static bool ShouldIncludeTime(LeaveType leaveType)
        {
            return leaveType == LeaveType.Permission || leaveType == LeaveType.ExternalAssignment;
        }
    }
}
