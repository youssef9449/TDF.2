using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using TDFMAUI.Services;
using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Maui.Controls; // For INavigation or Shell navigation
using TDFShared.DTOs.Requests; // Added
using TDFShared.DTOs.Common; // Added for pagination
using TDFShared.Enums;
using TDFMAUI.Features.Requests;
using TDFShared.Services;
using TDFShared.Utilities;
using TDFShared.DTOs.Users;

namespace TDFMAUI.ViewModels
{
    public partial class ReportsViewModel : ObservableObject
    {
        private readonly IRequestService _requestService;
        private readonly INavigation _navigation; // Assuming navigation is passed from the page
        private readonly IErrorHandlingService _errorHandlingService;
        private readonly IAuthService _authService;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private ObservableCollection<RequestResponseDto> _requests = new();

        [ObservableProperty]
        private List<string> _statusOptions = new List<string> { "All" }.Concat(Enum.GetNames(typeof(RequestStatus))).ToList();

        [ObservableProperty]
        private string _selectedStatus = "All"; // Default filter

        [ObservableProperty]
        private DateTime? _fromDate;

        [ObservableProperty]
        private DateTime? _toDate;

        [ObservableProperty]
        private string _requestCountText;

        [ObservableProperty]
        private RequestResponseDto _selectedRequest;

        // Authorization properties
        [ObservableProperty]
        private bool _canViewReports;

        [ObservableProperty]
        private bool _canExportReports;

        public ReportsViewModel(
            IRequestService requestService, 
            INavigation navigation, 
            IErrorHandlingService errorHandlingService,
            IAuthService authService)
        {
            _requestService = requestService ?? throw new ArgumentNullException(nameof(requestService));
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            _errorHandlingService = errorHandlingService ?? throw new ArgumentNullException(nameof(errorHandlingService));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        }

        public async Task InitializeAsync()
        {
            // Set default dates
            FromDate = DateTime.Today;
            ToDate = FromDate;

            // Check authorization before loading data
            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser == null)
            {
                await Shell.Current.DisplayAlert("Access Denied", "You must be logged in to view reports.", "OK");
                await Shell.Current.GoToAsync("//Login");
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

            // Use RequestStateManager for state-based checks
            CanViewReports = RequestStateManager.CanManageRequests(userDto);
            
            // Use AuthorizationUtilities for action-specific checks
            CanExportReports = AuthorizationUtilities.CanPerformRequestAction(userDto, null, RequestAction.View);

            if (!CanViewReports)
            {
                await Shell.Current.DisplayAlert("Access Denied", "You don't have permission to view reports.", "OK");
                await Shell.Current.GoToAsync("//Home");
                return;
            }

            await LoadRequestsAsync();
        }

        // Command to load/refresh data based on filters
        [RelayCommand]
        private async Task LoadRequestsAsync()
        {
            if (IsLoading) return;

            IsLoading = true;
            Requests.Clear();

            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null) return;

                var userDto = new UserDto
                {
                    UserID = currentUser.UserID,
                    IsAdmin = currentUser.IsAdmin,
                    IsHR = currentUser.IsHR,
                    IsManager = currentUser.IsManager,
                    Department = currentUser.Department
                };

                // Create pagination DTO
                var pagination = new RequestPaginationDto
                {
                    FilterStatus = SelectedStatus == "All" ? null : 
                        Enum.TryParse<RequestStatus>(SelectedStatus, true, out var parsedStatus) ? parsedStatus : null,
                    FromDate = FromDate,
                    ToDate = ToDate,
                    Page = 1,
                    PageSize = 50
                };

                var result = await _requestService.GetAllRequestsAsync(pagination);

                if (result?.Data?.Items != null)
                {
                    // Filter requests based on user permissions using RequestStateManager
                    var filteredRequests = result.Data.Items.Where(request =>
                        RequestStateManager.CanViewRequest(request, userDto)).ToList();

                    foreach (var req in filteredRequests)
                    {
                        Requests.Add(req);
                    }
                }
                RequestCountText = $"Total: {Requests.Count} requests";
            }
            catch (Exception ex)
            {
                await _errorHandlingService.ShowErrorAsync(ex, "loading requests");
                RequestCountText = "Error loading requests";
            }
            finally
            {
                IsLoading = false;
            }
        }

        // Command triggered by pull-to-refresh
        [RelayCommand]
        private Task RefreshRequestsAsync()
        {
            // Simply call LoadRequestsAsync again
            return LoadRequestsAsync();
        }

        // Command to navigate to the Add Request page
        [RelayCommand]
        private async Task NavigateToAddRequestAsync()
        {
            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser == null) return;

            var userDto = new UserDto
            {
                UserID = currentUser.UserID,
                IsAdmin = currentUser.IsAdmin,
                IsHR = currentUser.IsHR,
                IsManager = currentUser.IsManager,
                Department = currentUser.Department
            };

            // Check if user can create requests using AuthorizationUtilities
            if (!AuthorizationUtilities.CanPerformRequestAction(userDto, null, RequestAction.View))
            {
                await Shell.Current.DisplayAlert("Access Denied", "You don't have permission to create requests.", "OK");
                return;
            }

            await Shell.Current.GoToAsync(nameof(AddRequestPage));
        }

        // Command to navigate to the Request Details page
        [RelayCommand]
        private async Task NavigateToDetailsAsync(RequestResponseDto request)
        {
            if (request == null) return;

            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser == null) return;

            var userDto = new UserDto
            {
                UserID = currentUser.UserID,
                IsAdmin = currentUser.IsAdmin,
                IsHR = currentUser.IsHR,
                IsManager = currentUser.IsManager,
                Department = currentUser.Department
            };

            // Check if user can view this request using RequestStateManager
            if (!RequestStateManager.CanViewRequest(request, userDto))
            {
                await Shell.Current.DisplayAlert("Access Denied", "You don't have permission to view this request.", "OK");
                return;
            }

            await Shell.Current.GoToAsync($"{nameof(RequestDetailsPage)}", new Dictionary<string, object>
            {
                {"RequestId", request.RequestID}
            });
        }
    }
}