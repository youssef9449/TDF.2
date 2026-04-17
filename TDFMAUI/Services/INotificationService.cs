using System;
using TDFShared.DTOs.Messages;
using TDFShared.Models.Notification;

namespace TDFMAUI.Services
{
    public interface INotificationService
    {
        event EventHandler<NotificationDto> NotificationReceived;
        Task<IEnumerable<NotificationEntity>> GetUnreadNotificationsAsync();
        Task<bool> MarkAsSeenAsync(int notificationId);
        Task<bool> MarkNotificationsAsSeenAsync(IEnumerable<int> notificationIds);
        Task<bool> BroadcastNotificationAsync(string message, string? department = null);
        Task<bool> MarkNotificationsAsSeenAsync(int senderId);
        
        Task ShowErrorAsync(string message);
        Task ShowSuccessAsync(string message);
        Task ShowWarningAsync(string message);
        Task<bool> DeleteNotificationAsync(int notificationId);
    }
}
