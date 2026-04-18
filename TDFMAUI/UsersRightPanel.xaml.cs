using Microsoft.Maui.Controls;
using System;
using System.Threading.Tasks;
using TDFMAUI.Services;
using TDFMAUI.ViewModels;
using TDFShared.Enums;
using Microsoft.Extensions.Logging;
using TDFShared.DTOs.Users;
using TDFShared.Services;
using TDFMAUI.Services.Presence;

namespace TDFMAUI
{
    public partial class UsersRightPanel : ContentPage
    {
        private readonly TDFMAUI.Services.Presence.IUserPresenceService _userPresenceService;
        private readonly ILogger<UsersRightPanel> _logger;
        private readonly PanelStateService _panelStateService;
        private readonly UsersRightPanelViewModel _viewModel;

        public UsersRightPanel()
        {
            InitializeComponent();
            try
            {
                _userPresenceService = App.Services.GetService<TDFMAUI.Services.Presence.IUserPresenceService>();
                _logger = App.Services.GetService<ILogger<UsersRightPanel>>();
                _panelStateService = App.Services.GetService<PanelStateService>();
                _viewModel = App.Services.GetService<UsersRightPanelViewModel>();

                BindingContext = _viewModel;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CRITICAL] UsersRightPanel: Failed to resolve services: {ex.Message}");
            }
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            
            try
            {
                if (_userPresenceService != null)
                {
                    _userPresenceService.UserStatusChanged += OnUserPresenceServiceStatusChanged;
                    _userPresenceService.UserAvailabilityChanged += OnUserAvailabilityChanged;
                }

                _panelStateService?.RegisterPanel(this);
                await _viewModel.RefreshUsersAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in OnAppearing");
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            
            try
            {
                _panelStateService?.UnregisterPanel(this);

                if (_userPresenceService != null)
                {
                    _userPresenceService.UserStatusChanged -= OnUserPresenceServiceStatusChanged;
                    _userPresenceService.UserAvailabilityChanged -= OnUserAvailabilityChanged;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in OnDisappearing");
            }
        }

        private void OnUserPresenceServiceStatusChanged(object? sender, UserStatusChangedEventArgs e)
        {
            MainThread.BeginInvokeOnMainThread(() => _viewModel.HandleUserStatusChanged(e));
        }

        private void OnUserAvailabilityChanged(object? sender, UserAvailabilityChangedEventArgs e)
        {
            MainThread.BeginInvokeOnMainThread(() => _viewModel.HandleUserAvailabilityChanged(e));
        }

        private async void ClosePanel_Clicked(object sender, EventArgs e)
        {
            if (Shell.Current is AppShell appShell)
            {
                await appShell.CloseUsersRightPanelAsync();
            }
            else
            {
                if (Shell.Current.Navigation.NavigationStack.Count > 1) await Shell.Current.GoToAsync("..", true);
                else await Shell.Current.GoToAsync("//", true);
            }
        }
    }
}
