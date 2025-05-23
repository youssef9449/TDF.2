using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using TDFShared.DTOs.Users;
using TDFShared.Services;
using TDFMAUI.Services;
using System.Threading.Tasks;
using System.Linq;
using CommunityToolkit.Mvvm.Input;

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

        private readonly ApiService _apiService;
        private readonly IAuthService _authService;

        // Constructor injection
        public MyTeamViewModel(ApiService apiService, IAuthService authService)
        {
             _apiService = apiService;
             _authService = authService;
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
             try
             {
                // Get current user from auth service
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    // Handle case where user is not logged in
                    // Maybe display a message or navigate away
                    return;
                }

                // Convert to UserDto for RequestStateManager
                var userDto = new UserDto
                {
                    UserID = currentUser.UserID,
                    IsAdmin = currentUser.IsAdmin,
                    IsHR = currentUser.IsHR,
                    IsManager = currentUser.IsManager,
                    Department = currentUser.Department
                };

                // Check if user can manage requests (managers, HR, admin can see team members)
                if (!RequestStateManager.CanManageRequests(userDto))
                {
                    // Regular users shouldn't see team page - this should be handled by navigation logic
                    return;
                }

                // For managers, only show users from departments they can manage
                // For HR/Admin, show all users from the specified department
                var members = await _apiService.GetUsersByDepartmentAsync(currentUser.Department);

                TeamMembers.Clear();
                if (members != null)
                {
                    foreach(var member in members)
                    {
                         // Filter out the current user if needed
                         if (member.UserID != currentUser.UserID)
                         {
                            // For managers, validate they can manage this user's department
                            if (userDto.IsManager && !userDto.IsAdmin && !userDto.IsHR)
                            {
                                if (RequestStateManager.CanManageDepartment(userDto, member.Department))
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
             catch (Exception ex)
             {
                 // Handle exceptions appropriately (logging, user message)
                 System.Diagnostics.Debug.WriteLine($"Error loading team members: {ex.Message}"); // Basic logging
                 // Optionally, display an error message to the user
             }
             finally
             {
                 IsLoading = false;
             }
        }
    }
}