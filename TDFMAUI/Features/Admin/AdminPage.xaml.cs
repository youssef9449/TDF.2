using System;
using Microsoft.Maui.Controls; // Added for ContentPage, ActivityIndicator, Navigation, DisplayAlert, Shell
using Microsoft.Extensions.DependencyInjection; // Added for GetRequiredService
using TDFMAUI.Services;
using TDFShared.DTOs.Requests;
using TDFMAUI.Pages;
using TDFShared.DTOs.Users;
using TDFShared.Services;

namespace TDFMAUI.Features.Admin
{
    public partial class AdminPage : ContentPage
    {
        private readonly IRequestService _requestService;
        private readonly ApiService _apiService;
        private readonly IErrorHandlingService _errorHandlingService;
        private readonly IServiceProvider _serviceProvider; // Add IServiceProvider
        private List<UserDto> _users;
        private List<RequestResponseDto> _requests;

        public AdminPage(IRequestService requestService, ApiService apiService, IErrorHandlingService errorHandlingService, IServiceProvider serviceProvider) // Inject IServiceProvider
        {
            InitializeComponent();
            _requestService = requestService ?? throw new ArgumentNullException(nameof(requestService));
            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            _errorHandlingService = errorHandlingService ?? throw new ArgumentNullException(nameof(errorHandlingService));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider)); // Assign IServiceProvider
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
                // Load users data
                var usersResult = await _apiService.GetAllUsersAsync();
                _users = usersResult?.Items?.ToList() ?? new List<UserDto>();

                // Load requests data
                var pagination = new RequestPaginationDto { Page = 1, PageSize = 100 };
                var requestsResult = await _requestService.GetAllRequestsAsync(pagination);
                _requests = requestsResult?.Data?.Items?.ToList() ?? new List<RequestResponseDto>();

                // Data loaded successfully - UI updates would be handled by ViewModel in MVVM pattern
            }
            catch (Exception ex)
            {
                // Use shared error handling service for consistent error messages
                await _errorHandlingService.ShowErrorAsync(ex, "loading admin data");
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
            await Navigation.PushAsync(_serviceProvider.GetRequiredService<AddUserPage>());
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