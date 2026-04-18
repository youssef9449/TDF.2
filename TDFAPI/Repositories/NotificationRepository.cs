using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TDFAPI.Data;
using TDFShared.Models.Notification;

namespace TDFAPI.Repositories
{
    /// <summary>
    /// EF Core is the single data-access stack for the <c>Notifications</c> table.
    /// The previous implementation kept a silent ADO.NET fallback that hid EF
    /// errors and diverged over time (the fallback wrote <c>MessageText</c> while
    /// EF maps <c>Message</c>). Any EF exception now propagates to the caller.
    /// </summary>
    public sealed class NotificationRepository : INotificationRepository
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<NotificationRepository> _logger;

        public NotificationRepository(
            ApplicationDbContext dbContext,
            ILogger<NotificationRepository> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IEnumerable<NotificationEntity>> GetUnreadNotificationsAsync(int userId) =>
            await _dbContext.Notifications
                .Where(n => n.ReceiverID == userId && !n.IsSeen)
                .OrderByDescending(n => n.Timestamp)
                .ToListAsync();

        public async Task<int> CreateNotificationAsync(NotificationEntity notification)
        {
            _dbContext.Notifications.Add(notification);
            await _dbContext.SaveChangesAsync();
            return notification.NotificationID;
        }

        public async Task<bool> MarkNotificationAsSeenAsync(int notificationId, int userId)
        {
            var rows = await _dbContext.Notifications
                .Where(n => n.NotificationID == notificationId && n.ReceiverID == userId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.IsSeen, true));
            return rows > 0;
        }

        public async Task<bool> DeleteNotificationAsync(int notificationId, int userId)
        {
            var rows = await _dbContext.Notifications
                .Where(n => n.NotificationID == notificationId && n.ReceiverID == userId)
                .ExecuteDeleteAsync();
            return rows > 0;
        }
    }
}
