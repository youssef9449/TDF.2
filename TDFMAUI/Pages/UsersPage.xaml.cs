using TDFMAUI.Helpers;
using TDFMAUI.Services;
using TDFMAUI.ViewModels;
using TDFShared.Enums;
using TDFShared.DTOs.Users;
using Microsoft.Extensions.Logging;
using TDFMAUI.Services.Presence;

namespace TDFMAUI.Pages
{
    public partial class UsersPage : ContentPage
    {
        private readonly IUserPresenceService _userPresenceService;
        private readonly ILogger<UsersPage> _logger;
        private readonly UsersViewModel _viewModel;
        
        public UsersPage(
            IUserPresenceService userPresenceService,
            UsersViewModel viewModel,
            ILogger<UsersPage> logger)
        {
            InitializeComponent();
            _userPresenceService = userPresenceService;
            _viewModel = viewModel;
            _logger = logger;
            BindingContext = _viewModel;
        }
        
        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // Subscribe to events
            _userPresenceService.UserStatusChanged += OnUserStatusChanged;
            _userPresenceService.UserAvailabilityChanged += OnUserAvailabilityChanged;
            _userPresenceService.AvailabilityConfirmed += OnAvailabilityConfirmed;
            _userPresenceService.StatusUpdateConfirmed += OnStatusUpdateConfirmed;
            _userPresenceService.PresenceErrorReceived += OnPresenceErrorReceived;

            await _viewModel.RefreshUsersAsync();
            // In a real MVVM setup, status display would be fully data-bound
        }
        
        protected override void OnDisappearing()
        {
             // Unsubscribe from events
            _userPresenceService.UserStatusChanged -= OnUserStatusChanged;
            _userPresenceService.UserAvailabilityChanged -= OnUserAvailabilityChanged;
            _userPresenceService.AvailabilityConfirmed -= OnAvailabilityConfirmed;
            _userPresenceService.StatusUpdateConfirmed -= OnStatusUpdateConfirmed;
            _userPresenceService.PresenceErrorReceived -= OnPresenceErrorReceived;

            base.OnDisappearing();
        }
        
        private void OnUserStatusChanged(object sender, UserStatusChangedEventArgs e)
        {
            MainThread.BeginInvokeOnMainThread(() => _viewModel.HandleUserStatusChanged(e));
        }
        
        private void OnUserAvailabilityChanged(object sender, UserAvailabilityChangedEventArgs e)
        {
             MainThread.BeginInvokeOnMainThread(() => _viewModel.HandleUserAvailabilityChanged(e));
        }
        
        private void OnMyStatusClicked(object sender, EventArgs e)
        {
            myStatusFrame.IsVisible = !myStatusFrame.IsVisible;
        }

        private void OnAvailabilityConfirmed(object sender, AvailabilitySetEventArgs e)
        {
            _logger.LogInformation("UI Received Confirmation: Availability set to {IsAvailable}", e.IsAvailable);
        }

        private void OnStatusUpdateConfirmed(object sender, StatusUpdateConfirmedEventArgs e)
        {
            _logger.LogInformation("UI Received Confirmation: Status updated to {Status}", e.Status);
        }

        private void OnPresenceErrorReceived(object sender, WebSocketErrorEventArgs e)
        {
            _logger.LogError("UI Received Error: Code={Code}, Message={Message}", e.ErrorCode ?? "N/A", e.ErrorMessage);
            MainThread.BeginInvokeOnMainThread(() => DisplayAlert("Presence Error", e.ErrorMessage, "OK"));
        }

        // These handlers are still in code-behind because they use UI elements directly (checkbox/entry)
        // ideally they should be fully data bound
        private async void OnAvailableForChatChanged(object sender, CheckedChangedEventArgs e)
        {
            await _userPresenceService.SetAvailabilityForChatAsync(e.Value);
        }

        private async void OnStatusMessageCompleted(object sender, EventArgs e)
        {
            var statusMessage = statusMessageEntry.Text;
            await _userPresenceService.UpdateStatusAsync(_viewModel.CurrentStatus, statusMessage);
        }
    }
}
