using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Extensions.Logging;
using TDFMAUI.Pages;
using TDFMAUI.Services;
using TDFShared.Enums;
using TDFMAUI.Helpers;

namespace TDFMAUI.Features.Users
{
    public partial class OnlineUsersFlyout : ContentView, IDisposable
    {
        private IUserPresenceService _userPresenceService;
        private ILogger<OnlineUsersFlyout> _logger;

        private ObservableCollection<UserViewModel> _users = new();
        private bool _isDisposed;

        // Default constructor required for XAML
        public OnlineUsersFlyout()
        {
            InitializeComponent();

            // We'll initialize this properly in OnHandlerChanged
            _users = new ObservableCollection<UserViewModel>();
            usersCollection.ItemsSource = _users;
            refreshView.Command = new Command(async () => await RefreshUsers());
        }

        public OnlineUsersFlyout(IUserPresenceService userPresenceService, ILogger<OnlineUsersFlyout> logger)
        {
            InitializeComponent();

            _userPresenceService = userPresenceService;
            _logger = logger;

            usersCollection.ItemsSource = _users;
            refreshView.Command = new Command(async () => await RefreshUsers());
        }

        public async Task Initialize()
        {
            // If services aren't initialized yet, try to get them from DI
            if (_userPresenceService == null)
            {
                _userPresenceService = App.Services?.GetService<IUserPresenceService>();
                if (_userPresenceService == null)
                {
                    // Still null, can't proceed
                    return;
                }
            }

            // Subscribe to events
            _userPresenceService.UserStatusChanged += OnUserStatusChanged;
            _userPresenceService.UserAvailabilityChanged += OnUserAvailabilityChanged;
            _userPresenceService.PresenceErrorReceived += OnPresenceErrorReceived;

            await RefreshUsers();
        }

        public void Cleanup()
        {
            UnsubscribeFromEvents();
        }

        private void UnsubscribeFromEvents()
        {
            // Unsubscribe from events if service is available
            if (_userPresenceService != null)
            {
                _userPresenceService.UserStatusChanged -= OnUserStatusChanged;
                _userPresenceService.UserAvailabilityChanged -= OnUserAvailabilityChanged;
                _userPresenceService.PresenceErrorReceived -= OnPresenceErrorReceived;
            }
        }

        private async Task RefreshUsers()
        {
            if (App.CurrentUser == null)
            {
                return;
            }

            // If services aren't initialized yet, try to get them from DI
            if (_userPresenceService == null)
            {
                _userPresenceService = App.Services?.GetService<IUserPresenceService>();
                if (_userPresenceService == null)
                {
                    // Still null, can't proceed
                    return;
                }
            }

            if (_logger == null)
            {
                _logger = App.Services?.GetService<ILogger<OnlineUsersFlyout>>();
            }

            loadingIndicator.IsVisible = true;
            loadingIndicator.IsRunning = true;

            try
            {
                var onlineUsers = await _userPresenceService.GetOnlineUsersAsync();

                // Update UI on main thread
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _users.Clear();

                    foreach (var user in onlineUsers.Values)
                    {
                        _users.Add(new UserViewModel
                        {
                            UserId = user.UserId,
                            Username = user.Username,
                            FullName = user.FullName,
                            Department = user.Department,
                            Status = user.Status,
                            StatusMessage = user.StatusMessage,
                            IsAvailableForChat = user.IsAvailableForChat,
                            HasStatusMessage = !string.IsNullOrEmpty(user.StatusMessage),
                            StatusColor = GetStatusColor(user.Status),
                            ProfilePictureData = user.ProfilePictureData
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                if (_logger != null)
                {
                    _logger.LogError(ex, "Failed to load online users: {Message}", ex.Message);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading online users: {ex.Message}");
                }

                // Show error message to user
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await Shell.Current.DisplayAlert("Error", "Failed to load online users. Please try again later.", "OK");
                });
            }
            finally
            {
                refreshView.IsRefreshing = false;
                loadingIndicator.IsVisible = false;
                loadingIndicator.IsRunning = false;
            }
        }

        private static Color GetStatusColor(UserPresenceStatus status)
        {
            return status switch
            {
                UserPresenceStatus.Online => Colors.LightGreen,
                UserPresenceStatus.Away => Colors.Yellow,
                UserPresenceStatus.Busy => Colors.Orange,
                UserPresenceStatus.DoNotDisturb => Colors.Red,
                UserPresenceStatus.Offline => Colors.Gray,
                _ => Colors.Gray
            };
        }

        private void OnUserStatusChanged(object sender, UserStatusChangedEventArgs e)
        {
            // Update UI efficiently instead of full refresh
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var userVM = _users.FirstOrDefault(u => u.UserId == e.UserId);
                if (userVM != null)
                {
                    userVM.Status = e.Status;
                    userVM.StatusColor = GetStatusColor(e.Status);
                    _logger?.LogDebug("Updated UI for user {UserId} status change to {Status}", e.UserId, e.Status);
                }
                else
                {
                    // User might have come online and wasn't in the list before
                    _logger?.LogDebug("User {UserId} status changed but not found in current list. Refreshing list.", e.UserId);
                    // Consider adding the new user directly or trigger a refresh as a fallback
                    Task.Run(async () => await RefreshUsers());
                }
            });
        }

        private void OnUserAvailabilityChanged(object sender, UserAvailabilityChangedEventArgs e)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var userVM = _users.FirstOrDefault(u => u.UserId == e.UserId);
                if (userVM != null)
                {
                    userVM.IsAvailableForChat = e.IsAvailableForChat;
                    _logger?.LogDebug("Updated UI for user {UserId} availability change to {IsAvailable}", e.UserId, e.IsAvailableForChat);
                }
                else
                {
                    _logger?.LogDebug("User {UserId} availability changed but not found in current list.", e.UserId);
                }
            });
        }

        private void OnPresenceErrorReceived(object sender, WebSocketErrorEventArgs e)
        {
            _logger?.LogError("UI Received Error: Code={Code}, Message={Message}", e.ErrorCode ?? "N/A", e.ErrorMessage);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Optionally show a toast or notification to the user
                DebugService.LogError("OnlineUsersFlyout", $"Connection error: {e.ErrorMessage}");
            });
        }

        private async void OnRefreshClicked(object sender, EventArgs e)
        {
            await RefreshUsers();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    UnsubscribeFromEvents();
                }

                _isDisposed = true;
            }
        }
    }

    public class UserViewModel
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public string FullName { get; set; }
        public string Department { get; set; }
        public UserPresenceStatus Status { get; set; }
        public string StatusMessage { get; set; }
        public bool IsAvailableForChat { get; set; }
        public bool HasStatusMessage { get; set; }
        public Color StatusColor { get; set; }
        public byte[] ProfilePictureData { get; set; }
    }
}