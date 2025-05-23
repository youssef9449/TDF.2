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

namespace TDFMAUI.ViewModels
{
    public partial class ReportsViewModel : ObservableObject
    {
        private readonly IRequestService _requestService;
        private readonly INavigation _navigation; // Assuming navigation is passed from the page
        private readonly IErrorHandlingService _errorHandlingService;

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

        public ReportsViewModel(IRequestService requestService, INavigation navigation, IErrorHandlingService errorHandlingService)
        {
            _requestService = requestService ?? throw new ArgumentNullException(nameof(requestService));
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            _errorHandlingService = errorHandlingService ?? throw new ArgumentNullException(nameof(errorHandlingService));
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
                // Determine userId based on role (pass null if admin/not needed by API for 'my' requests)
                int? userIdToFetch = App.CurrentUser?.IsAdmin == false ? App.CurrentUser.UserID : null;

                // Adapt filter values before sending to API if needed (e.g., "All" might mean null)
                string statusFilter = SelectedStatus == "All" ? null : SelectedStatus;

                // Create a RequestPaginationDto for filtering
                var pagination = new RequestPaginationDto
                {
                    FilterStatus = statusFilter == null ? (RequestStatus?)null : Enum.TryParse<RequestStatus>(statusFilter, true, out var parsedStatus) ? parsedStatus : (RequestStatus?)null,
                    FromDate = FromDate,
                    ToDate = ToDate,
                    Page = 1,
                    PageSize = 50
                };

                // Call GetRequestsAsync with the proper parameters
                // Ensure pagination.UserId is set correctly before this call
                var fetchedRequests = await _requestService.GetAllRequestsAsync(pagination);

                if (fetchedRequests?.Items != null)
                {
                    foreach (var req in fetchedRequests.Items)
                    {
                        Requests.Add(req);
                    }
                }
                RequestCountText = $"Total: {Requests.Count} requests";
            }
            catch (Exception ex)
            {
                // Use shared error handling service for consistent error messages
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
            // Use Shell navigation instead
            await Shell.Current.GoToAsync(nameof(AddRequestPage));
        }

        // Command to navigate to the Request Details page
        [RelayCommand]
        private async Task NavigateToDetailsAsync(RequestResponseDto request)
        {
            if (request == null) return;

            // Use Shell navigation instead
            await Shell.Current.GoToAsync($"{nameof(RequestDetailsPage)}", new Dictionary<string, object>
            {
                {"RequestId", request.RequestID}
            });
        }

        // This method might be called when the page appears
        public Task InitializeAsync()
        {
            // Set default dates if needed
            FromDate = DateTime.Today;
            ToDate = FromDate;
            return LoadRequestsAsync();
        }
    }
}