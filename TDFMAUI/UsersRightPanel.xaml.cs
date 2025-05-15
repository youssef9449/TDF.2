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
    public partial class UsersRightPanel : ContentPage
    {
        private readonly IUserPresenceService _userPresenceService;
        private readonly ApiService _apiService; 
        private readonly ILogger<UsersRightPanel> _logger;

        private ObservableCollection<UserViewModel> _users = new ObservableCollection<UserViewModel>();
        public ObservableCollection<UserViewModel> Users => _users;

        private bool _isRefreshing;
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
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged(nameof(IsLoading));
            }
        }

        public ICommand RefreshUsersCommand { get; private set; }

        // Constructor should be public for XAML instantiation with DI
        public UsersRightPanel(IUserPresenceService userPresenceService, ApiService apiService, ILogger<UsersRightPanel> logger)
        {
            InitializeComponent();
            _userPresenceService = userPresenceService;
            _apiService = apiService; // Keep for potential future use if presence doesn't have all data
            _logger = logger;

            BindingContext = this;
            RefreshUsersCommand = new Command(async () => await RefreshUsersAsync());
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await RefreshUsersAsync();
            // Assuming UserStatusChanged provides UserStatusChangedEventArgs
            _userPresenceService.UserStatusChanged += OnUserPresenceServiceStatusChanged; 
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _userPresenceService.UserStatusChanged -= OnUserPresenceServiceStatusChanged;
        }

        // Made sender nullable: object?
        private void OnUserPresenceServiceStatusChanged(object? sender, UserStatusChangedEventArgs e)
        {
            _logger.LogInformation($"User status changed: UserID {e.UserId}, Status {e.Status}. Refreshing panel.");
            MainThread.BeginInvokeOnMainThread(async () => await RefreshUsersAsync());
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
                var onlineUsersDetails = await _userPresenceService.GetOnlineUsersAsync();
                
                var currentAppUserId = App.CurrentUser?.UserID ?? 0;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var currentUsers = new ObservableCollection<UserViewModel>();
                    if (onlineUsersDetails != null)
                    {
                        foreach (var userDetail in onlineUsersDetails.Values.Where(u => u.Id != currentAppUserId)) // Exclude current user
                        {
                            currentUsers.Add(new UserViewModel
                            {
                                UserId = userDetail.Id,
                                Username = userDetail.Username,
                                FullName = userDetail.FullName,
                                Department = userDetail.Department,
                                Status = userDetail.Status,
                                StatusMessage = userDetail.StatusMessage,
                                IsAvailableForChat = userDetail.IsAvailableForChat,
                                HasStatusMessage = !string.IsNullOrEmpty(userDetail.StatusMessage),
                                StatusColor = GetStatusColor(userDetail.Status),
                                // UserPresenceInfo (userDetail) does not have ProfilePictureData.
                                // UserViewModel.ProfilePictureData will be null by default.
                                // The ByteArrayToImageSourceConverter in XAML should handle null.
                                ProfilePictureData = null 
                            });
                        }
                        int userCount = currentUsers?.Count ?? 0;
                         _logger.LogInformation($"UsersRightPanel: Displaying {userCount} users.");
                    }
                    else
                    {
                        _logger.LogWarning("UsersRightPanel: GetOnlineUsersAsync returned null.");
                    }
                    // Efficiently update the collection by replacing it
                    // This handles adds, removes, and updates in one go after fetching all current online users.
                    Users.Clear();
                    foreach(var u in currentUsers) Users.Add(u);
                    OnPropertyChanged(nameof(Users)); // Notify UI if necessary, though ObservableCollection should handle it

                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UsersRightPanel: Failed to load online users.");
                // Avoid showing DisplayAlert if the panel is not visible or not primary focus.
                // Consider a less intrusive way to report errors for a side panel.
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