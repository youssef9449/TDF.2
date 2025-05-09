using System;
using TDFMAUI.Services;
using TDFShared.DTOs.Requests;
using TDFMAUI.Pages;
using TDFShared.DTOs.Users; 

namespace TDFMAUI.Features.Admin 
{
    public partial class AdminPage : ContentPage
    {
        private readonly ApiService _apiService; 
        private List<UserDto> _users; 
        private List<RequestResponseDto> _requests; 

        public AdminPage(ApiService apiService) 
        {
            InitializeComponent();
            _apiService = apiService;
            LoadData();
        }

        private async void LoadData()
        {
            var loadingIndicator = this.FindByName<ActivityIndicator>("loadingIndicator");
            bool hasLoadingIndicator = loadingIndicator != null;
            
            if (hasLoadingIndicator)
            {
                loadingIndicator.IsVisible = true;
                loadingIndicator.IsRunning = true;
            }

            try
            {
                // TODO: Replace with calls to new UserApiService and RequestApiService
                var usersResult = await _apiService.GetAllUsersAsync(); 
                _users = usersResult?.Items?.ToList() ?? new List<UserDto>();
                
                var pagination = new RequestPaginationDto { Page = 1, PageSize = 100 }; 
                // TODO: Replace with call to new RequestApiService
                var requestsResult = await _apiService.GetRequestsAsync(pagination, null, null); 
                _requests = requestsResult?.Items?.ToList() ?? new List<RequestResponseDto>();

                // Remove UI update logic - should be handled by ViewModel or removed if sections are simplified
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ApiService.GetFriendlyErrorMessage(ex), "OK");
            }
            finally
            {
                if (hasLoadingIndicator)
                {
                    loadingIndicator.IsVisible = false;
                    loadingIndicator.IsRunning = false;
                }
            }
        }

        // Consider removing user/request selection if page simplifies to just navigation buttons
        /*
        private async void OnUserSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is UserDto selectedUser)
            {
                ((CollectionView)sender).SelectedItem = null;
                // Navigate to the user edit page within the Users feature
                // Assuming EditUserPage is moved and namespace updated
                await Navigation.PushAsync(new TDFMAUI.Features.Users.EditUserPage(selectedUser, _apiService)); 
            }
        }

        private async void OnRequestSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is RequestResponseDto selectedRequest)
            {
                ((CollectionView)sender).SelectedItem = null;
                // Navigate to the request details page within the Requests feature
                // Assuming RequestDetailsPage is moved and namespace updated
                await Navigation.PushAsync(new TDFMAUI.Features.Requests.RequestDetailsPage(_apiService, selectedRequest)); 
            }
        }
        */

        private async void OnAddUserClicked(object sender, EventArgs e)
        {
             // Navigate to AddUserPage within Users feature
             // Assuming AddUserPage is moved and namespace updated
            await Navigation.PushAsync(new AddUserPage());
        }
        
        private async void OnManageRequestsClicked(object sender, EventArgs e)
        {
            // Navigate to RequestApprovalPage within Requests feature
            // Assuming RequestApprovalPage is moved and namespace updated
            // Need to resolve RequestApprovalViewModel dependency later
            // await Navigation.PushAsync(new TDFMAUI.Features.Requests.RequestApprovalPage());
             await DisplayAlert("Not Implemented", "Navigation to Manage Requests not fully implemented yet.", "OK");
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            if (Shell.Current != null)
                await Shell.Current.GoToAsync("..");
            else
                await Navigation.PopAsync();
        }
    }
} 