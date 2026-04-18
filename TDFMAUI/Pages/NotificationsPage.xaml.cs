using TDFMAUI.Services;
using TDFMAUI.ViewModels;
using TDFMAUI.Services.Notifications;

namespace TDFMAUI.Pages
{
    public partial class NotificationsPage : ContentPage
    {
        private readonly WebSocketService _webSocketService;
        private readonly NotificationsViewModel _viewModel;

        public NotificationsPage(
            NotificationsViewModel viewModel,
            WebSocketService webSocketService)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _webSocketService = webSocketService;
            BindingContext = _viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            _webSocketService.NotificationReceived += OnNotificationReceived;
            await _viewModel.LoadNotificationsAsync();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _webSocketService.NotificationReceived -= OnNotificationReceived;
        }

        private void OnNotificationReceived(object sender, TDFShared.DTOs.Messages.NotificationEventArgs e)
        {
            var dto = new TDFShared.DTOs.Messages.NotificationDto
            {
                NotificationId = e.NotificationId,
                Message = e.Message,
                Title = e.Title,
                NotificationType = e.Type,
                SenderId = e.SenderId,
                SenderName = e.SenderName,
                Timestamp = e.Timestamp
            };
            _viewModel.HandleNotificationReceived(dto);
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Controls.NotificationToast.ShowToastAsync(this, "New Notification", e.Message, TDFShared.Enums.NotificationType.Info);
            });
        }
    }
}
