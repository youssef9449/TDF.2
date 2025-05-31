using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TDFShared.DTOs.Common;

namespace TDFShared.Services
{
    /// <summary>
    /// Defines methods for scheduling and managing scheduled notifications.
    /// </summary>
    public interface IScheduledNotificationService
    {
        /// <summary>
        /// Schedules a notification to be delivered at a specific time.
        /// </summary>
        /// <param name="dto">The notification schedule details.</param>
        /// <returns>True if the notification was scheduled successfully.</returns>
        Task<bool> ScheduleAsync(NotificationScheduleDto dto);

        /// <summary>
        /// Cancels a scheduled notification.
        /// </summary>
        /// <param name="scheduleId">The unique identifier of the scheduled notification.</param>
        /// <returns>True if the notification was canceled successfully.</returns>
        Task<bool> CancelScheduledAsync(Guid scheduleId);

        /// <summary>
        /// Gets all scheduled notifications for a user.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <returns>A list of scheduled notifications.</returns>
        Task<IEnumerable<NotificationScheduleDto>> GetScheduledAsync(int userId);
    }
} 