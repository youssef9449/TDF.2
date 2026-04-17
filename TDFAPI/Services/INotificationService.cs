using System.Collections.Generic;
using System.Threading.Tasks;
using TDFShared.DTOs.Messages;
using TDFShared.Enums;
using TDFShared.Models.Notification;

namespace TDFAPI.Services
{
    public interface INotificationService
    {
        Task<IEnumerable<NotificationEntity>> GetUnreadNotificationsAsync(int userId);
        Task<bool> MarkAsSeenAsync(int notificationId, int userId);
        Task<bool> MarkNotificationsAsSeenAsync(IEnumerable<int> notificationIds, int userId);
        Task<bool> CreateNotificationAsync(int receiverId, string message, int? senderId = null);
        Task SendNotificationAsync(int userId, string title, string message, NotificationType type = NotificationType.Info, string? data = null);
        Task SendNotificationAsync(IEnumerable<int> userIds, string title, string message, NotificationType type = NotificationType.Info, string? data = null);
        Task SendDepartmentNotificationAsync(string department, string title, string message, NotificationType type = NotificationType.Info, string? data = null);
        Task ScheduleNotificationAsync(int userId, string title, string message, DateTime deliveryTime, NotificationType type = NotificationType.Info, string? data = null);
        Task CancelScheduledNotificationAsync(string notificationId);
        Task<IEnumerable<NotificationRecord>> GetScheduledNotificationsAsync(int userId);
        Task<bool> DeleteNotificationAsync(int notificationId, int userId);
    }
}
