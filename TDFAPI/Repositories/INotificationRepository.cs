using System.Collections.Generic;
using System.Threading.Tasks;
using TDFShared.Models.Notification;


namespace TDFAPI.Repositories
{
    public interface INotificationRepository
    {
        Task<IEnumerable<NotificationEntity>> GetUnreadNotificationsAsync(int userId);
        Task<int> CreateNotificationAsync(NotificationEntity notification);
        Task<bool> MarkNotificationAsSeenAsync(int notificationId, int userId);
    }
}