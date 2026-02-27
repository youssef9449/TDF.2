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
using TDFShared.Services;
using TDFShared.DTOs.Common;
using TDFShared.DTOs.Users;
using TDFShared.Constants;

namespace TDFMAUI
{
    /// <summary>
    /// Right-side panel that displays online users with their status
    /// </summary>
    public partial class UsersRightPanel : ContentPage
    {
        private const int DefaultPageSize = 10;
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

        // Pagination properties
        private int _currentPage = 1;
        public int CurrentPage
        {
            get => _currentPage;
            set
            {
                _currentPage = value;
                OnPropertyChanged(nameof(CurrentPage));
            }
        }

        private int _totalPages = 1;
        public int TotalPages
        {
            get => _totalPages;
            set
            {
                _totalPages = value;
                OnPropertyChanged(nameof(TotalPages));
            }
        }

        private bool _hasNextPage;
        public bool HasNextPage
        {
            get => _hasNextPage;
            set
            {
                _hasNextPage = value;
                OnPropertyChanged(nameof(HasNextPage));
            }
        }

        private bool _hasPreviousPage;
        public bool HasPreviousPage
        {
            get => _hasPreviousPage;
            set
            {
                _hasPreviousPage = value;
                OnPropertyChanged(nameof(HasPreviousPage));
            }
        }

        private bool _hasPagination = true;
        public bool HasPagination
        {
            get => _hasPagination;
            set
            {
                _hasPagination = value;
                OnPropertyChanged(nameof(HasPagination));
            }
        }

        /// <summary>
        /// Command to refresh the user list
        /// </summary>
        public ICommand RefreshUsersCommand { get; private set; }
        public ICommand NextPageCommand { get; private set; }
        public ICommand PreviousPageCommand { get; private set; }

        // Parameterless constructor for XAML instantiation
        public UsersRightPanel() : this(
            App.Services?.GetService<IUserPresenceService>(),
            App.Services?.GetService<ApiService>(),
            App.Services?.GetService<ILogger<UsersRightPanel>>(),
            App.Services?.GetService<IConnectivityService>())
        {
        }

        // Constructor with dependency injection
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

            InitializePanel();
        }

        private void InitializePanel()
        {
            if (_userPresenceService == null) _logger?.LogCritical("UsersRightPanel: IUserPresenceService is null.");
            if (_apiService == null) _logger?.LogCritical("UsersRightPanel: ApiService is null.");
            if (_connectivityService == null) _logger?.LogCritical("UsersRightPanel: IConnectivityService is null.");
            if (_logger == null) System.Diagnostics.Debug.WriteLine("[CRITICAL] UsersRightPanel: ILogger is null.");

            BindingContext = this;
            RefreshUsersCommand = new Command(async () => await RefreshUsersAsync());
            NextPageCommand = new Command(async () => await LoadNextPageAsync());
            PreviousPageCommand = new Command(async () => await LoadPreviousPageAsync());
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            
            try
            {
                // Unsubscribe first to avoid duplicate subscriptions
                if (_userPresenceService != null)
                {
                    _userPresenceService.UserStatusChanged -= OnUserPresenceServiceStatusChanged;
                    _userPresenceService.UserAvailabilityChanged -= OnUserAvailabilityChanged;

                    // Subscribe to presence events
                    _userPresenceService.UserStatusChanged += OnUserPresenceServiceStatusChanged;
                    _userPresenceService.UserAvailabilityChanged += OnUserAvailabilityChanged;
                    _logger?.LogInformation("Subscribed to user presence events");
                }
                else
                {
                    _logger?.LogWarning("UserPresenceService is null, cannot subscribe to events");
                }

                // Initial load of users
                await RefreshUsersAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in OnAppearing: {Message}", ex.Message);
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            
            try
            {
                // Unsubscribe from events
                if (_userPresenceService != null)
                {
                    _userPresenceService.UserStatusChanged -= OnUserPresenceServiceStatusChanged;
                    _userPresenceService.UserAvailabilityChanged -= OnUserAvailabilityChanged;
                    _logger?.LogInformation("Unsubscribed from user presence events");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in OnDisappearing: {Message}", ex.Message);
            }
        }

        private void ConfigureDeviceSpecificBehavior()
        {
            // Apply platform-specific styling or behavior here
            if (DeviceHelper.IsMobile)
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
                _logger?.LogInformation("Ignoring status change for current user (ID: {UserId})", e.UserId);
                return;
            }

            _logger?.LogInformation("User status changed: UserID {UserId}, Status {Status}, Username {Username}. Updating panel.", 
                e.UserId, e.Status, e.Username);

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
                        _logger?.LogInformation("Updated user {Username} (ID: {UserId}) status to {Status} in UsersRightPanel", 
                            existingUser.Username, e.UserId, e.Status);
                    }
                    else if (e.Status != UserPresenceStatus.Offline)
                    {
                        // If user not in collection and they're not offline, refresh the full list
                        _logger?.LogInformation("User {Username} (ID: {UserId}) not found in collection, refreshing panel", 
                            e.Username, e.UserId);
                        await RefreshUsersAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error updating user status in UsersRightPanel: {Message}", ex.Message);
                }
            });
        }

        private void OnUserAvailabilityChanged(object? sender, UserAvailabilityChangedEventArgs e)
        {
            // Skip updates for the current user
            if (App.CurrentUser != null && e.UserId == App.CurrentUser.UserID)
            {
                _logger?.LogInformation("Ignoring availability change for current user (ID: {UserId})", e.UserId);
                return;
            }

            _logger?.LogInformation("User availability changed: UserID {UserId}, Available: {IsAvailableForChat}", e.UserId, e.IsAvailableForChat);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    var existingUser = _users.FirstOrDefault(u => u.UserId == e.UserId);
                    if (existingUser != null)
                    {
                        existingUser.IsAvailableForChat = e.IsAvailableForChat;
                        _logger?.LogInformation("Updated user {Username} (ID: {UserId}) availability to {IsAvailableForChat} in UsersRightPanel", e.Username, e.UserId, e.IsAvailableForChat);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error updating user availability in UsersRightPanel: {Message}", ex.Message);
                }
            });
        }

        private async Task LoadNextPageAsync()
        {
            if (HasNextPage)
            {
                CurrentPage++;
                await RefreshUsersAsync();
            }
        }

        private async Task LoadPreviousPageAsync()
        {
            if (HasPreviousPage)
            {
                CurrentPage--;
                await RefreshUsersAsync();
            }
        }

        private async Task RefreshUsersAsync()
        {
            if (IsLoading && !IsRefreshing) return;

            if (!IsRefreshing)
            {
                IsLoading = true;
            }

            try
            {
                _logger?.LogInformation("UsersRightPanel: Refreshing users... Page {Page}", CurrentPage);

                var isConnected = _connectivityService.IsConnected();
                Dictionary<int, UserPresenceInfo> onlineUsersDetails;

                MainThread.BeginInvokeOnMainThread(() => {
                    offlineIndicator.IsVisible = !isConnected;
                });

                if (isConnected)
                {
                    // Get users from the centralized presence service with pagination
                    var paginatedResponse = await _userPresenceService.GetOnlineUsersAsync(CurrentPage, DefaultPageSize);
                    
                    if (paginatedResponse != null && paginatedResponse.Items != null)
                    {
                        onlineUsersDetails = paginatedResponse.Items.ToDictionary(u => u.UserId);

                        // Update pagination info
                        TotalPages = paginatedResponse.TotalPages;
                        HasNextPage = paginatedResponse.HasNextPage;
                        HasPreviousPage = paginatedResponse.HasPreviousPage;
                        HasPagination = TotalPages > 1;

                        _logger?.LogInformation("UsersRightPanel: Retrieved {Count} users from service", onlineUsersDetails.Count);
                    }
                    else
                    {
                        _logger?.LogWarning("UsersRightPanel: Presence service returned null or empty result");
                        onlineUsersDetails = new Dictionary<int, UserPresenceInfo>();
                    }
                }
                else
                {
                    _logger?.LogWarning("UsersRightPanel: Device is offline, using cached user data.");
                    onlineUsersDetails = await Task.FromResult(_userPresenceService.GetCachedOnlineUsers());
                }

                var currentAppUserId = App.CurrentUser?.UserID ?? 0;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var currentUsers = new ObservableCollection<UserViewModel>();
                    if (onlineUsersDetails != null)
                    {
                        var filteredUsers = onlineUsersDetails.Values
                            .Where(u => u.UserId != currentAppUserId && u.UserId > 0)
                            .ToList();

                        foreach (var userDetail in filteredUsers)
                        {
                            _logger?.LogDebug("Processing user: ID={UserId}, Name={Username}, Status={Status}", 
                                userDetail.UserId, userDetail.Username, userDetail.Status);

                            var userVm = new UserViewModel
                            {
                                UserId = userDetail.UserId,
                                Username = userDetail.Username,
                                FullName = userDetail.FullName,
                                Department = userDetail.Department,
                                Status = userDetail.Status,
                                StatusMessage = userDetail.StatusMessage,
                                IsAvailableForChat = userDetail.IsAvailableForChat,
                                ProfilePictureData = userDetail.ProfilePictureData != null && userDetail.ProfilePictureData.Length > 0 
                                    ? userDetail.ProfilePictureData 
                                    : null
                            };

                            currentUsers.Add(userVm);
                        }

                        int userCount = currentUsers.Count;
                        int totalUsers = onlineUsersDetails.Count;
                        _logger?.LogInformation("UsersRightPanel: Displaying {UserCount} users (filtered from {TotalUsers} total users). Page {CurrentPage} of {TotalPages}", 
                            userCount, totalUsers, CurrentPage, TotalPages);
                    }
                    else
                    {
                        _logger?.LogWarning("UsersRightPanel: GetOnlineUsersAsync returned null.");
                    }

                    Users.Clear();
                    foreach(var u in currentUsers) Users.Add(u);
                    OnPropertyChanged(nameof(Users));
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "UsersRightPanel: Failed to load online users.");
            }
            finally
            {
                IsRefreshing = false;
                IsLoading = false;
            }
        }

        private async void ClosePanel_Clicked(object sender, EventArgs e)
        {
            _logger.LogInformation("Close panel button clicked");

            if (Shell.Current is AppShell appShell)
            {
                // Use the AppShell's method to close the panel
                await appShell.CloseUsersRightPanelAsync();
            }
            else
            {
                // Fallback if AppShell is not available
                _logger.LogWarning("AppShell not available, using fallback navigation");

                // Try to navigate back if possible
                if (Shell.Current.Navigation.NavigationStack.Count > 1)
                {
                    await Shell.Current.GoToAsync("..", true);
                }
                else
                {
                    // Navigate to the root as a last resort
                    await Shell.Current.GoToAsync("//", true);
                }
            }
        }
    }
}