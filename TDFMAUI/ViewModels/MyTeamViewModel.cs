using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using TDFShared.DTOs.Users;
using TDFShared.Services;
using TDFMAUI.Services;
using System.Threading.Tasks;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using TDFMAUI.Helpers;
using TDFShared.Utilities;
using TDFShared.DTOs.Requests;
using TDFShared.DTOs.Common;

namespace TDFMAUI.ViewModels
{
    public partial class MyTeamViewModel : BaseViewModel
    {
        private readonly IUserApiService _userApiService;
        private readonly ILookupApiService _lookupApiService;
        private readonly IAuthService _authService;
        private readonly INavigationService _navigationService;

        [ObservableProperty]
        private string _profileImageUrl = string.Empty;

        [ObservableProperty]
        private ObservableCollection<UserDto> _teamMembers = new();

        public MyTeamViewModel(
            IUserApiService userApiService,
            ILookupApiService lookupApiService,
            IAuthService authService,
            INavigationService navigationService)
        {
            _userApiService = userApiService;
            _lookupApiService = lookupApiService;
            _authService = authService;
            _navigationService = navigationService;
            Title = "My Team";
            _ = LoadTeamAsync();
        }

        [RelayCommand]
        private async Task LoadTeamAsync()
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    await _navigationService.NavigateToAsync("//Login");
                    return;
                }

                if (!AuthorizationUtilities.IsManagement(currentUser))
                {
                    await _navigationService.NavigateToAsync("//Home");
                    return;
                }

                var allDepartmentsResponse = await _lookupApiService.GetDepartmentsAsync();
                var departmentNames = allDepartmentsResponse?.Data?.Select(d => d.Name);
                var accessibleDepartments = AuthorizationUtilities.GetAccessibleDepartments(currentUser, departmentNames);

                TeamMembers.Clear();
                foreach (var department in accessibleDepartments)
                {
                    var members = await _userApiService.GetUsersByDepartmentAsync(department);
                    if (members != null)
                    {
                        foreach (var member in members.Where(m => m.UserID != currentUser.UserID))
                        {
                            if (AuthorizationUtilities.CanAccessDepartment(currentUser, member.Department))
                            {
                                TeamMembers.Add(member);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to load team members.";
                System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        private async Task ViewProfileAsync(int userId) => await _navigationService.NavigateToAsync($"UserProfile?userId={userId}");

        [RelayCommand]
        private async Task MessageAsync(int userId) => await _navigationService.NavigateToAsync($"NewMessage?recipientId={userId}");

        [RelayCommand]
        private async Task ViewRequestsAsync(int userId)
        {
            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser == null) return;

            var targetUser = TeamMembers.FirstOrDefault(u => u.UserID == userId);
            if (targetUser == null) return;

            if (!AuthorizationUtilities.CanAccessDepartment(currentUser, targetUser.Department))
            {
                ErrorMessage = "No permission.";
                return;
            }
            await _navigationService.NavigateToAsync($"RequestApproval?userId={userId}");
        }

        [RelayCommand]
        private async Task RefreshAsync() => await LoadTeamAsync();
    }
}
