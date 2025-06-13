using TDFShared.Enums;
using Microsoft.Maui.Graphics;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TDFMAUI.ViewModels
{
    /// <summary>
    /// View model representing a user and their online presence status
    /// Implements INotifyPropertyChanged for real-time UI updates
    /// </summary>
    public class UserViewModel : INotifyPropertyChanged
    {
        private int _userId;
        private string _username;
        private string _fullName;
        private string _department;
        private UserPresenceStatus _status;
        private string _statusMessage;
        private bool _isAvailableForChat;
        private byte[] _profilePictureData;

        /// <summary>
        /// User's unique identifier
        /// </summary>
        public int UserId 
        { 
            get => _userId;
            set => SetProperty(ref _userId, value);
        }

        /// <summary>
        /// User's login username
        /// </summary>
        public string Username 
        { 
            get => _username;
            set => SetProperty(ref _username, value);
        }

        /// <summary>
        /// User's full display name
        /// </summary>
        public string FullName 
        { 
            get => _fullName; 
            set => SetProperty(ref _fullName, value);
        }

        /// <summary>
        /// User's department or team
        /// </summary>
        public string Department 
        { 
            get => _department; 
            set => SetProperty(ref _department, value);
        }

        /// <summary>
        /// Current online presence status of the user
        /// </summary>
        public UserPresenceStatus Status 
        { 
            get => _status; 
            set
            {
                if (SetProperty(ref _status, value))
                {
                    OnPropertyChanged(nameof(StatusColor));
                    OnPropertyChanged(nameof(StatusDisplay));
                }
            }
        }

        /// <summary>
        /// Optional custom status message set by the user
        /// </summary>
        public string StatusMessage 
        { 
            get => _statusMessage; 
            set
            {
                if (SetProperty(ref _statusMessage, value))
                {
                    OnPropertyChanged(nameof(HasStatusMessage));
                }
            }
        }

        /// <summary>
        /// Indicates if the user is available for chat/messaging
        /// </summary>
        public bool IsAvailableForChat 
        { 
            get => _isAvailableForChat; 
            set => SetProperty(ref _isAvailableForChat, value);
        }

        /// <summary>
        /// Indicates if the user has set a custom status message
        /// </summary>
        public bool HasStatusMessage => !string.IsNullOrEmpty(StatusMessage);

        /// <summary>
        /// Provides a color based on the user's presence status.
        /// </summary>
        /// <param name="status">The user's presence status.</param>
        /// <returns>A <see cref="Color"/> representing the status.</returns>
        public static Color GetColorForStatus(UserPresenceStatus status)
        {
            return status switch
            {
                UserPresenceStatus.Online => Colors.Green,
                UserPresenceStatus.Away => Colors.Orange,
                UserPresenceStatus.Busy => Colors.Red,
                UserPresenceStatus.DoNotDisturb => Colors.DarkRed,
                UserPresenceStatus.Offline => Application.Current.Resources["TextSecondaryColor"] as Color ?? Colors.Gray,
            _ => Application.Current.Resources["TextSecondaryColor"] as Color ?? Colors.Gray
            };
        }

        /// <summary>
        /// Color representation of the user's status for UI display
        /// </summary>
        public Color StatusColor => GetColorForStatus(this.Status);

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

        /// <summary>
        /// Binary data of the user's profile picture
        /// </summary>
        public byte[] ProfilePictureData 
        { 
            get => _profilePictureData; 
            set => SetProperty(ref _profilePictureData, value);
        }

        /// <summary>
        /// Default constructor initializing properties with default values
        /// </summary>
        public UserViewModel()
        {
            Username = string.Empty;
            FullName = string.Empty;
            Department = string.Empty;
            StatusMessage = string.Empty;
            Status = UserPresenceStatus.Offline;
        }

        #region INotifyPropertyChanged Implementation
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
        #endregion
    }
}