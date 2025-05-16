using System.Collections.ObjectModel;
using System.Windows.Input;
using TDFMAUI.Services;
using TDFMAUI.ViewModels;
using TDFShared.Enums;
using Microsoft.Extensions.Logging;
using TDFMAUI.Helpers;

namespace TDFMAUI.Pages
{
    public partial class UsersPage : ContentPage
    {
        private readonly IUserPresenceService _userPresenceService;
        private readonly INotificationService _notificationService;
        private readonly ApiService _apiService;
        private readonly ILogger<UsersPage> _logger;
        
        private ObservableCollection<UserViewModel> _users = new ObservableCollection<UserViewModel>();
        private UserPresenceStatus _currentStatus = UserPresenceStatus.Online;
        
        // Commands for user interactions
        public ICommand MessageCommand { get; private set; }
        public ICommand ViewProfileCommand { get; private set; }
        
        public UsersPage(
            IUserPresenceService userPresenceService,
            INotificationService notificationService,
            ApiService apiService,
            ILogger<UsersPage> logger)
        {
            InitializeComponent();
            
            _userPresenceService = userPresenceService;
            _notificationService = notificationService;
            _apiService = apiService;
            _logger = logger;
            
            // Initialize commands
            MessageCommand = new Command<int>(OnMessageUser);
            ViewProfileCommand = new Command<int>(OnViewUserProfile);
            
            usersCollection.ItemsSource = _users;
            refreshView.Command = new Command(async () => await RefreshUsers());
        }
        
        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // Subscribe to events
            _userPresenceService.UserStatusChanged += OnUserStatusChanged;
            _userPresenceService.UserAvailabilityChanged += OnUserAvailabilityChanged;
            _userPresenceService.AvailabilityConfirmed += OnAvailabilityConfirmed;
            _userPresenceService.StatusUpdateConfirmed += OnStatusUpdateConfirmed;
            _userPresenceService.PresenceErrorReceived += OnPresenceErrorReceived;

            await RefreshUsers();
            UpdateMyStatusDisplay();
        }
        
        protected override void OnDisappearing()
        {
             // Unsubscribe from events
            _userPresenceService.UserStatusChanged -= OnUserStatusChanged;
            _userPresenceService.UserAvailabilityChanged -= OnUserAvailabilityChanged;
            _userPresenceService.AvailabilityConfirmed -= OnAvailabilityConfirmed;
            _userPresenceService.StatusUpdateConfirmed -= OnStatusUpdateConfirmed;
            _userPresenceService.PresenceErrorReceived -= OnPresenceErrorReceived;

            base.OnDisappearing();
        }
        
        private async Task RefreshUsers()
        {
            if (App.CurrentUser == null)
            {
                return;
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
                    
                    foreach (var user in onlineUsers.Values.Where(u => u.UserId != App.CurrentUser.UserID))
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
                            ProfilePictureData = user.ProfilePictureData
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to load online users: {ex.Message}", "OK");
            }
            finally
            {
                refreshView.IsRefreshing = false;
                loadingIndicator.IsVisible = false;
                loadingIndicator.IsRunning = false;
            }
        }
        
        private void UpdateMyStatusDisplay()
        {
            if (App.CurrentUser == null)
            {
                return;
            }
            
            myStatusLabel.Text = _currentStatus.ToString();
            myStatusIndicator.BackgroundColor = UserViewModel.GetColorForStatus(_currentStatus);
            
            // Update availability checkbox
            var currentUserViewModel = _users.FirstOrDefault(u => u.UserId == App.CurrentUser.UserID);
            if (currentUserViewModel != null)
            {
                availableForChatCheckbox.IsChecked = currentUserViewModel.IsAvailableForChat;
            }
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
                    _logger.LogDebug("Updated UI for user {UserId} status change to {Status}", e.UserId, e.Status);

                    // If the change is for the current user, update their specific UI
                    if (e.UserId == App.CurrentUser?.UserID)
                    {
                        _currentStatus = e.Status; // Keep local status in sync
                        UpdateMyStatusDisplay();
                    }
                }
                else
                {
                    // User might have come online and wasn't in the list before
                    _logger.LogDebug("User {UserId} status changed but not found in current list. Refreshing list.", e.UserId);
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
                     _logger.LogDebug("Updated UI for user {UserId} availability change to {IsAvailable}", e.UserId, e.IsAvailableForChat);
                    // If the change is for the current user, update their specific UI
                    if (e.UserId == App.CurrentUser?.UserID)
                    {
                        UpdateMyStatusDisplay(); // Re-sync checkbox state
                    }
                }
                 else
                 {
                    _logger.LogDebug("User {UserId} availability changed but not found in current list.", e.UserId);
                    // Optionally trigger a refresh if this case is important
                 }
             });
        }
        
        private async void OnRefreshClicked(object sender, EventArgs e)
        {
            await RefreshUsers();
        }
        
        private void OnMyStatusClicked(object sender, EventArgs e)
        {
            myStatusFrame.IsVisible = !myStatusFrame.IsVisible;
        }
        
        private async void OnChangeStatusClicked(object sender, EventArgs e)
        {
            var result = await DisplayActionSheet(
                "Set Status", 
                "Cancel", 
                null, 
                "Online", 
                "Away", 
                "Busy", 
                "Do Not Disturb");
                
            if (result == null || result == "Cancel")
            {
                return;
            }
            
            _currentStatus = result switch
            {
                "Online" => UserPresenceStatus.Online,
                "Away" => UserPresenceStatus.Away,
                "Busy" => UserPresenceStatus.Busy,
                "Do Not Disturb" => UserPresenceStatus.DoNotDisturb,
                _ => UserPresenceStatus.Online
            };
            
            // Update service
            await _userPresenceService.UpdateStatusAsync(_currentStatus, statusMessageEntry.Text);
            
            // Update UI
            UpdateMyStatusDisplay();
        }
        
        private async void OnStatusMessageCompleted(object sender, EventArgs e)
        {
            var statusMessage = statusMessageEntry.Text;
            await _userPresenceService.UpdateStatusAsync(_currentStatus, statusMessage);
        }
        
        private async void OnAvailableForChatChanged(object sender, CheckedChangedEventArgs e)
        {
            await _userPresenceService.SetAvailabilityForChatAsync(e.Value);
        }
        
        private async void OnMessageUser(int userId)
        {
            var user = _users.FirstOrDefault(u => u.UserId == userId);
            if (user == null)
            {
                return;
            }
            
            await Navigation.PushAsync(new NewMessagePage(_apiService)
            {
                PreSelectedUserId = userId,
                PreSelectedUserName = user.FullName
            });
        }
        
        private async void OnViewUserProfile(int userId)
        {
            var user = _users.FirstOrDefault(u => u.UserId == userId);
            if (user == null)
            {
                return;
            }
            
            // Create the ViewModel and pass it to the UserProfilePage
            var localStorageService = App.Services.GetService<ILocalStorageService>();
            var viewModel = new UserProfileViewModel(_apiService, localStorageService);
            
            // Call the new method to load the specific user by ID
            await viewModel.LoadUserByIdAsync(userId);
            
            // Navigation to user profile page with proper parameters
            await Navigation.PushAsync(new UserProfilePage(viewModel, localStorageService));
        }

        // New Handlers for Confirmations/Errors
        private void OnAvailabilityConfirmed(object sender, AvailabilitySetEventArgs e)
        {
            _logger.LogInformation("UI Received Confirmation: Availability set to {IsAvailable}", e.IsAvailable);
            // Optional: Show a brief success indicator, e.g., change a border color briefly
            // MainThread.BeginInvokeOnMainThread(async () => { 
            //    myAvailabilitySection.BorderColor = Colors.Green;
            //    await Task.Delay(1000);
            //    myAvailabilitySection.BorderColor = Colors.Transparent; // Or original color
            // });
        }

        private void OnStatusUpdateConfirmed(object sender, StatusUpdateConfirmedEventArgs e)
        {
            _logger.LogInformation("UI Received Confirmation: Status updated to {Status}", e.Status);
            // Update local state if necessary (though OnUserStatusChanged should handle this)
            _currentStatus = _userPresenceService.ParseStatus(e.Status); // Ensure local state matches confirmed state
            // Update the display immediately based on confirmation
            MainThread.BeginInvokeOnMainThread(() => {
                 UpdateMyStatusDisplay();
                 // Optional: Show brief success indicator
            });
        }

        private void OnPresenceErrorReceived(object sender, WebSocketErrorEventArgs e)
        {
            _logger.LogError("UI Received Error: Code={Code}, Message={Message}", e.ErrorCode ?? "N/A", e.ErrorMessage);
            // Show error to user
            MainThread.BeginInvokeOnMainThread(() => {
                DisplayAlert("Presence Error", e.ErrorMessage, "OK");
            });
        }
    }
} 