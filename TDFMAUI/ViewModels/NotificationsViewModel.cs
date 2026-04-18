using System.Collections.ObjectModel;
using TDFMAUI.Services;
using System.Linq;
using TDFShared.Models.Notification;
using TDFShared.Constants;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TDFShared.DTOs.Messages;
using TDFMAUI.Helpers;
using TDFMAUI.Services.Notifications;

namespace TDFMAUI.ViewModels
{
    public partial class NotificationsViewModel : BaseViewModel
    {
        private readonly INotificationClient _notificationService;
        private readonly IUserApiService _userApiService;
        private readonly Dictionary<int, string> _userCache = new();

        [ObservableProperty]
        private ObservableCollection<NotificationItemViewModel> _notifications = new();

        public NotificationsViewModel(
            INotificationClient notificationService,
            IUserApiService userApiService)
        {
            _notificationService = notificationService;
            _userApiService = userApiService;
            Title = "Notifications";
        }

        [RelayCommand]
        public async Task LoadNotificationsAsync()
        {
            if (App.CurrentUser == null) return;
            IsBusy = true;
            try
            {
                var notifications = await _notificationService.GetUnreadNotificationsAsync();
                Notifications.Clear();
                foreach (var n in notifications.OrderByDescending(x => x.Timestamp))
                {
                    Notifications.Add(await ConvertToViewModel(n));
                }
            }
            catch (Exception ex) { ErrorMessage = "Failed to load notifications."; }
            finally { IsBusy = false; }
        }

        private async Task<NotificationItemViewModel> ConvertToViewModel(NotificationEntity notification)
        {
            string senderName = "System";
            if (notification.SenderID.HasValue)
            {
                if (!_userCache.TryGetValue(notification.SenderID.Value, out senderName))
                {
                    var user = await _userApiService.GetUserByIdAsync(notification.SenderID.Value);
                    senderName = user?.UserName ?? $"User {notification.SenderID}";
                    _userCache[notification.SenderID.Value] = senderName;
                }
            }

            return new NotificationItemViewModel
            {
                NotificationId = notification.NotificationID,
                Message = notification.Message ?? string.Empty,
                Timestamp = notification.Timestamp,
                IsSeen = notification.IsSeen,
                SenderName = senderName
            };
        }

        [RelayCommand]
        private async Task ViewNotificationAsync(NotificationItemViewModel item)
        {
            if (item == null) return;
            if (!item.IsSeen)
            {
                if (await _notificationService.MarkAsSeenAsync(item.NotificationId)) item.IsSeen = true;
            }
            await Shell.Current.DisplayAlert("Notification", item.Message, "OK");
        }

        [RelayCommand]
        private async Task DeleteNotificationAsync(NotificationItemViewModel item)
        {
            if (item == null) return;
            if (await Shell.Current.DisplayAlert("Confirm", "Delete this notification?", "Yes", "No"))
            {
                try
                {
                    if (await _notificationService.DeleteNotificationAsync(item.NotificationId))
                    {
                        Notifications.Remove(item);
                    }
                }
                catch { ErrorMessage = "Delete failed."; }
            }
        }

        [RelayCommand]
        private async Task MarkAllReadAsync()
        {
            var unreadIds = Notifications.Where(n => !n.IsSeen).Select(n => n.NotificationId).ToList();
            if (!unreadIds.Any()) return;

            if (await _notificationService.MarkNotificationsAsSeenAsync(unreadIds))
            {
                foreach (var n in Notifications) n.IsSeen = true;
            }
        }

        public void HandleNotificationReceived(NotificationDto e)
        {
            if (Notifications.Any(n => n.NotificationId == e.NotificationId)) return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                Notifications.Insert(0, new NotificationItemViewModel
                {
                    NotificationId = e.NotificationId,
                    Message = e.Message,
                    Timestamp = e.Timestamp,
                    IsSeen = false,
                    SenderName = e.SenderName ?? "System"
                });
            });
        }
    }

    public partial class NotificationItemViewModel : ObservableObject
    {
        public int NotificationId { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(BackgroundColor))]
        private bool _isSeen;

        public string SenderName { get; set; } = string.Empty;
        public string TimestampFormatted => Timestamp.ToString("g");
        public Color BackgroundColor => IsSeen ? ThemeHelper.GetThemeResource<Color>("SurfaceColor") : ThemeHelper.GetThemeResource<Color>("BlueCardColor");
    }
}
