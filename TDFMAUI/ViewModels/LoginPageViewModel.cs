using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using TDFMAUI.Services;
using TDFShared.Enums;

namespace TDFMAUI.ViewModels
{
    public partial class LoginPageViewModel : ObservableObject
    {
        private readonly IAuthService _authService;
        private readonly WebSocketService _webSocketService;
        private readonly ILogger<LoginPageViewModel> _logger;
        private readonly IServiceProvider _serviceProvider;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
        private string? _username;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
        private string? _password;

        [ObservableProperty]
        private string? _errorMessage;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotBusy))]
        private bool _isBusy;

        public bool IsNotBusy => !IsBusy;

        public LoginPageViewModel(
            IAuthService authService,
            WebSocketService webSocketService,
            ILogger<LoginPageViewModel> logger,
            IServiceProvider serviceProvider)
        {
            _authService = authService;
            _webSocketService = webSocketService;
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Clears login data when returning to the login page
        /// </summary>
        public void ClearLoginData()
        {
            // Only clear password for security, keep username for convenience
            Password = string.Empty;
            ErrorMessage = string.Empty;
            IsBusy = false;
        }

        // Determine if the login command can execute
        private bool CanLogin() => !string.IsNullOrWhiteSpace(Username) &&
                                   !string.IsNullOrWhiteSpace(Password) &&
                                   !IsBusy;

        [RelayCommand(CanExecute = nameof(CanLogin))]
        private async Task LoginAsync()
        {
            IsBusy = true;
            ErrorMessage = null; // Clear previous errors
            try
            {
                _logger.LogInformation("Login attempt for user: {Username}", Username);

                // Use non-null assertion because CanLogin ensures they are not null/whitespace
                var userDetails = await _authService.LoginAsync(Username!, Password!);

                if (userDetails != null)
                {
                    // Guard against null properties with null conditional operators and default values
                    _logger.LogInformation("Login successful for user ID: {UserId}, Name: {FullName}",
                        userDetails.UserId,
                        userDetails.FullName ?? "Unknown");

                    // Store needed data in a try-catch to handle any issues
                    try
                    {
                        // Connect to WebSocket
                        _logger.LogInformation("Setting up WebSocket connection after successful login");
                        var webSocketService = _serviceProvider.GetRequiredService<IWebSocketService>();
                        await webSocketService.ConnectAsync();

                        // Update user status to Online
                        _logger.LogInformation("Updating user status to Online after login");
                        var userPresenceService = _serviceProvider.GetRequiredService<IUserPresenceService>();
                        await userPresenceService.UpdateStatusAsync(UserPresenceStatus.Online, "");

                        // Navigate to main page
                        _logger.LogInformation("Navigating to main page after successful login");

                        // Always create a new AppShell and set it as MainPage
                        try
                        {
                            var appShell = _serviceProvider?.GetService<AppShell>();
                            if (appShell != null && Application.Current != null)
                            {
                                _logger.LogInformation("Setting AppShell as MainPage");
                                Application.Current.MainPage = appShell;

                                // Wait a moment for the shell to initialize
                                await Task.Delay(100);

                                // Now try to navigate if Shell.Current is available
                                if (Shell.Current != null)
                                {
                                    _logger.LogInformation("Shell.Current is now available, navigating to MainPage");
                                    await Shell.Current.GoToAsync($"//{nameof(MainPage)}");
                                }
                            }
                            else
                            {
                                throw new InvalidOperationException("AppShell could not be resolved or Application.Current is null.");
                            }
                        }
                        catch (Exception shellEx)
                        {
                            _logger.LogError(shellEx, "Failed to set AppShell as MainPage after login.");
                            ErrorMessage = "Login successful, but failed to navigate to the main application page. Please restart.";
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during post-login navigation or WebSocket setup");
                        ErrorMessage = "Login successful, but there was an issue connecting to services. Some features may be limited.";

                        // Try to navigate anyway, even if WebSocket failed
                        _logger.LogInformation("Attempting secondary navigation to main page after WebSocket error.");

                        // Always create a new AppShell and set it as MainPage for consistency
                        try
                        {
                            var appShell = _serviceProvider?.GetService<AppShell>();
                            if (appShell != null && Application.Current != null)
                            {
                                _logger.LogInformation("Setting AppShell as MainPage (secondary navigation)");
                                Application.Current.MainPage = appShell;

                                // Wait a moment for the shell to initialize
                                await Task.Delay(100);

                                // Now try to navigate if Shell.Current is available
                                if (Shell.Current != null)
                                {
                                    _logger.LogInformation("Shell.Current is now available, navigating to MainPage (secondary navigation)");
                                    await Shell.Current.GoToAsync($"//{nameof(MainPage)}");
                                }
                            }
                            else
                            {
                                throw new InvalidOperationException("AppShell could not be resolved or Application.Current is null for secondary navigation.");
                            }
                        }
                        catch (Exception shellEx)
                        {
                            _logger.LogError(shellEx, "Failed to set AppShell as MainPage during secondary navigation attempt.");
                            ErrorMessage = "Unable to proceed after login (AppShell setup error). Please restart the application.";
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("Login failed for user: {Username}", Username);
                    ErrorMessage = "Login failed. Please check your username and password.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred during login for user: {Username}", Username);
                ErrorMessage = "An unexpected error occurred. Please try again.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        // Optional: Command for navigating to Signup page
        // [RelayCommand]
        // private async Task GoToSignupAsync()
        // {
        //     await Shell.Current.GoToAsync("SignupPage"); // Assuming route name
        // }
    }
}