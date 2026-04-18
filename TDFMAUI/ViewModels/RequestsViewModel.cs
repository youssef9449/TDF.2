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
using TDFShared.Contracts;

namespace TDFMAUI.ViewModels
{
    public partial class RequestsViewModel : BaseViewModel
    {
        private readonly IRequestService _requestService;
        private readonly IRequestApiService _requestApiService;
        private readonly IAuthClient _authService;
        private readonly ILogger<RequestsViewModel> _logger;
        private readonly IErrorHandlingService _errorHandlingService;
        private readonly ILookupService? _lookupService;

        [ObservableProperty]
        private ObservableCollection<RequestResponseDto> _requests = new();

        [ObservableProperty]
        private bool _showPendingOnly = true;

        partial void OnShowPendingOnlyChanged(bool value) => _ = LoadRequestsAsync();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanManageRequests))]
        private bool? _isAdmin;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanManageRequests))]
        private bool? _isManager;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanManageRequests))]
        private bool? _isHR;

        [ObservableProperty] private int _currentUserId;

        /// <summary>
        /// True when the current user can approve/reject/view other users' requests
        /// (admin, HR, or manager).
        /// </summary>
        public bool CanManageRequests => IsAdmin == true || IsHR == true || IsManager == true;

        [ObservableProperty]
        private ObservableCollection<LookupItem> _departments = new();
        [ObservableProperty]
        private LookupItem? _selectedDepartment;

        partial void OnSelectedDepartmentChanged(LookupItem? value) => _ = LoadRequestsAsync();

        public RequestsViewModel(
            IRequestService requestService,
            IRequestApiService requestApiService,
            IAuthClient authService,
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

                // Apply selected department filter if any
                if (SelectedDepartment != null && SelectedDepartment.Id != "0")
                {
                    pagination.Department = SelectedDepartment.Name;
                }

                var currentUser = await _authService.GetCurrentUserAsync();

                // Simplified: The backend now handles security scoping based on the user's role.
                // We just call GetRequestsAsync with any optional filters (like department).
                var response = await _requestApiService.GetRequestsAsync(pagination);

                if (response?.Success == true && response.Data?.Items != null)
                {
                    foreach (var request in response.Data.Items)
                    {
                        SetRequestPermissions(request, currentUser);
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

        private void SetRequestPermissions(RequestResponseDto request, UserDto? user)
        {
            if (user == null) return;
            bool isOwner = request.RequestUserID == user.UserID;
            request.CanEdit = RequestStateManager.CanEdit(request, user.IsAdmin ?? false, isOwner);
            request.CanDelete = request.CanEdit;
            request.CanApprove = RequestStateManager.CanApproveRequest(request, user);
            request.CanReject = RequestStateManager.CanRejectRequest(request, user);
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
                string? remarks = await Shell.Current.DisplayPromptAsync("Approve Request", "Remarks (optional):", "Approve", "Cancel");
                if (remarks == null) return; // user canceled

                var currentUser = await _authService.GetCurrentUserAsync();
                ApiResponse<RequestResponseDto>? response = null;
                if (currentUser?.IsManager == true)
                    response = await _requestService.ManagerApproveRequestAsync(request.RequestID, new ManagerApprovalDto { ManagerRemarks = remarks });
                else if (currentUser?.IsHR == true || currentUser?.IsAdmin == true)
                    response = await _requestService.HRApproveRequestAsync(request.RequestID, new HRApprovalDto { HRRemarks = remarks });

                if (response?.Success == true)
                {
                    await Shell.Current.DisplayAlert("Success", "Request approved.", "OK");
                    await LoadRequestsAsync();
                }
                else
                {
                    ErrorMessage = response?.Message ?? "Approval failed.";
                    await Shell.Current.DisplayAlert("Error", ErrorMessage, "OK");
                }
            }
            catch (Exception ex) { _logger.LogError(ex, "Approval error"); }
        }

        [RelayCommand]
        private async Task RejectRequestAsync(RequestResponseDto? request)
        {
            if (request == null) return;
            try
            {
                string? reason = await Shell.Current.DisplayPromptAsync("Reject Request", "Reason (required):", "Reject", "Cancel");
                if (string.IsNullOrWhiteSpace(reason)) return;

                var currentUser = await _authService.GetCurrentUserAsync();
                ApiResponse<RequestResponseDto>? response = null;
                if (currentUser?.IsManager == true)
                    response = await _requestService.ManagerRejectRequestAsync(request.RequestID, new ManagerRejectDto { ManagerRemarks = reason });
                else if (currentUser?.IsHR == true || currentUser?.IsAdmin == true)
                    response = await _requestService.HRRejectRequestAsync(request.RequestID, new HRRejectDto { HRRemarks = reason });

                if (response?.Success == true)
                {
                    await Shell.Current.DisplayAlert("Success", "Request rejected.", "OK");
                    await LoadRequestsAsync();
                }
                else
                {
                    ErrorMessage = response?.Message ?? "Rejection failed.";
                    await Shell.Current.DisplayAlert("Error", ErrorMessage, "OK");
                }
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

            // Filter departments based on user permissions
            if (currentUser != null && !(currentUser.IsAdmin == true || currentUser.IsHR == true))
            {
                departmentsList = departmentsList
                    .Where(d => RequestStateManager.CanAccessDepartment(currentUser.Department, d.Name))
                    .ToList();
            }

            // Only add "All" if user can see multiple departments
            if (departmentsList.Count > 1 || (currentUser?.IsAdmin == true || currentUser?.IsHR == true))
            {
                departmentsList.Insert(0, new LookupItem { Name = "All", Id = "0" });
            }

            Departments = new ObservableCollection<LookupItem>(departmentsList);
            if (Departments.Any()) SelectedDepartment = Departments[0];
        }
    }
}
