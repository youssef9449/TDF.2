using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using TDFShared.DTOs.Users;
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

        // Constructor injection
        public MyTeamViewModel(ApiService apiService)
        {
             _apiService = apiService;
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
                // Assuming App.CurrentUser holds the logged-in user's info
                if (App.CurrentUser == null)
                {
                    // Handle case where user is not logged in
                    // Maybe display a message or navigate away
                    return;
                }
                
                // Assuming ApiService has a method like GetTeamMembersAsync
                // This might need adjustment based on the actual ApiService implementation
                // and the correct API endpoint for fetching team members.
                var members = await _apiService.GetUsersByDepartmentAsync(App.CurrentUser.Department); // Example: fetch users by dept
                // Or: var members = await _apiService.GetTeamMembersAsync(App.CurrentUser.Id); // If specific team endpoint exists
                
                TeamMembers.Clear();
                if (members != null)
                {                  
                    foreach(var member in members)
                    {                       
                         // Filter out the current user if needed
                         if (member.UserID != App.CurrentUser.UserID)
                         {
                            TeamMembers.Add(member);
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