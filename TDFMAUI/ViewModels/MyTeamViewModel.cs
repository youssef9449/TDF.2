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
    /// <summary>
    /// ViewModel for MyTeamPage.
    /// </summary>
    // NOTE: The FullName property used in the XAML should be on the UserDto type (team member), not this view model.
    public partial class MyTeamViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _profileImageUrl;

        // Add properties and commands as needed later.
        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private ObservableCollection<UserDto> _teamMembers;

        [ObservableProperty]
        private string _errorMessage;

        [ObservableProperty]
        private bool _hasError;

        private readonly ApiService _apiService;
        private readonly TDFShared.Services.IAuthService _authService;
        private readonly INavigationService _navigationService;

        // Constructor injection
        public MyTeamViewModel(
            ApiService apiService, 
            TDFShared.Services.IAuthService authService,
            INavigationService navigationService)
        {
            _apiService = apiService;
            _authService = authService;
            _navigationService = navigationService;
            TeamMembers = new ObservableCollection<UserDto>();
            // Load data when the view model is created
            // Consider moving to an OnAppearing/NavigatedTo method if more appropriate
            _ = LoadTeamAsync();
        }

        // Example command placeholder
        [RelayCommand]
        private async Task LoadTeamAsync()
        {
            IsLoading = true;
            HasError = false;
            ErrorMessage = string.Empty;

            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    await _navigationService.NavigateToAsync("//Login");
                    return;
                }

                var userDto = new UserDto
                {
                    UserID = currentUser.UserID,
                    IsAdmin = currentUser.IsAdmin,
                    IsHR = currentUser.IsHR,
                    IsManager = currentUser.IsManager,
                    Department = currentUser.Department
                };

                // Use AuthorizationUtilities to check access
                if (!AuthorizationUtilities.IsManagement(userDto))
                {
                    await _navigationService.NavigateToAsync("//Home");
                    return;
                }

                // Get accessible departments using AuthorizationUtilities
                var allDepartments = await _apiService.GetDepartmentsAsync();
                var departmentNames = allDepartments.Select(d => d.Name);
                var accessibleDepartments = AuthorizationUtilities.GetAccessibleDepartments(userDto, departmentNames);

                TeamMembers.Clear();
                foreach (var department in accessibleDepartments)
                {
                    var members = await _apiService.GetUsersByDepartmentAsync(department);
                    if (members != null)
                    {
                        foreach (var member in members)
                        {
                            // Filter out the current user
                            if (member.UserID != currentUser.UserID)
                            {
                                // For managers, validate they can manage this user's department
                                if (userDto.IsManager && !userDto.IsAdmin && !userDto.IsHR)
                                {
                                    if (AuthorizationUtilities.CanAccessDepartment(userDto, member.Department))
                                    {
                                        TeamMembers.Add(member);
                                    }
                                }
                                else
                                {
                                    // HR and Admin can see all users
                                    TeamMembers.Add(member);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = "Failed to load team members. Please try again.";
                System.Diagnostics.Debug.WriteLine($"Error loading team members: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task ViewProfileAsync(int userId)
        {
            try
            {
                await _navigationService.NavigateToAsync($"UserProfile?userId={userId}");
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = "Failed to navigate to user profile.";
                System.Diagnostics.Debug.WriteLine($"Error navigating to profile: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task MessageAsync(int userId)
        {
            try
            {
                await _navigationService.NavigateToAsync($"NewMessage?recipientId={userId}");
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = "Failed to start new message.";
                System.Diagnostics.Debug.WriteLine($"Error starting message: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task ViewRequestsAsync(int userId)
        {
            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    await _navigationService.NavigateToAsync("//Login");
                    return;
                }

                var userDto = new UserDto
                {
                    UserID = currentUser.UserID,
                    IsAdmin = currentUser.IsAdmin,
                    IsHR = currentUser.IsHR,
                    IsManager = currentUser.IsManager,
                    Department = currentUser.Department
                };

                // Get the target user
                var targetUser = TeamMembers.FirstOrDefault(u => u.UserID == userId);
                if (targetUser == null)
                {
                    HasError = true;
                    ErrorMessage = "User not found.";
                    return;
                }

                // Check if current user can view target user's requests
                if (!AuthorizationUtilities.CanAccessDepartment(userDto, targetUser.Department))
                {
                    HasError = true;
                    ErrorMessage = "You don't have permission to view this user's requests.";
                    return;
                }

                await _navigationService.NavigateToAsync($"RequestApproval?userId={userId}");
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = "Failed to view user requests.";
                System.Diagnostics.Debug.WriteLine($"Error viewing requests: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            await LoadTeamAsync();
        }
    }
}