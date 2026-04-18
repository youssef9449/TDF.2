using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using TDFMAUI.Services;
using System;
using System.Linq;
using System.Collections.Generic;
using TDFShared.DTOs.Requests;
using TDFShared.DTOs.Common;
using TDFShared.Enums;
using TDFMAUI.Features.Requests;
using TDFShared.Services;
using TDFShared.Utilities;
using TDFShared.DTOs.Users;
using TDFShared.Contracts;

namespace TDFMAUI.ViewModels
{
    public partial class ReportsViewModel : BaseViewModel
    {
        private readonly IRequestService _requestService;
        private readonly IErrorHandlingService _errorHandlingService;
        private readonly IAuthClient _authService;

        [ObservableProperty]
        private ObservableCollection<RequestResponseDto> _requests = new();

        [ObservableProperty]
        private List<string> _statusOptions = new List<string> { "All" }.Concat(Enum.GetNames(typeof(RequestStatus))).ToList();

        [ObservableProperty]
        private string _selectedStatus = "All";

        [ObservableProperty]
        private DateTime? _fromDate = DateTime.Today;

        [ObservableProperty]
        private DateTime? _toDate = DateTime.Today;

        [ObservableProperty]
        private string _requestCountText = string.Empty;

        [ObservableProperty]
        private RequestResponseDto? _selectedRequest;

        [ObservableProperty] private bool _canViewReports;
        [ObservableProperty] private bool _canExportReports;

        public ReportsViewModel(
            IRequestService requestService, 
            IErrorHandlingService errorHandlingService,
            IAuthClient authService)
        {
            _requestService = requestService;
            _errorHandlingService = errorHandlingService;
            _authService = authService;
            Title = "Reports";
        }

        public async Task InitializeAsync()
        {
            var user = await _authService.GetCurrentUserAsync();
            if (user == null)
            {
                await Shell.Current.GoToAsync("//Login");
                return;
            }

            CanViewReports = RequestStateManager.CanManageRequests(user);
            CanExportReports = CanViewReports; // Export requires management permissions too

            if (!CanViewReports)
            {
                await Shell.Current.DisplayAlert("Access Denied", "No permission.", "OK");
                await Shell.Current.GoToAsync("//Home");
                return;
            }

            await LoadRequestsAsync();
        }

        [RelayCommand]
        public async Task LoadRequestsAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            Requests.Clear();

            try
            {
                var user = await _authService.GetCurrentUserAsync();
                if (user == null) return;

                var pagination = new RequestPaginationDto
                {
                    FilterStatus = SelectedStatus == "All" ? null : 
                        Enum.TryParse<RequestStatus>(SelectedStatus, true, out var parsedStatus) ? parsedStatus : null,
                    FromDate = FromDate,
                    ToDate = ToDate,
                    Page = 1,
                    PageSize = 50
                };

                // Apply security scope - if manager, force their department if no other department/user filter is set
                // but for Reports we usually want to show what the user is allowed to see.
                // The backend now handles this scoping.
                var result = await _requestService.GetAllRequestsAsync(pagination);

                if (result?.Data?.Items != null)
                {
                    foreach (var req in result.Data.Items)
                    {
                        // Double check visibility client-side for safety
                        if (RequestStateManager.CanViewRequest(req, user))
                        {
                            Requests.Add(req);
                        }
                    }
                }
                RequestCountText = $"Total: {Requests.Count} requests";
            }
            catch (Exception ex)
            {
                await _errorHandlingService.ShowErrorAsync(ex, "loading requests");
                RequestCountText = "Error loading requests";
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        private Task RefreshRequestsAsync() => LoadRequestsAsync();

        [RelayCommand]
        private async Task NavigateToAddRequestAsync()
        {
            await Shell.Current.GoToAsync(nameof(AddRequestPage));
        }

        [RelayCommand]
        private async Task NavigateToDetails(RequestResponseDto request)
        {
            if (request == null) return;
            var user = await _authService.GetCurrentUserAsync();
            if (user != null && RequestStateManager.CanViewRequest(request, user))
            {
                await Shell.Current.GoToAsync($"{nameof(RequestDetailsPage)}", new Dictionary<string, object> { {"RequestId", request.RequestID} });
            }
        }
    }
}
