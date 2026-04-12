using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using TDFShared.DTOs.Requests;
using TDFShared.DTOs.Common;
using TDFShared.DTOs.Users;
using TDFShared.Services;
using TDFMAUI.Services;
using TDFMAUI.Features.Requests;
using TDFShared.Enums;
using TDFShared.Utilities;
using TDFMAUI.Helpers;

namespace TDFMAUI.ViewModels
{
    public partial class RequestsViewModel : BaseViewModel
    {
        private readonly IRequestService _requestService;
        private readonly IRequestApiService _requestApiService;
        private readonly IAuthService _authService;
        private readonly ILogger<RequestsViewModel> _logger;
        private readonly IErrorHandlingService _errorHandlingService;
        private readonly ILookupService? _lookupService;

        [ObservableProperty]
        private ObservableCollection<RequestResponseDto> _requests = new();

        [ObservableProperty]
        private bool _showPendingOnly = true;

        partial void OnShowPendingOnlyChanged(bool value) => _ = LoadRequestsAsync();

        [ObservableProperty] private bool? _isAdmin;
        [ObservableProperty] private bool? _isManager;
        [ObservableProperty] private bool? _isHR;
        [ObservableProperty] private int _currentUserId;

        [ObservableProperty]
        private ObservableCollection<LookupItem> _departments = new();
        [ObservableProperty]
        private LookupItem? _selectedDepartment;

        partial void OnSelectedDepartmentChanged(LookupItem? value) => _ = LoadRequestsAsync();

        public RequestsViewModel(
            IRequestService requestService,
            IRequestApiService requestApiService,
            IAuthService authService,
            ILogger<RequestsViewModel> logger,
            IErrorHandlingService errorHandlingService,
            ILookupService lookupService)
        {
            _requestService = requestService;
            _requestApiService = requestApiService;
            _authService = authService;
            _logger = logger;
            _errorHandlingService = errorHandlingService;
            _lookupService = lookupService;
        }

        public async Task InitializeAsync()
        {
            await LoadUserRolesAsync();
            CurrentUserId = await _authService.GetCurrentUserIdAsync();
            await LoadDepartmentsAsync();
            await LoadRequestsAsync();
        }

        private async Task LoadUserRolesAsync()
        {
            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser != null)
                {
                    IsAdmin = currentUser.IsAdmin;
                    IsManager = currentUser.IsManager;
                    IsHR = currentUser.IsHR;
                    SetTitleBasedOnRole();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load roles.");
                Title = "My Requests";
            }
        }

        private void SetTitleBasedOnRole()
        {
            if (IsAdmin == true || IsHR == true) Title = "All Requests";
            else if (IsManager == true) Title = "Team Requests";
            else Title = "My Requests";
        }

        [RelayCommand(CanExecute = nameof(IsNotBusy))]
        private async Task LoadRequestsAsync()
        {
            IsBusy = true;
            Requests.Clear();
            try
            {
                var pagination = new RequestPaginationDto
                {
                    Page = 1,
                    PageSize = 50,
                    FilterStatus = ShowPendingOnly ? RequestStatus.Pending : null
                };

                var currentUser = await _authService.GetCurrentUserAsync();
                var accessLevel = AuthorizationUtilities.GetRequestAccessLevel(currentUser);
                ApiResponse<PaginatedResult<RequestResponseDto>>? response = null;

                switch (accessLevel)
                {
                    case RequestAccessLevel.All:
                        response = await _requestApiService.GetRequestsAsync(pagination);
                        break;
                    case RequestAccessLevel.Department:
                        string? department = (SelectedDepartment != null && SelectedDepartment.Id != "0") ? SelectedDepartment.Name : currentUser?.Department;
                        if (!string.IsNullOrEmpty(department)) response = await _requestApiService.GetRequestsAsync(pagination, department: department);
                        else response = await _requestApiService.GetRequestsAsync(pagination, userId: currentUser?.UserID);
                        break;
                    default:
                        response = await _requestApiService.GetRequestsAsync(pagination, userId: currentUser?.UserID);
                        break;
                }

                if (response?.Success == true && response.Data?.Items != null)
                {
                    foreach (var request in response.Data.Items)
                    {
                        await SetRequestPermissions(request, currentUser);
                        Requests.Add(request);
                    }
                }
                else ErrorMessage = response?.Message ?? "Failed to load requests.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading requests.");
                ErrorMessage = _errorHandlingService.GetFriendlyErrorMessage(ex, "loading requests");
            }
            finally { IsBusy = false; }
        }

        private async Task SetRequestPermissions(RequestResponseDto request, UserDto? user)
        {
            if (user == null) return;
            bool isOwner = request.RequestUserID == user.UserID;
            request.CanEdit = RequestStateManager.CanEdit(request, user.IsAdmin ?? false, isOwner);
            request.CanDelete = request.CanEdit;
            request.CanApprove = AuthorizationUtilities.CanPerformRequestAction(user, request, RequestAction.Approve);
            request.CanReject = AuthorizationUtilities.CanPerformRequestAction(user, request, RequestAction.Reject);
        }

        [RelayCommand]
        private async Task GoToAddRequestAsync() => await Shell.Current.GoToAsync(nameof(AddRequestPage));

        [RelayCommand]
        private async Task GoToRequestDetailsAsync(RequestResponseDto? request)
        {
            if (request == null) return;
            await Shell.Current.GoToAsync($"{nameof(RequestDetailsPage)}", new Dictionary<string, object> { {"RequestId", request.RequestID} });
        }

        [RelayCommand(CanExecute = nameof(IsNotBusy))]
        private async Task RefreshRequestsAsync() => await LoadRequestsAsync();

        [RelayCommand]
        private async Task ApproveRequestAsync(RequestResponseDto? request)
        {
            if (request == null) return;
            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                ApiResponse<RequestResponseDto>? response = null;
                if (currentUser?.IsManager == true) response = await _requestService.ManagerApproveRequestAsync(request.RequestID, new ManagerApprovalDto { ManagerRemarks = "Approved" });
                else if (currentUser?.IsHR == true) response = await _requestService.HRApproveRequestAsync(request.RequestID, new HRApprovalDto { HRRemarks = "Approved" });

                if (response?.Success == true)
                {
                    await Shell.Current.DisplayAlert("Success", "Approved", "OK");
                    await LoadRequestsAsync();
                }
                else ErrorMessage = response?.Message ?? "Approval failed.";
            }
            catch (Exception ex) { _logger.LogError(ex, "Approval error"); }
        }

        [RelayCommand]
        private async Task RejectRequestAsync(RequestResponseDto? request)
        {
            if (request == null) return;
            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                ApiResponse<RequestResponseDto>? response = null;
                if (currentUser?.IsManager == true) response = await _requestService.ManagerRejectRequestAsync(request.RequestID, new ManagerRejectDto { ManagerRemarks = "Rejected" });
                else if (currentUser?.IsHR == true) response = await _requestService.HRRejectRequestAsync(request.RequestID, new HRRejectDto { HRRemarks = "Rejected" });

                if (response?.Success == true)
                {
                    await Shell.Current.DisplayAlert("Success", "Rejected", "OK");
                    await LoadRequestsAsync();
                }
                else ErrorMessage = response?.Message ?? "Rejection failed.";
            }
            catch (Exception ex) { _logger.LogError(ex, "Rejection error"); }
        }

        [RelayCommand]
        private async Task EditRequestAsync(RequestResponseDto? request)
        {
            if (request == null) return;
            await Shell.Current.GoToAsync($"{nameof(AddRequestPage)}", new Dictionary<string, object> { {"ExistingRequest", request} });
        }

        [RelayCommand]
        private async Task DeleteRequestAsync(RequestResponseDto? request)
        {
            if (request == null) return;
            bool confirmed = await Shell.Current.DisplayAlert("Confirm Delete", "Are you sure?", "Delete", "Cancel");
            if (!confirmed) return;
            IsBusy = true;
            try
            {
                var response = await _requestService.DeleteRequestAsync(request.RequestID);
                if (response?.Success == true)
                {
                    Requests.Remove(request);
                    await Shell.Current.DisplayAlert("Success", "Deleted", "OK");
                }
                else ErrorMessage = response?.Message ?? "Delete failed.";
            }
            catch (Exception ex) { _logger.LogError(ex, "Delete error"); }
            finally { IsBusy = false; }
        }

        private async Task LoadDepartmentsAsync()
        {
            if (_lookupService == null) return;
            var allDepartments = await _lookupService.GetDepartmentsAsync();
            var currentUser = await _authService.GetCurrentUserAsync();

            var departmentsList = allDepartments.ToList();
            departmentsList.Insert(0, new LookupItem { Name = "All", Id = "0" });
            Departments = new ObservableCollection<LookupItem>(departmentsList);
            SelectedDepartment = Departments[0];
        }
    }
}
