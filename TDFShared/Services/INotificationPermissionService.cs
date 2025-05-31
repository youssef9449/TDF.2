using System.Threading.Tasks;
using TDFShared.Enums;

namespace TDFShared.Services
{
    /// <summary>
    /// Defines methods for requesting and checking notification permissions on a device.
    /// </summary>
    public interface INotificationPermissionService
    {
        /// <summary>
        /// Requests notification permission from the user.
        /// </summary>
        /// <returns>The resulting permission status.</returns>
        Task<NotificationPermissionStatus> RequestPermissionAsync();

        /// <summary>
        /// Gets the current notification permission status.
        /// </summary>
        /// <returns>The current permission status.</returns>
        Task<NotificationPermissionStatus> GetPermissionStatusAsync();
    }
} 