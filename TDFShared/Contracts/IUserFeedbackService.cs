using System.Threading.Tasks;

namespace TDFShared.Contracts
{
    /// <summary>
    /// Displays transient user-facing feedback (toasts, alerts, dialogs) on the client.
    /// Intended to be implemented only by UI-capable hosts (MAUI). The server has no
    /// meaningful implementation for these methods, which is why they are kept off
    /// <see cref="TDFShared.Services.INotificationService"/>.
    /// </summary>
    public interface IUserFeedbackService
    {
        /// <summary>
        /// Shows an error message to the user.
        /// </summary>
        Task ShowErrorAsync(string message);

        /// <summary>
        /// Shows a success message to the user.
        /// </summary>
        Task ShowSuccessAsync(string message);

        /// <summary>
        /// Shows a warning message to the user.
        /// </summary>
        Task ShowWarningAsync(string message);
    }
}
