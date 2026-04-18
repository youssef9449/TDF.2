using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using TDFMAUI.Services;
using TDFShared.Enums;
using TDFShared.DTOs.Users;
using TDFShared.Services;
using TDFMAUI.Services.Presence;

namespace TDFMAUI.ViewModels
{
    public partial class UsersRightPanelViewModel : BaseViewModel
    {
        private readonly TDFMAUI.Services.Presence.IUserPresenceService _userPresenceService;
        private readonly IUserApiService _userApiService;
        private readonly ILogger<UsersRightPanelViewModel> _logger;
        private readonly IConnectivityService _connectivityService;

        [ObservableProperty]
        private ObservableCollection<UserViewModel> _users = new();

        [ObservableProperty]
        private bool _isRefreshing;

        [ObservableProperty]
        private int _currentPage = 1;

        [ObservableProperty]
        private int _totalPages = 1;

        [ObservableProperty]
        private bool _hasNextPage;

        [ObservableProperty]
        private bool _hasPreviousPage;

        [ObservableProperty]
        private bool _hasPagination = true;

        [ObservableProperty]
        private bool _isOffline;

        public UsersRightPanelViewModel(
            TDFMAUI.Services.Presence.IUserPresenceService userPresenceService,
            IUserApiService userApiService,
            ILogger<UsersRightPanelViewModel> logger,
            IConnectivityService connectivityService)
        {
            _userPresenceService = userPresenceService;
            _userApiService = userApiService;
            _logger = logger;
            _connectivityService = connectivityService;
        }

        [RelayCommand]
        public async Task RefreshUsersAsync()
        {
            if (IsBusy && !IsRefreshing) return;
            if (!IsRefreshing) IsBusy = true;

            try
            {
                IsOffline = !_connectivityService.IsConnected();
                Dictionary<int, UserPresenceInfo> onlineUsersDetails;

                if (!IsOffline)
                {
                    var paginatedResult = await _userPresenceService.GetOnlineUsersAsync(CurrentPage, 10);
                    onlineUsersDetails = paginatedResult.Items.ToDictionary(u => u.UserId);
                    TotalPages = paginatedResult.TotalPages;
                    HasNextPage = paginatedResult.HasNextPage;
                    HasPreviousPage = paginatedResult.HasPreviousPage;
                    HasPagination = paginatedResult.TotalPages > 1;
                }
                else
                {
                    onlineUsersDetails = await Task.FromResult(_userPresenceService.GetCachedOnlineUsers());
                }

                var currentAppUserId = App.CurrentUser?.UserID ?? 0;
                Users.Clear();
                foreach (var userDetail in onlineUsersDetails.Values.Where(u => u.UserId != currentAppUserId && u.UserId > 0))
                {
                    Users.Add(new UserViewModel
                    {
                        UserId = userDetail.UserId,
                        Username = userDetail.Username,
                        FullName = userDetail.FullName,
                        Department = userDetail.Department,
                        Status = userDetail.Status,
                        StatusMessage = userDetail.StatusMessage,
                        IsAvailableForChat = userDetail.IsAvailableForChat,
                        ProfilePictureData = userDetail.ProfilePictureData
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load online users in UsersRightPanelViewModel");
            }
            finally
            {
                IsRefreshing = false;
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task LoadNextPageAsync()
        {
            if (HasNextPage)
            {
                CurrentPage++;
                await RefreshUsersAsync();
            }
        }

        [RelayCommand]
        private async Task LoadPreviousPageAsync()
        {
            if (HasPreviousPage)
            {
                CurrentPage--;
                await RefreshUsersAsync();
            }
        }

        public void HandleUserStatusChanged(UserStatusChangedEventArgs e)
        {
            if (App.CurrentUser != null && e.UserId == App.CurrentUser.UserID) return;
            var existingUser = Users.FirstOrDefault(u => u.UserId == e.UserId);
            if (existingUser != null) existingUser.Status = e.Status;
            else if (e.Status != UserPresenceStatus.Offline) _ = RefreshUsersAsync();
        }

        public void HandleUserAvailabilityChanged(UserAvailabilityChangedEventArgs e)
        {
            if (App.CurrentUser != null && e.UserId == App.CurrentUser.UserID) return;
            var existingUser = Users.FirstOrDefault(u => u.UserId == e.UserId);
            if (existingUser != null) existingUser.IsAvailableForChat = e.IsAvailableForChat;
        }
    }
}
