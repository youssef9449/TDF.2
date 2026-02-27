using TDFShared.Enums;
using TDFShared.DTOs.Users;
using TDFShared.Services;

namespace TDFMAUI.Services
{
    /// <summary>
    /// Service to manage user presence status and availability
    /// </summary>
    public interface IUserPresenceService : TDFShared.Services.IUserPresenceService
    {
        /// <summary>
        /// Gets cached information about online users for offline usage
        /// </summary>
        Dictionary<int, UserPresenceInfo> GetCachedOnlineUsers();

        /// <summary>
        /// Updates the current user's status and optional status message
        /// </summary>
        Task UpdateStatusAsync(UserPresenceStatus status, string? statusMessage = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets whether the current user is available for chat
        /// </summary>
        Task SetAvailabilityForChatAsync(bool isAvailable);

        /// <summary>
        /// Parses a string status name to the enum value
        /// </summary>
        UserPresenceStatus ParseStatus(string status);

        /// <summary>
        /// Fired when current user's availability change is confirmed
        /// </summary>
        event EventHandler<AvailabilitySetEventArgs> AvailabilityConfirmed;

        /// <summary>
        /// Fired when current user's status update is confirmed
        /// </summary>
        event EventHandler<StatusUpdateConfirmedEventArgs> StatusUpdateConfirmed;

        /// <summary>
        /// Fired when a presence-related error occurs
        /// </summary>
        event EventHandler<WebSocketErrorEventArgs> PresenceErrorReceived;
    }
}
