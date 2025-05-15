using TDFShared.Enums;
using Microsoft.Maui.Graphics;

namespace TDFMAUI.ViewModels
{
    public class UserViewModel // Consider making it an ObservableObject if properties need to notify changes individually
    {
        public int UserId { get; set; }
        public string? Username { get; set; }
        public string? FullName { get; set; }
        public string? Department { get; set; }
        public UserPresenceStatus Status { get; set; }
        // public UserPresenceStatus NewStatus { get; set; } // This was in the UsersPage.xaml.cs version, review if needed
        public string? StatusMessage { get; set; }
        public bool IsAvailableForChat { get; set; }
        public bool HasStatusMessage { get; set; }
        public Color StatusColor { get; set; }
        public byte[]? ProfilePictureData { get; set; } // Made nullable

        // Default constructor to initialize nullable reference types if needed for safety, though properties are settable
        public UserViewModel()
        {
            Username = string.Empty;
            FullName = string.Empty;
            Department = string.Empty;
            StatusMessage = string.Empty;
            StatusColor = Colors.Gray; // Initialize to a default color
            // ProfilePictureData is fine as null by default
        }
    }
} 