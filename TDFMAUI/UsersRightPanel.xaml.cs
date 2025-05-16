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
        private readonly IUserPresenceService _userPresenceService;
        private readonly ApiService _apiService; 
        private readonly ILogger<UsersRightPanel> _logger;
        private readonly IConnectivityService _connectivityService;

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
            _logger.LogInformation($"User status changed: UserID {e.UserId}, Status {e.Status}. Refreshing panel.");
            MainThread.BeginInvokeOnMainThread(async () => 
            {
                // Find and update the user in our collection if they exist
                var existingUser = _users.FirstOrDefault(u => u.UserId == e.UserId);
                if (existingUser != null)
                {
                    existingUser.Status = e.Status;
                }
                else
                {
                    // If user not in collection, refresh the full list
                    await RefreshUsersAsync();
                }
            });
        }

        private void OnUserAvailabilityChanged(object? sender, UserAvailabilityChangedEventArgs e)
        {
            _logger.LogInformation($"User availability changed: UserID {e.UserId}, Available: {e.IsAvailableForChat}");
            MainThread.BeginInvokeOnMainThread(() => 
            {
                var existingUser = _users.FirstOrDefault(u => u.UserId == e.UserId);
                if (existingUser != null)
                {
                    existingUser.IsAvailableForChat = e.IsAvailableForChat;
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
                
                var currentAppUserId = App.CurrentUser?.UserID ?? 0;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var currentUsers = new ObservableCollection<UserViewModel>();
                    if (onlineUsersDetails != null)
                    {
                        foreach (var userDetail in onlineUsersDetails.Values.Where(u => u.UserId != currentAppUserId)) // Exclude current user
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
                        int userCount = currentUsers?.Count ?? 0;
                        _logger.LogInformation($"UsersRightPanel: Displaying {userCount} users.");
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

        private Color GetStatusColor(UserPresenceStatus status)
        {
            return status switch
            {
                UserPresenceStatus.Online => Colors.Green,
                UserPresenceStatus.Away => Colors.Orange,
                UserPresenceStatus.Busy => Colors.Red,
                UserPresenceStatus.Offline => Colors.Gray,
                UserPresenceStatus.DoNotDisturb => Colors.DarkRed,
                // BeRightBack and AppearingOffline removed as they are not in the enum
                _ => Colors.SlateGray, 
            };
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