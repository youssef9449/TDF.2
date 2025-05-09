using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using TDFMAUI.Helpers;
using TDFMAUI.Services;
using TDFShared.Enums;
using Microsoft.Maui.Controls;

namespace TDFMAUI.Pages
{
    public partial class NotificationTestPage : ContentPage
    {
        private readonly IExtendedNotificationService _notificationService;
        
        // Platform info for display
        public string PlatformInfo { get; set; }
        
        // Info notification fields
        public string InfoTitle { get; set; } = "Information";
        public string InfoMessage { get; set; } = "This is an information notification.";
        
        // Success notification fields
        public string SuccessTitle { get; set; } = "Success";
        public string SuccessMessage { get; set; } = "Operation completed successfully!";
        
        // Warning notification fields
        public string WarningTitle { get; set; } = "Warning";
        public string WarningMessage { get; set; } = "Something might go wrong.";
        
        // Error notification fields
        public string ErrorTitle { get; set; } = "Error";
        public string ErrorMessage { get; set; } = "An error has occurred.";
        
        // Commands
        public ICommand SendInfoCommand { get; private set; }
        public ICommand SendSuccessCommand { get; private set; }
        public ICommand SendWarningCommand { get; private set; }
        public ICommand SendErrorCommand { get; private set; }
        public ICommand ViewHistoryCommand { get; private set; }
        
        public NotificationTestPage(IExtendedNotificationService notificationService)
        {
            InitializeComponent();
            _notificationService = notificationService;
            
            // Set platform info
            SetPlatformInfo();
            
            // Initialize commands
            SendInfoCommand = new Command(async () => await SendNotification(InfoTitle, InfoMessage, NotificationType.Info));
            SendSuccessCommand = new Command(async () => await SendNotification(SuccessTitle, SuccessMessage, NotificationType.Success));
            SendWarningCommand = new Command(async () => await SendNotification(WarningTitle, WarningMessage, NotificationType.Warning));
            SendErrorCommand = new Command(async () => await SendNotification(ErrorTitle, ErrorMessage, NotificationType.Error));
            ViewHistoryCommand = new Command(async () => await ViewNotificationHistory());
            
            BindingContext = this;
        }
        
        private void SetPlatformInfo()
        {
            string platform;
            if (DeviceHelper.IsWindows)
                platform = "Windows";
            else if (DeviceHelper.IsMacOS)
                platform = "macOS";
            else if (DeviceHelper.IsIOS)
                platform = "iOS";
            else if (DeviceHelper.IsAndroid)
                platform = "Android";
            else
                platform = "Unknown";
                
            string deviceType = DeviceHelper.IsDesktop ? "Desktop" : "Mobile";
            
            PlatformInfo = $"Running on {platform} ({deviceType}) - {DeviceInfo.Model}";
        }
        
        private async Task SendNotification(string title, string message, NotificationType type)
        {
            try
            {
                await NotificationHelper.ShowNotificationAsync(title, message, type);
                
                // Also show a toast to confirm
                await DisplayToast($"Sent {type} notification", type);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to send notification: {ex.Message}", "OK");
            }
        }
        
        private async Task DisplayToast(string message, NotificationType type)
        {
            switch (type)
            {
                case NotificationType.Success:
                    await DisplayAlert("Success", message, "OK");
                    break;
                case NotificationType.Error:
                    await DisplayAlert("Error", message, "OK");
                    break;
                default:
                    await DisplayAlert("Notification", message, "OK");
                    break;
            }
        }
        
        private async Task ViewNotificationHistory()
        {
            try
            {
                var history = await NotificationHelper.GetNotificationHistoryAsync();
                
                if (history == null || !history.Any())
                {
                    await DisplayAlert("Notification History", "No notifications have been sent yet.", "OK");
                    return;
                }
                
                // Build a string with the history items
                var historyText = new StringBuilder();
                historyText.AppendLine($"Total notifications: {history.Count}");
                historyText.AppendLine();
                
                foreach (var item in history.OrderByDescending(n => n.Timestamp).Take(10))
                {
                    historyText.AppendLine($"[{item.Timestamp.ToString("g")}] {item.Type}: {item.Title}");
                    historyText.AppendLine($"    {item.Message}");
                    historyText.AppendLine();
                }
                
                // Display the history
                await DisplayAlert("Recent Notifications (10)", historyText.ToString(), "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to load notification history: {ex.Message}", "OK");
            }
        }
    }
} 