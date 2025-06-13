using TDFMAUI.Pages;
using TDFMAUI.Features.Auth;
using TDFMAUI.Features.Admin;
using TDFMAUI.Features.Dashboard;
using TDFMAUI.Features.Settings;
using TDFMAUI.Features.Requests;
using TDFMAUI.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TDFMAUI.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using TDFMAUI.Helpers;
using TDFShared.Services;

namespace TDFMAUI
{
    public partial class AppShell : Shell, INotifyPropertyChanged
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AppShell> _logger;
        private readonly IUserPresenceService _userPresenceService;
        private string _previousRoute = "//"; // To store the route before opening the flyout

        /// <summary>        /// Gets the previous route that was navigated from before the current route
        /// </summary>
        public string PreviousRoute => _previousRoute;

        public bool IsAdmin => App.CurrentUser?.IsAdmin ?? false;
        public bool IsHR => App.CurrentUser?.IsHR ?? false;
        public bool IsManager => App.CurrentUser?.IsManager ?? false;

        public bool IsDevelopmentMode => ApiConfig.DevelopmentMode;

        /// <summary>
        /// Gets a value indicating whether the current device is a desktop device.
        /// Uses DeviceHelper.IsDesktop for consistent platform detection.
        /// </summary>
        public bool IsDesktopUser => DeviceHelper.IsDesktop;

        public AppShell(IAuthService authService, ILogger<AppShell> logger, IUserPresenceService userPresenceService)
        {
            try
            {
                _logger = logger;
                _logger?.LogInformation("AppShell constructor started.");
                _logger?.LogInformation("AuthService dependency resolved: {IsResolved}", authService != null);

                try
                {
                    _logger?.LogInformation("Calling InitializeComponent...");
                    InitializeComponent();
                    _logger?.LogInformation("AppShell InitializeComponent successful.");
                    Routing.RegisterRoute("users", typeof(UsersRightPanel));
                }
                catch (Exception ex)
                {
                    _logger?.LogCritical(ex, "AppShell InitializeComponent FAILED.");
                    throw;
                }

                _authService = authService;
                _userPresenceService = userPresenceService;

                _logger?.LogInformation("AppShell registering routes.");
                RegisterRoutes();
                _logger?.LogInformation("Routes registered successfully.");

                BindingContext = this;
                _logger?.LogInformation("BindingContext set to self.");

                App.UserChanged += OnUserChanged;
                this.Navigated += OnShellNavigated; // Setup gestures on first navigation and subsequent ones
                this.Navigating += OnShellNavigating; // Add navigation guard

                _logger?.LogInformation("AppShell constructor completed successfully. Gestures will be set up in OnShellNavigated.");

            }
            catch (Exception ex)
            {
                _logger?.LogCritical(ex, "Error in AppShell constructor.");
                throw;
            }
        }

        private void RegisterRoutes()
        {
            System.Diagnostics.Debug.WriteLine("[AppShell] Registering Shell routes...");
            // Auth pages
            Routing.RegisterRoute(nameof(Features.Auth.LoginPage), typeof(Features.Auth.LoginPage));
            System.Diagnostics.Debug.WriteLine("[AppShell] Registered route: LoginPage");
            Routing.RegisterRoute(nameof(Features.Auth.SignupPage), typeof(Features.Auth.SignupPage));
            System.Diagnostics.Debug.WriteLine("[AppShell] Registered route: SignupPage");
            
            // Main pages
            Routing.RegisterRoute(nameof(MainPage), typeof(MainPage));
            Routing.RegisterRoute(nameof(Features.Dashboard.DashboardPage), typeof(Features.Dashboard.DashboardPage));
            
            // User related pages
            Routing.RegisterRoute(nameof(ProfilePage), typeof(ProfilePage));
            Routing.RegisterRoute(nameof(UserProfilePage), typeof(UserProfilePage));
            Routing.RegisterRoute(nameof(UsersPage), typeof(UsersPage));
            Routing.RegisterRoute(nameof(UserDetailsPage), typeof(UserDetailsPage));
            Routing.RegisterRoute(nameof(AddUserPage), typeof(AddUserPage));
            Routing.RegisterRoute(nameof(EditUserPage), typeof(EditUserPage));
            
            // Request related pages
            Routing.RegisterRoute(nameof(Features.Requests.AddRequestPage), typeof(Features.Requests.AddRequestPage));
            Routing.RegisterRoute(nameof(Features.Requests.RequestDetailsPage), typeof(Features.Requests.RequestDetailsPage));
            Routing.RegisterRoute(nameof(Pages.RequestsPage), typeof(Pages.RequestsPage));
            Routing.RegisterRoute(nameof(Pages.RequestApprovalPage), typeof(Pages.RequestApprovalPage));
            Routing.RegisterRoute(nameof(Pages.MyTeamPage), typeof(Pages.MyTeamPage));
            
            // Message related pages
            Routing.RegisterRoute(nameof(Pages.MessagesPage), typeof(Pages.MessagesPage));
            Routing.RegisterRoute(nameof(Pages.PrivateMessagesPage), typeof(Pages.PrivateMessagesPage));
            Routing.RegisterRoute(nameof(Pages.GlobalMessagesPage), typeof(Pages.GlobalMessagesPage));
            Routing.RegisterRoute(nameof(Pages.GlobalChatPage), typeof(Pages.GlobalChatPage));
            Routing.RegisterRoute(nameof(Pages.NewMessagePage), typeof(Pages.NewMessagePage));
            
            // Other pages
            Routing.RegisterRoute(nameof(NotificationsPage), typeof(NotificationsPage));
            Routing.RegisterRoute(nameof(NotificationTestPage), typeof(NotificationTestPage));
            Routing.RegisterRoute(nameof(DiagnosticsPage), typeof(DiagnosticsPage));
            Routing.RegisterRoute(nameof(StartupDiagnosticPage), typeof(StartupDiagnosticPage));
            Routing.RegisterRoute(nameof(ReportsPage), typeof(ReportsPage));
            Routing.RegisterRoute(nameof(Features.Admin.AdminPage), typeof(Features.Admin.AdminPage));
            Routing.RegisterRoute(nameof(Features.Settings.ThemeSettingsPage), typeof(Features.Settings.ThemeSettingsPage));
            
            System.Diagnostics.Debug.WriteLine("[AppShell] All routes registered.");
        }

        private void OnUserChanged(object sender, EventArgs e)
        {
            OnPropertyChanged(nameof(IsAdmin));
            OnPropertyChanged(nameof(IsHR));
            OnPropertyChanged(nameof(IsManager));
            OnPropertyChanged(nameof(IsDesktopUser));
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            // Setup swipe gestures for the initial page
            // This ensures the Dashboard and other pages have the swipe gesture from the beginning
            SetupRightSwipeGesture();

            _logger?.LogInformation("AppShell.OnAppearing: Initial right swipe gestures set up");
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            App.UserChanged -= OnUserChanged;
            this.Navigated -= OnShellNavigated;
        }

        private async void OnLogoutClicked(object sender, EventArgs e)
        {
            bool confirm = await DisplayAlert("Logout", "Are you sure you want to logout?", "Yes", "No");
            if (confirm)
            {
                try
                {
                    _logger?.LogInformation("Logout initiated by user.");
                    await _authService.LogoutAsync();
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error during logout process.");
                    await DisplayAlert("Logout Error", "An unexpected error occurred during logout. Please try again.", "OK");
                }
            }
        }

        public new event PropertyChangedEventHandler PropertyChanged;

        protected new void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void OnShellNavigated(object sender, ShellNavigatedEventArgs e)
        {
            _logger?.LogInformation("Navigated to: {Current}, Previous: {Previous}, Source: {Source}",
                e.Current?.Location?.OriginalString, e.Previous?.Location?.OriginalString, e.Source);

            // Store the previous route if we are not coming from the users flyout itself
            if (e.Previous?.Location?.OriginalString != null && !e.Previous.Location.OriginalString.EndsWith("//users"))
            {
                _previousRoute = e.Previous.Location.OriginalString;
            }
            else if (e.Source == ShellNavigationSource.Pop && e.Current?.Location?.OriginalString != null)
            {
                // If we popped to a page, that's our new "previous" for context
                _previousRoute = e.Current.Location.OriginalString;
            }
             else if (string.IsNullOrEmpty(_previousRoute) || _previousRoute == "//" && e.Current?.Location?.OriginalString != null && !e.Current.Location.OriginalString.EndsWith("//users"))
            {
                // Initial navigation or if previousRoute is unset, set previous route to current if not users flyout
                _previousRoute = e.Current.Location.OriginalString;
            }

            SetupRightSwipeGesture();
        }

        private void SetupRightSwipeGesture()
        {
            _logger?.LogInformation("Attempting to set up right swipe gestures.");

            try
            {
                // Gesture to open the flyout (swipe from right edge on page content)
                if (CurrentPage is ContentPage page && page.Content != null)
                {
                    bool openGestureExists = page.Content.GestureRecognizers.Any(g => g is SwipeGestureRecognizer sgr && sgr.Direction == SwipeDirection.Right && sgr.CommandParameter as string == "open_users_flyout");
                    if (!openGestureExists)
                    {
                        var openSwipeGesture = new SwipeGestureRecognizer { Direction = SwipeDirection.Right, CommandParameter = "open_users_flyout" };
                        openSwipeGesture.Swiped += OnOpenRightFlyout_Swiped;
                        page.Content.GestureRecognizers.Add(openSwipeGesture);
                        _logger?.LogInformation("Added OPEN swipe gesture (right edge) to current page content.");
                    }
                }
                else
                {
                     _logger?.LogWarning("CurrentPage is not a ContentPage with Content, cannot attach open swipe gesture.");
                }

                // For the close gesture, we'll handle it differently
                // Instead of trying to access the content directly, we'll add the gesture in the UsersRightPanel class
                // This avoids the timing issue where the content might not be fully loaded yet
                
                // We'll just log that we're skipping this part for now
                _logger?.LogInformation("Close swipe gesture will be handled by UsersRightPanel itself.");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error setting up swipe gestures");
            }
        }

        private async void OnOpenRightFlyout_Swiped(object sender, SwipedEventArgs e)
        {
            _logger?.LogInformation("Open right flyout (users panel) swiped from right edge.");
            if (Shell.Current.CurrentState.Location?.OriginalString != null && !Shell.Current.CurrentState.Location.OriginalString.EndsWith("//users"))
            {
                _previousRoute = Shell.Current.CurrentState.Location.OriginalString;
                _logger?.LogInformation($"Storing previous route: {_previousRoute}");
            }
            await Shell.Current.GoToAsync("//users", true); // Animate
        }

        /// <summary>
        /// Closes the UsersRightPanel and navigates back to the previous route
        /// </summary>
        public async Task CloseUsersRightPanelAsync()
        {
            _logger?.LogInformation("Closing users right panel.");
            if (!string.IsNullOrEmpty(_previousRoute) && _previousRoute != "//users")
            {
                _logger?.LogInformation($"Returning to previous route: {_previousRoute}");
                await Shell.Current.GoToAsync(_previousRoute, true); // Animate back
            }
            else
            {
                _logger?.LogWarning("No valid previous route to return to, attempting to go back or to root.");
                if (Shell.Current.Navigation.NavigationStack.Count > 1)
                {
                    await Shell.Current.GoToAsync("..", true);
                }
                else
                {
                    var mainPageRoute = "//" + nameof(MainPage);
                    try
                    { // Attempt to navigate to main page, if route is not valid GoToAsync will throw
                        await Shell.Current.GoToAsync(mainPageRoute, true);
                    }
                    catch (Exception ex_mainpage_nav)
                    {
                        _logger?.LogWarning(ex_mainpage_nav, $"Could not navigate to {mainPageRoute}, going to Shell root.");
                        await Shell.Current.GoToAsync("//", true); // Go to shell root as a last resort
                    }
                }
            }
        }

        private async void OnCloseRightFlyout_Swiped(object sender, SwipedEventArgs e)
        {
            _logger?.LogInformation("Close right flyout (users panel) swiped to left.");
            await CloseUsersRightPanelAsync();
        }

        private async void OpenUsersFlyout_Tapped(object sender, EventArgs e)
        {
            try
            {
                _logger?.LogInformation("OpenUsersFlyout_Tapped called.");
                
                // Store current route if not already at users panel
                if (Shell.Current.CurrentState.Location?.OriginalString != null && 
                    !Shell.Current.CurrentState.Location.OriginalString.EndsWith("//users"))
                {
                    _previousRoute = Shell.Current.CurrentState.Location.OriginalString;
                    _logger?.LogInformation($"Storing previous route: {_previousRoute} before navigating from tap.");
                }

                // Navigate to users panel with animation
                await Shell.Current.GoToAsync("//users", true);
                _logger?.LogInformation("Successfully navigated to users panel.");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error navigating to users panel");
                await DisplayAlert("Navigation Error", "Could not open the users panel. Please try again.", "OK");
            }
        }
        
        private void ToggleTheme_Clicked(object sender, EventArgs e)
        {
            _logger?.LogInformation("ToggleTheme_Clicked called.");
            ThemeHelper.ToggleTheme();
            _logger?.LogInformation($"Theme toggled to: {ThemeHelper.CurrentTheme}");
        }

        /// <summary>
        /// Navigation guard: blocks navigation to protected pages if not authenticated
        /// </summary>
        private void OnShellNavigating(object sender, ShellNavigatingEventArgs e)
        {
            try
            {
                // Allow navigation to these pages without authentication
                var publicRoutes = new[] { "//LoginPage", "//SignupPage", "//DiagnosticsPage" };
                var target = e.Target?.Location?.OriginalString;
                if (string.IsNullOrEmpty(target)) return;

                // If not authenticated and not navigating to a public route, block and redirect
                if (App.CurrentUser == null && !publicRoutes.Any(r => target.StartsWith(r, StringComparison.OrdinalIgnoreCase)))
                {
                    _logger?.LogInformation($"Navigation to {target} blocked: user not authenticated. Redirecting to login page.");
                    e.Cancel();
                    // Redirect to login page
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        try { await Shell.Current.GoToAsync("//LoginPage"); } catch { }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in navigation guard");
            }
        }
    }
}
