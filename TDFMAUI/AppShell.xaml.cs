using TDFMAUI.Pages;
using TDFMAUI.Features.Auth;
using TDFMAUI.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TDFMAUI.Config;
using Microsoft.Extensions.Logging;
using TDFMAUI.Features.Requests;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using TDFMAUI.Helpers;

namespace TDFMAUI
{
    public partial class AppShell : Shell, INotifyPropertyChanged
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AppShell> _logger;
        private readonly IUserPresenceService _userPresenceService;
        private string _previousRoute = "//"; // To store the route before opening the flyout

        /// <summary>
        /// Gets the previous route that was navigated from before the current route
        /// </summary>
        public string PreviousRoute => _previousRoute;

        public bool IsAdmin => App.CurrentUser?.Roles?.Contains("Admin", StringComparer.OrdinalIgnoreCase) ?? false;
        public bool IsHR => App.CurrentUser?.Roles?.Contains("HR", StringComparer.OrdinalIgnoreCase) ?? false;
        public bool IsManager => App.CurrentUser?.Roles?.Contains("Manager", StringComparer.OrdinalIgnoreCase) ?? false;

        public bool IsDevelopmentMode => ApiConfig.DevelopmentMode;
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
            Routing.RegisterRoute(nameof(LoginPage), typeof(LoginPage));
            Routing.RegisterRoute(nameof(SignupPage), typeof(SignupPage));
            Routing.RegisterRoute(nameof(MainPage), typeof(MainPage));
            Routing.RegisterRoute(nameof(ProfilePage), typeof(ProfilePage));
            Routing.RegisterRoute(nameof(AddRequestPage), typeof(AddRequestPage));
            Routing.RegisterRoute(nameof(RequestDetailsPage), typeof(RequestDetailsPage));
            Routing.RegisterRoute(nameof(RequestsPage), typeof(RequestsPage));
            Routing.RegisterRoute(nameof(MessagesPage), typeof(MessagesPage));
            Routing.RegisterRoute(nameof(DiagnosticsPage), typeof(DiagnosticsPage));
            Routing.RegisterRoute(nameof(UsersPage), typeof(UsersPage));
            Routing.RegisterRoute("DashboardPage", typeof(Features.Dashboard.DashboardPage));
            Routing.RegisterRoute(nameof(UserDetailsPage), typeof(UserDetailsPage));
        }

        private void OnUserChanged(object sender, EventArgs e)
        {
            OnPropertyChanged(nameof(IsAdmin));
            OnPropertyChanged(nameof(IsHR));
            OnPropertyChanged(nameof(IsManager));
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
            _logger?.LogInformation($"Navigated to: {e.Current?.Location?.OriginalString}, Previous: {e.Previous?.Location?.OriginalString}, Source: {e.Source}");

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

            // Gesture to close the flyout (swipe left on the flyout panel itself)
            if (this.RightSideUsersFlyout?.Content != null && this.RightSideUsersFlyout.Content is VisualElement)
            {
                View flyoutActualContent = (View)this.RightSideUsersFlyout.Content;
                bool closeGestureExists = flyoutActualContent.GestureRecognizers.Any(g => g is SwipeGestureRecognizer sgr && sgr.Direction == SwipeDirection.Left && sgr.CommandParameter as string == "close_users_flyout");
                if (!closeGestureExists)
                {
                    var closeSwipeGesture = new SwipeGestureRecognizer { Direction = SwipeDirection.Left, CommandParameter = "close_users_flyout" };
                    closeSwipeGesture.Swiped += OnCloseRightFlyout_Swiped;
                    flyoutActualContent.GestureRecognizers.Add(closeSwipeGesture);
                    _logger?.LogInformation("Added CLOSE swipe gesture to RightSideUsersFlyout's actual content (UsersRightPanel).");
                }
            }
            else
            {
                _logger?.LogWarning("Could not find RightSideUsersFlyout's content as VisualElement to attach close swipe gesture.");
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
            _logger?.LogInformation("OpenUsersFlyout_Tapped called.");
            if (Shell.Current.CurrentState.Location?.OriginalString != null && !Shell.Current.CurrentState.Location.OriginalString.EndsWith("//users"))
            {
                _previousRoute = Shell.Current.CurrentState.Location.OriginalString;
                 _logger?.LogInformation($"Storing previous route: {_previousRoute} before navigating from tap.");
            }
            await Shell.Current.GoToAsync("//users", true); // Navigate to the shell item, animate
        }
    }
}
