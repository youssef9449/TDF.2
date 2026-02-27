using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TDFShared.DTOs.Messages;
using TDFShared.Enums;

namespace TDFMAUI.Services
{
    public sealed class NoOpPushNotificationService : IPushNotificationService
    {
        public event EventHandler<NotificationEventArgs> NotificationReceived = delegate { };

        public Task<bool> RegisterTokenAsync() => Task.FromResult(false);
        public Task<bool> UnregisterTokenAsync() => Task.FromResult(false);
        public Task<bool> ShowLocalNotificationAsync(string title, string message, NotificationType type = NotificationType.Info, string? data = null) => Task.FromResult(false);
        public Task<List<NotificationRecord>> GetNotificationHistoryAsync() => Task.FromResult(new List<NotificationRecord>());
        public Task<bool> ClearNotificationHistoryAsync() => Task.FromResult(true);
        public Task<bool> ScheduleNotificationAsync(string title, string message, DateTime deliveryTime, string? data = null) => Task.FromResult(false);
        public Task<bool> CancelScheduledNotificationAsync(string notificationId) => Task.FromResult(false);
        public Task<IEnumerable<string>> GetScheduledNotificationIdsAsync() => Task.FromResult<IEnumerable<string>>(Array.Empty<string>());
    }
}
