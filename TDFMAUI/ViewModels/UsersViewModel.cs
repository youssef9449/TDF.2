using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Graphics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using TDFMAUI.Services;
using TDFShared.Enums;
using TDFShared.DTOs.Users;
using TDFMAUI.Pages;

namespace TDFMAUI.ViewModels
{
    public partial class UsersViewModel : BaseViewModel
    {
        private readonly IUserPresenceService _userPresenceService;
        private readonly IUserApiService _userApiService;
        private readonly ILogger<UsersViewModel> _logger;

        [ObservableProperty]
        private ObservableCollection<UserViewModel> _users = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusColor))]
        [NotifyPropertyChangedFor(nameof(StatusDisplay))]
        private UserPresenceStatus _currentStatus = UserPresenceStatus.Online;

        public Color StatusColor => UserViewModel.GetColorForStatus(CurrentStatus);

        public string StatusDisplay => CurrentStatus switch
        {
            UserPresenceStatus.Online => "Online",
            UserPresenceStatus.Away => "Away",
            UserPresenceStatus.Busy => "Busy",
            UserPresenceStatus.DoNotDisturb => "Do Not Disturb",
            UserPresenceStatus.Offline => "Offline",
            _ => "Unknown"
        };

        public UsersViewModel(
            IUserPresenceService userPresenceService,
            IUserApiService userApiService,
            ILogger<UsersViewModel> logger)
        {
            _userPresenceService = userPresenceService;
            _userApiService = userApiService;
            _logger = logger;
            Title = "Users";
        }

        [RelayCommand]
        public async Task RefreshUsersAsync()
        {
            if (App.CurrentUser == null) return;

            IsBusy = true;
            try
            {
                var onlineUsers = await _userPresenceService.GetOnlineUsersAsync();
                Users.Clear();
                foreach (var user in onlineUsers.Values.Where(u => u.UserId != App.CurrentUser.UserID))
                {
                    Users.Add(new UserViewModel
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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load online users");
                ErrorMessage = "Failed to load online users.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task ChangeStatusAsync()
        {
            var result = await Shell.Current.DisplayActionSheet(
                "Set Status", "Cancel", null, "Online", "Away", "Busy", "Do Not Disturb");

            if (string.IsNullOrEmpty(result) || result == "Cancel") return;

            CurrentStatus = result switch
            {
                "Online" => UserPresenceStatus.Online,
                "Away" => UserPresenceStatus.Away,
                "Busy" => UserPresenceStatus.Busy,
                "Do Not Disturb" => UserPresenceStatus.DoNotDisturb,
                _ => UserPresenceStatus.Online
            };

            await _userPresenceService.UpdateStatusAsync(CurrentStatus, string.Empty);
        }

        [RelayCommand]
        private async Task MessageUserAsync(int userId)
        {
            var user = Users.FirstOrDefault(u => u.UserId == userId);
            if (user == null) return;

            var messageApiService = App.Services.GetService<IMessageService>();
            await Shell.Current.Navigation.PushAsync(new NewMessagePage(messageApiService)
            {
                PreSelectedUserId = userId,
                PreSelectedUserName = user.FullName
            });
        }

        [RelayCommand]
        private async Task ViewProfileAsync(int userId)
        {
            var user = Users.FirstOrDefault(u => u.UserId == userId);
            if (user == null) return;

            var localStorageService = App.Services.GetService<ILocalStorageService>();
            var viewModel = new UserProfileViewModel(_userApiService, localStorageService);
            await viewModel.LoadUserByIdAsync(userId);

            await Shell.Current.Navigation.PushAsync(new UserProfilePage(viewModel, localStorageService));
        }

        public void HandleUserStatusChanged(UserStatusChangedEventArgs e)
        {
            var userVM = Users.FirstOrDefault(u => u.UserId == e.UserId);
            if (userVM != null)
            {
                userVM.Status = e.Status;
                if (e.UserId == App.CurrentUser?.UserID) CurrentStatus = e.Status;
            }
            else if (e.Status != UserPresenceStatus.Offline)
            {
                _ = RefreshUsersAsync();
            }
        }

        public void HandleUserAvailabilityChanged(UserAvailabilityChangedEventArgs e)
        {
            var userVM = Users.FirstOrDefault(u => u.UserId == e.UserId);
            if (userVM != null) userVM.IsAvailableForChat = e.IsAvailableForChat;
        }
    }
}
