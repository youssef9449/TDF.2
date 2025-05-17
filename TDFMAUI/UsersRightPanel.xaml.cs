using Microsoft.Maui.Controls;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using TDFMAUI.Services;
using TDFMAUI.ViewModels; // Ensures UserViewModel is found
using TDFShared.Enums;
using Microsoft.Extensions.Logging;
using TDFMAUI.Helpers; // For ByteArrayToImageSourceConverter & GetStatusColor, if moved

namespace TDFMAUI
{
    /// <summary>
    /// Right-side panel that displays online users with their status
    /// </summary>
    public partial class UsersRightPanel : ContentPage
    {
        private IUserPresenceService _userPresenceService;
        private ApiService _apiService;
        private ILogger<UsersRightPanel> _logger;
        private IConnectivityService _connectivityService;

        private ObservableCollection<UserViewModel> _users = new ObservableCollection<UserViewModel>();
        public ObservableCollection<UserViewModel> Users => _users;

        private bool _isRefreshing;
        /// <summary>
        /// Indicates if the user list is currently refreshing
        /// </summary>
        public bool IsRefreshing
        {
            get => _isRefreshing;
            set
            {
                _isRefreshing = value;
                OnPropertyChanged(nameof(IsRefreshing));
            }
        }

        private bool _isLoading;
        /// <summary>
        /// Indicates if the user list is currently loading
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged(nameof(IsLoading));
            }
        }

        /// <summary>
        /// Command to refresh the user list
        /// </summary>
        public ICommand RefreshUsersCommand { get; private set; }

        // Parameterless constructor for XAML instantiation
        public UsersRightPanel()
        {
            InitializeComponent();
            try
            {
                // Resolve services from the global service provider
                _userPresenceService = App.Services.GetService<IUserPresenceService>();
                _apiService = App.Services.GetService<ApiService>();
                _logger = App.Services.GetService<ILogger<UsersRightPanel>>();
                _connectivityService = App.Services.GetService<IConnectivityService>();

                if (_userPresenceService == null) _logger?.LogCritical("UsersRightPanel: IUserPresenceService could not be resolved.");
                if (_apiService == null) _logger?.LogCritical("UsersRightPanel: ApiService could not be resolved.");
                if (_logger == null) System.Diagnostics.Debug.WriteLine("[CRITICAL] UsersRightPanel: ILogger could not be resolved.");
                if (_connectivityService == null) _logger?.LogCritical("UsersRightPanel: IConnectivityService could not be resolved.");
            }
            catch (Exception ex)
            {
                // Log critical failure if services can't be resolved
                _logger?.LogCritical(ex, "UsersRightPanel: Critical failure resolving services in parameterless constructor.");
                // Or use System.Diagnostics.Debug if logger itself failed
                System.Diagnostics.Debug.WriteLine($"[CRITICAL] UsersRightPanel: Failed to resolve services: {ex.Message}");
            }

            BindingContext = this;
            RefreshUsersCommand = new Command(async () => await RefreshUsersAsync());
        }

        // Constructor with dependency injection (can be kept for testing or direct instantiation)
        public UsersRightPanel(
            IUserPresenceService userPresenceService,
            ApiService apiService,
            ILogger<UsersRightPanel> logger,
            IConnectivityService connectivityService)
        {
            InitializeComponent();
            _userPresenceService = userPresenceService;
            _apiService = apiService;
            _logger = logger;
            _connectivityService = connectivityService;

            BindingContext = this;
            RefreshUsersCommand = new Command(async () => await RefreshUsersAsync());
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await RefreshUsersAsync();

            // Subscribe to presence events
            _userPresenceService.UserStatusChanged += OnUserPresenceServiceStatusChanged;
            _userPresenceService.UserAvailabilityChanged += OnUserAvailabilityChanged;

            // Device-specific behavior
            ConfigureDeviceSpecificBehavior();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            // Unsubscribe from events
            _userPresenceService.UserStatusChanged -= OnUserPresenceServiceStatusChanged;
            _userPresenceService.UserAvailabilityChanged -= OnUserAvailabilityChanged;
        }

        private void ConfigureDeviceSpecificBehavior()
        {
            // Apply platform-specific styling or behavior here
            if (DeviceInfo.Platform == DevicePlatform.iOS || DeviceInfo.Platform == DevicePlatform.Android)
            {
                // Mobile-specific adjustments (already handled by swipe gesture in AppShell)
            }
            else
            {
                // Desktop-specific adjustments
                // Note: The AppShell already handles swipe detection,
                // this would be for additional desktop-specific customizations
            }
        }

        private void OnUserPresenceServiceStatusChanged(object? sender, UserStatusChangedEventArgs e)
        {
            // Skip updates for the current user - they should not be displayed in the panel
            if (App.CurrentUser != null && e.UserId == App.CurrentUser.UserID)
            {
                _logger.LogInformation($"Ignoring status change for current user (ID: {e.UserId})");
                return;
            }

            _logger.LogInformation($"User status changed: UserID {e.UserId}, Status {e.Status}. Updating panel.");

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    // Find and update the user in our collection if they exist
                    var existingUser = _users.FirstOrDefault(u => u.UserId == e.UserId);
                    if (existingUser != null)
                    {
                        // Update the user's status
                        existingUser.Status = e.Status;
                        _logger.LogInformation($"Updated user {e.Username} (ID: {e.UserId}) status to {e.Status} in UsersRightPanel");
                    }
                    else
                    {
                        // If user not in collection and they're not offline, refresh the full list
                        // This handles cases where a user comes online that wasn't in our list before
                        if (e.Status != UserPresenceStatus.Offline)
                        {
                            _logger.LogInformation($"User {e.Username} (ID: {e.UserId}) not found in collection, refreshing panel");
                            await RefreshUsersAsync();
                        }
                        else
                        {
                            _logger.LogInformation($"Ignoring offline status for user not in collection: {e.Username} (ID: {e.UserId})");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error updating user status in UsersRightPanel: {ex.Message}");
                }
            });
        }

        private void OnUserAvailabilityChanged(object? sender, UserAvailabilityChangedEventArgs e)
        {
            // Skip updates for the current user - they should not be displayed in the panel
            if (App.CurrentUser != null && e.UserId == App.CurrentUser.UserID)
            {
                _logger.LogInformation($"Ignoring availability change for current user (ID: {e.UserId})");
                return;
            }

            _logger.LogInformation($"User availability changed: UserID {e.UserId}, Available: {e.IsAvailableForChat}");

            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    var existingUser = _users.FirstOrDefault(u => u.UserId == e.UserId);
                    if (existingUser != null)
                    {
                        existingUser.IsAvailableForChat = e.IsAvailableForChat;
                        _logger.LogInformation($"Updated user {e.Username} (ID: {e.UserId}) availability to {e.IsAvailableForChat} in UsersRightPanel");
                    }
                    // No need to refresh the full list for availability changes
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error updating user availability in UsersRightPanel: {ex.Message}");
                }
            });
        }

        private async Task RefreshUsersAsync()
        {
            if (IsLoading && !IsRefreshing) return; // Avoid concurrent full loads unless it's a pull-to-refresh

            if (!IsRefreshing) // Only set IsLoading if not a pull-to-refresh action
            {
                IsLoading = true;
            }

            try
            {
                _logger.LogInformation("UsersRightPanel: Refreshing users...");

                // Check for connectivity
                var isConnected = _connectivityService.IsConnected();
                Dictionary<int, UserPresenceInfo> onlineUsersDetails;

                // Update offline indicator visibility
                MainThread.BeginInvokeOnMainThread(() => {
                    offlineIndicator.IsVisible = !isConnected;
                });

                if (isConnected)
                {
                    // Get online users from service if connected
                    onlineUsersDetails = await _userPresenceService.GetOnlineUsersAsync();
                }
                else
                {
                    // Use cached data when offline
                    _logger.LogWarning("UsersRightPanel: Device is offline, using cached user data.");
                    onlineUsersDetails = await Task.FromResult(_userPresenceService.GetCachedOnlineUsers());
                }

                // Get the current user ID to exclude from the list
                var currentAppUserId = App.CurrentUser?.UserID ?? 0;

                if (currentAppUserId <= 0)
                {
                    _logger.LogWarning("UsersRightPanel: Current user ID is invalid or not set");
                }
                else
                {
                    _logger.LogInformation($"UsersRightPanel: Will exclude current user ID: {currentAppUserId}");
                }

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var currentUsers = new ObservableCollection<UserViewModel>();
                    if (onlineUsersDetails != null)
                    {
                        // Filter out the current user and create view models for other users
                        var filteredUsers = onlineUsersDetails.Values
                            .Where(u => u.UserId != currentAppUserId && u.UserId > 0) // Exclude current user and ensure valid IDs
                            .ToList();

                        foreach (var userDetail in filteredUsers)
                        {
                            var userVm = new UserViewModel
                            {
                                UserId = userDetail.UserId,
                                Username = userDetail.Username,
                                FullName = userDetail.FullName,
                                Department = userDetail.Department,
                                Status = userDetail.Status,
                                StatusMessage = userDetail.StatusMessage,
                                IsAvailableForChat = userDetail.IsAvailableForChat,
                                ProfilePictureData = userDetail.ProfilePictureData
                            };

                            currentUsers.Add(userVm);
                        }

                        int userCount = currentUsers.Count;
                        int totalUsers = onlineUsersDetails.Count;
                        _logger.LogInformation($"UsersRightPanel: Displaying {userCount} users (filtered from {totalUsers} total users).");
                    }
                    else
                    {
                        _logger.LogWarning("UsersRightPanel: GetOnlineUsersAsync returned null.");
                    }

                    // Update the collection
                    Users.Clear();
                    foreach(var u in currentUsers) Users.Add(u);
                    OnPropertyChanged(nameof(Users));
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UsersRightPanel: Failed to load online users.");
                // Show a non-intrusive error message for the side panel
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (_users.Count == 0)
                    {
                        // Only show error indicators if we don't have any existing data to display
                        // This prevents disrupting the user experience for temporary issues
                        // Consider adding a small indicator in the UI to show connectivity status
                    }
                });
            }
            finally
            {
                IsRefreshing = false;
                IsLoading = false;
            }
        }

        private void ClosePanel_Clicked(object sender, EventArgs e)
        {
            // Check if the current route is the users panel itself
            if (Shell.Current.CurrentState.Location.OriginalString.EndsWith("//users"))
            {
                Shell.Current.GoToAsync(".."); // Navigate back
            }
            else
            {
                // Fallback: Navigate to the main route or a default page if not on the users route
                Shell.Current.GoToAsync("//main");
            }
        }
    }
}