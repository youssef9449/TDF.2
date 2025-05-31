using System.Threading.Tasks;

namespace TDFMAUI.Services
{
    /// <summary>
    /// Platform-specific notification permission service interface.
    /// </summary>
    public interface INotificationPermissionPlatformService
    {
        /// <summary>
        /// Requests notification permission from the user (platform-specific).
        /// </summary>
        /// <returns>True if permission is granted, false otherwise.</returns>
        Task<bool> RequestPlatformNotificationPermissionAsync();
    }
} 