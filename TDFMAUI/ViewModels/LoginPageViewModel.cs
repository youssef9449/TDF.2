using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using TDFMAUI.Services;
using TDFShared.Enums;
using TDFShared.DTOs.Users;
using TDFShared.Services;
using TDFMAUI.Services.Presence;
using TDFShared.Contracts;

namespace TDFMAUI.ViewModels
{
    public partial class LoginPageViewModel : BaseViewModel
    {
        private readonly IAuthClient _authService;
        private readonly IAuthApiService _authApiService;
        private readonly IWebSocketService _webSocketService;
        private readonly ILogger<LoginPageViewModel> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly TDFMAUI.Services.Presence.IUserPresenceService _userPresenceService;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
        private string _username = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
        private string _password = string.Empty;

        public LoginPageViewModel(
            IAuthClient authService,
            IAuthApiService authApiService,
            IWebSocketService webSocketService,
            TDFMAUI.Services.Presence.IUserPresenceService userPresenceService,
            ILogger<LoginPageViewModel> logger,
            IServiceProvider serviceProvider)
        {
            _authService = authService;
            _authApiService = authApiService;
            _webSocketService = webSocketService;
            _userPresenceService = userPresenceService;
            _logger = logger;
            _serviceProvider = serviceProvider;
            Title = "Login";
        }

        public void ClearLoginData()
        {
            Password = string.Empty;
            ErrorMessage = string.Empty;
            IsBusy = false;
        }

        private bool CanLogin() => !string.IsNullOrWhiteSpace(Username) &&
                                   !string.IsNullOrWhiteSpace(Password) &&
                                   !IsBusy;

        [RelayCommand(CanExecute = nameof(CanLogin))]
        private async Task LoginAsync()
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            try
            {
                _logger.LogInformation("Login attempt for user: {Username}", Username);
                var userDetails = await _authService.LoginAsync(Username, Password);

                if (userDetails != null)
                {
                    var token = await _authService.GetCurrentTokenAsync();
                    if (!string.IsNullOrEmpty(token))
                    {
                        await _webSocketService.ConnectAsync(token);
                    }

                    _logger.LogInformation("Login successful for user ID: {UserId}", userDetails.UserId);
                    await _userPresenceService.UpdateStatusAsync(UserPresenceStatus.Online, "");

                    var appShell = _serviceProvider.GetService<AppShell>();
                    if (appShell != null && Application.Current != null)
                    {
                        Application.Current.MainPage = appShell;
                        await Task.Delay(100);
                        if (Shell.Current != null) await Shell.Current.GoToAsync("//DashboardPage");
                    }
                }
                else
                {
                    ErrorMessage = "Login failed. Please check your username and password.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for user: {Username}", Username);
                ErrorMessage = "An unexpected error occurred. Please try again.";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
