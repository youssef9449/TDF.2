using TDFShared.Enums;
using Microsoft.Maui.Graphics;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;

namespace TDFMAUI.ViewModels
{
    /// <summary>
    /// View model representing a user and their online presence status
    /// </summary>
    public partial class UserViewModel : ObservableObject
    {
        [ObservableProperty]
        private int _userId;

        [ObservableProperty]
        private string _username = string.Empty;

        [ObservableProperty]
        private string _fullName = string.Empty;

        [ObservableProperty]
        private string _department = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusColor))]
        [NotifyPropertyChangedFor(nameof(StatusDisplay))]
        private UserPresenceStatus _status = UserPresenceStatus.Offline;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasStatusMessage))]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private bool _isAvailableForChat;

        [ObservableProperty]
        private byte[]? _profilePictureData;

        /// <summary>
        /// Indicates if the user has set a custom status message
        /// </summary>
        public bool HasStatusMessage => !string.IsNullOrEmpty(StatusMessage);

        /// <summary>
        /// Provides a color based on the user's presence status.
        /// </summary>
        public static Color GetColorForStatus(UserPresenceStatus status)
        {
            return status switch
            {
                UserPresenceStatus.Online => Helpers.ThemeHelper.GetThemeResource<Color>("SuccessColor"),
                UserPresenceStatus.Away => Helpers.ThemeHelper.GetThemeResource<Color>("WarningColor"),
                UserPresenceStatus.Busy => Helpers.ThemeHelper.GetThemeResource<Color>("ErrorColor"),
                UserPresenceStatus.DoNotDisturb => Color.FromArgb("#8B0000"), // DarkRed fallback
                UserPresenceStatus.Offline => Helpers.ThemeHelper.GetThemeResource<Color>("TextSecondaryColor"),
                _ => Helpers.ThemeHelper.GetThemeResource<Color>("TextSecondaryColor")
            };
        }

        /// <summary>
        /// Color representation of the user's status for UI display
        /// </summary>
        public Color StatusColor => GetColorForStatus(Status);

        /// <summary>
        /// Friendly display name for the status
        /// </summary>
        public string StatusDisplay => Status switch
        {
            UserPresenceStatus.Online => "Online",
            UserPresenceStatus.Away => "Away",
            UserPresenceStatus.Busy => "Busy",
            UserPresenceStatus.DoNotDisturb => "Do Not Disturb",
            UserPresenceStatus.Offline => "Offline",
            _ => "Unknown"
        };
    }
}
