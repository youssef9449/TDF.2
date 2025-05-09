using TDFMAUI.Pages;
using TDFMAUI.Features.Auth;
using TDFMAUI.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TDFMAUI.Config;
using Microsoft.Extensions.Logging;
using TDFMAUI.Features.Requests;

namespace TDFMAUI
{
    public partial class AppShell : Shell, INotifyPropertyChanged
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AppShell> _logger;

        public bool IsAdmin => App.CurrentUser?.Roles?.Contains("Admin", StringComparer.OrdinalIgnoreCase) ?? false;
        public bool IsHR => App.CurrentUser?.Roles?.Contains("HR", StringComparer.OrdinalIgnoreCase) ?? false;
        public bool IsManager => App.CurrentUser?.Roles?.Contains("Manager", StringComparer.OrdinalIgnoreCase) ?? false;

        public bool IsDevelopmentMode => ApiConfig.DevelopmentMode;

        public AppShell(IAuthService authService, ILogger<AppShell> logger)
        {
            try
            {
                _logger = logger; // Assign logger first if possible
                _logger?.LogInformation("AppShell constructor started.");

                // Log dependencies
                _logger?.LogInformation("AuthService dependency resolved: {IsResolved}", authService != null);

                try
                {
                    _logger?.LogInformation("Calling InitializeComponent...");
                    InitializeComponent();
                    _logger?.LogInformation("AppShell InitializeComponent successful.");
                }
                catch (Exception ex)
                {
                    _logger?.LogCritical(ex, "AppShell InitializeComponent FAILED.");
                    // Rethrow or handle critically, as the Shell cannot function
                    throw;
                }

                _authService = authService;

                // Register routes for navigation
                _logger?.LogInformation("AppShell registering routes.");
                RegisterRoutes();
                _logger?.LogInformation("Routes registered successfully.");

                // Set binding context for property bindings
                BindingContext = this;
                _logger?.LogInformation("BindingContext set to self.");

                // Hook into user changed event to refresh admin status
                App.UserChanged += OnUserChanged;
                _logger?.LogInformation("AppShell constructor completed successfully.");
            }
            catch (Exception ex)
            {
                _logger?.LogCritical(ex, "Error in AppShell constructor.");
                throw; // Rethrow so the application knows there was a critical failure
            }
        }

        private void RegisterRoutes()
        {
            Routing.RegisterRoute(nameof(LoginPage), typeof(LoginPage));
            Routing.RegisterRoute(nameof(SignupPage), typeof(SignupPage));
            Routing.RegisterRoute(nameof(MainPage), typeof(MainPage));
            Routing.RegisterRoute(nameof(ProfilePage), typeof(ProfilePage));
            Routing.RegisterRoute(nameof(AddRequestPage), typeof(AddRequestPage));
            Routing.RegisterRoute(nameof(RequestDetailsPage), typeof(RequestDetailsPage));
            Routing.RegisterRoute(nameof(RequestsPage), typeof(RequestsPage));
            Routing.RegisterRoute(nameof(MessagesPage), typeof(MessagesPage));

            // Debug routes
            Routing.RegisterRoute(nameof(DebugPage), typeof(DebugPage));
            Routing.RegisterRoute(nameof(DiagnosticsPage), typeof(DiagnosticsPage));

            // Admin routes
            Routing.RegisterRoute(nameof(UsersPage), typeof(UsersPage));
            Routing.RegisterRoute(nameof(UserDetailsPage), typeof(UserDetailsPage));
        }

        private void OnUserChanged(object sender, EventArgs e)
        {
            OnPropertyChanged(nameof(IsAdmin));
            OnPropertyChanged(nameof(IsHR));
            OnPropertyChanged(nameof(IsManager));
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            App.UserChanged -= OnUserChanged;
        }

        private async void OnLogoutClicked(object sender, EventArgs e)
        {
            bool confirm = await DisplayAlert("Logout", "Are you sure you want to logout?", "Yes", "No");
            if (confirm)
            {
                try
                {
                    _logger?.LogInformation("Logout initiated by user.");
                    await _authService.LogoutAsync(); // Call the service method
                    // Navigation is now handled within AuthService.LogoutAsync
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error during logout process.");
                    // Display an error message to the user if logout fails
                    await DisplayAlert("Logout Error", "An unexpected error occurred during logout. Please try again.", "OK");
                }
            }
        }

        // INotifyPropertyChanged implementation
        public new event PropertyChangedEventHandler PropertyChanged;

        protected new void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
