using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using TDFShared.DTOs.Requests;
using TDFShared.DTOs.Common;
using TDFShared.Services;
using TDFMAUI.Services;
using TDFMAUI.Features.Requests; // For AddRequestPage navigation
using TDFShared.Exceptions;
using TDFShared.Enums;

namespace TDFMAUI.ViewModels
{
    public partial class RequestsViewModel : ObservableObject
    {
        private readonly IRequestService _requestService;
        private readonly IAuthService _authService;
        private readonly ILogger<RequestsViewModel> _logger;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotBusy))]
        private bool _isBusy;

        public bool IsNotBusy => !IsBusy;

        [ObservableProperty]
        private ObservableCollection<RequestResponseDto> _requests;

        [ObservableProperty]
        private string _title;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(LoadRequestsCommand))]
        [NotifyCanExecuteChangedFor(nameof(RefreshRequestsCommand))]
        private bool _showPendingOnly = true; // Default to showing pending

        // Role properties with nullable bool type
        [ObservableProperty]
        private bool? _isAdmin;
        [ObservableProperty]
        private bool? _isManager;
        [ObservableProperty]
        private bool? _isHR;

        // Computed Properties
        public bool CanManageRequests => RequestAuthorizationService.CanManageRequests(IsAdmin, IsManager, IsHR);
        public bool CanApproveReject => RequestAuthorizationService.CanManageRequests(IsAdmin, IsManager, IsHR);
        public bool CanEditDeleteAny => RequestAuthorizationService.CanEditDeleteAny(IsAdmin);
        public bool CanFilterByDepartment => RequestAuthorizationService.CanFilterByDepartment(IsManager);

        public List<string> StatusOptions => RequestOptions.StatusOptions;
        public List<string> TypeOptions => RequestOptions.TypeOptions;
        public List<string> DepartmentOptions => RequestOptions.DepartmentOptions;

        public RequestsViewModel(IRequestService requestService, IAuthService authService, ILogger<RequestsViewModel> logger)
        {
            _requestService = requestService;
            _authService = authService;
            _logger = logger;
            _requests = new ObservableCollection<RequestResponseDto>();
            // Title will be set after loading roles
        }

        public async Task InitializeAsync()
        {
            await LoadUserRolesAsync();

            // Get and store the current user ID to avoid repeated async calls
            _currentUserId = await _authService.GetCurrentUserIdAsync();
            _logger.LogInformation("Current user ID loaded: {UserId}", _currentUserId);

            // Notify commands that their CanExecute status might have changed
            NotifyCommandsCanExecuteChanged();

            await LoadRequestsAsync(); // Load requests after roles are known
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
                    _logger.LogInformation("User roles loaded: Admin={IsAdmin}, Manager={IsManager}, HR={IsHR}", IsAdmin, IsManager, IsHR);
                    SetTitleBasedOnRole();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load user roles.");
                IsAdmin = IsManager = IsHR = null;
                Title = "My Requests";
            }
        }

        private void SetTitleBasedOnRole()
        {
            if (IsAdmin == true || IsHR == true)
            {
                Title = "All Requests";
            }
            else if (IsManager == true)
            {
                Title = "Team Requests"; // Or manage own + team?
            }
            else
            {
                Title = "My Requests";
            }
        }

        [RelayCommand(CanExecute = nameof(IsNotBusy))]
        private async Task LoadRequestsAsync()
        {
            IsBusy = true;
            Requests.Clear();
            try
            {
                if (_currentUserId == 0 && !(IsAdmin == true || IsHR == true || IsManager == true))
                {
                    _logger.LogWarning("Cannot load requests: User not authenticated.");
                    await Shell.Current.DisplayAlert("Auth Error", "Could not verify user.", "OK");
                    IsBusy = false;
                    return;
                }

                var pagination = new RequestPaginationDto
                {
                    Page = 1,
                    PageSize = 50,
                    FilterStatus = ShowPendingOnly ? RequestStatus.Pending : RequestStatus.All
                };

                PaginatedResult<RequestResponseDto>? result = null;

                if (IsAdmin == true || IsHR == true)
                {
                    _logger.LogInformation("Loading all requests (Admin/HR view) with filter: {Filter}", pagination.FilterStatus);
                    result = await _requestService.GetAllRequestsAsync(pagination);
                }
                else if (IsManager == true)
                {
                    string? department = await _authService.GetCurrentUserDepartmentAsync();
                    if (!string.IsNullOrEmpty(department))
                    {
                        _logger.LogInformation("Loading requests for department '{Department}' (Manager view) with filter: {Filter}", department, pagination.FilterStatus);
                        pagination.Department = department;
                        result = await _requestService.GetRequestsByDepartmentAsync(department, pagination);
                    }
                    else
                    {
                        _logger.LogWarning("Manager detected, but department claim not found in token. Loading own requests instead.");
                        result = await _requestService.GetMyRequestsAsync(pagination);
                    }
                }
                else
                {
                    _logger.LogInformation("Loading requests for user {UserId} with filter: {Filter}", _currentUserId, pagination.FilterStatus);
                    result = await _requestService.GetMyRequestsAsync(pagination);
                }

                if (result?.Items != null)
                {
                    foreach (var request in result.Items)
                    {
                        Requests.Add(request);
                    }
                    _logger.LogInformation("Loaded {Count} requests.", Requests.Count);
                }
                else
                {
                    _logger.LogWarning("Received null or empty item list when loading requests.");
                }
            }
            catch (ApiException apiEx)
            {
                _logger.LogError(apiEx, "API Error loading requests: {ErrorMessage}", apiEx.Message);
                await Shell.Current.DisplayAlert("API Error", $"Failed to load requests: {apiEx.Message}", "OK");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error loading requests.");
                await Shell.Current.DisplayAlert("Error", "Failed to load requests.", "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task GoToAddRequestAsync()
        {
            await Shell.Current.GoToAsync(nameof(AddRequestPage));
        }

        [RelayCommand]
        private async Task GoToRequestDetailsAsync(RequestResponseDto? request)
        {
            if (request == null) return;
            _logger.LogInformation("Navigating to details for request {RequestId}", request.RequestID);
            await Shell.Current.GoToAsync($"{nameof(RequestDetailsPage)}", new Dictionary<string, object>
            { {"RequestId", request.RequestID} });
        }

        // This command can be used for pull-to-refresh
        [RelayCommand(CanExecute = nameof(IsNotBusy))]
        private async Task RefreshRequestsAsync()
        {
            // Optional: Reload roles if they might change dynamically during app session
            // await LoadUserRolesAsync();

            // Refresh the current user ID
            _currentUserId = await _authService.GetCurrentUserIdAsync();
            _logger.LogInformation("Current user ID refreshed: {UserId}", _currentUserId);

            // Notify commands that their CanExecute status might have changed
            NotifyCommandsCanExecuteChanged();

            await LoadRequestsAsync();
        }

        // --- Action Commands Implementation ---

        // Store the current user ID to avoid repeated async calls
        private int _currentUserId;

        // Helper to check if current user can edit/delete a specific request
        private bool CanEditDeleteRequest(RequestResponseDto? request)
        {
            if (request == null) return false;
            return RequestAuthorizationService.CanEditRequest(request, _currentUserId, IsAdmin);
        }

        // Helper to check if current user can approve/reject a specific request
        private bool CanApproveRejectRequest(RequestResponseDto? request)
        {
            if (request == null) return false;
            if (request.RequestUserID == _currentUserId) return false; // Can't approve/reject own requests
            if (request.Status != RequestStatus.Pending) return false;

            var canManage = RequestAuthorizationService.CanManageRequests(IsAdmin, IsManager, IsHR);
            if (!canManage) return false;

            // If manager (not admin/HR), check department match
            if (IsManager == true && IsAdmin != true && IsHR != true)
            {
                var currentUserDepartment = Task.Run(async () => await _authService.GetCurrentUserDepartmentAsync()).Result;
                return request.RequestDepartment == currentUserDepartment;
            }

            return true;
        }

        [RelayCommand(CanExecute = nameof(CanApproveRejectRequest))]
        private async Task ApproveRequestAsync(RequestResponseDto? request)
        {
            if (request == null) return;

            bool confirmed = await Shell.Current.DisplayAlert("Confirm Approval", $"Approve request from {request.UserName} for {request.LeaveType} starting {request.RequestStartDate:d}?", "Approve", "Cancel");
            if (!confirmed) return;

            IsBusy = true;
            try
            {
                _logger.LogInformation("Attempting to approve request {RequestId}", request.RequestID);
                var approvalDto = new RequestApprovalDto { Status = RequestStatus.Approved, Comment = "Approved via Mobile App" };
                bool success = await _requestService.ApproveRequestAsync(request.RequestID, approvalDto);
                if (success)
                {
                    _logger.LogInformation("Successfully approved request {RequestId}", request.RequestID);
                    request.Status = RequestStatus.Approved;
                    OnPropertyChanged(nameof(Requests));
                    await Shell.Current.DisplayAlert("Success", "Request approved.", "OK");
                }
                else
                {
                    _logger.LogWarning("Failed to approve request {RequestId} via API.", request.RequestID);
                    await Shell.Current.DisplayAlert("Error", "Failed to approve request.", "OK");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving request {RequestId}", request.RequestID);
                await Shell.Current.DisplayAlert("Error", "An error occurred while approving.", "OK");
            }
            finally { IsBusy = false; }
        }

        [RelayCommand(CanExecute = nameof(CanApproveRejectRequest))]
        private async Task RejectRequestAsync(RequestResponseDto? request)
        {
            if (request == null) return;

            string reason = await Shell.Current.DisplayPromptAsync("Reject Request", $"Reason for rejecting {request.UserName}'s request:", "OK", "Cancel", "Reason", maxLength: 100, keyboard: Keyboard.Text);

            if (string.IsNullOrWhiteSpace(reason)) // User cancelled or entered empty reason
            {
                if (reason == null) _logger.LogInformation("Reject cancelled by user.");
                else _logger.LogWarning("Rejection reason cannot be empty.");
                // Optionally show error if reason was empty but not cancelled
                if (reason != null) await Shell.Current.DisplayAlert("Invalid Reason", "Rejection reason cannot be empty.", "OK");
                return;
            }

            IsBusy = true;
            try
            {
                _logger.LogInformation("Attempting to reject request {RequestId}", request.RequestID);
                var rejectDto = new RequestRejectDto { RejectReason = reason };
                bool success = await _requestService.RejectRequestAsync(request.RequestID, rejectDto);
                if (success)
                {
                    _logger.LogInformation("Successfully rejected request {RequestId}", request.RequestID);
                    request.Status = RequestStatus.Rejected;
                    request.Remarks = reason; // Update remarks locally?
                    OnPropertyChanged(nameof(Requests));
                    await Shell.Current.DisplayAlert("Success", "Request rejected.", "OK");
                }
                else
                {
                    _logger.LogWarning("Failed to reject request {RequestId} via API.", request.RequestID);
                    await Shell.Current.DisplayAlert("Error", "Failed to reject request.", "OK");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting request {RequestId}", request.RequestID);
                await Shell.Current.DisplayAlert("Error", "An error occurred while rejecting.", "OK");
            }
            finally { IsBusy = false; }
        }

        [RelayCommand(CanExecute = nameof(CanEditDeleteRequest))]
        private async Task EditRequestAsync(RequestResponseDto? request)
        {
            if (request == null) return;
            _logger.LogInformation("Navigating to edit request {RequestId}", request.RequestID);
            await Shell.Current.GoToAsync($"{nameof(AddRequestPage)}", new Dictionary<string, object>
            { {"ExistingRequest", request} });
        }

        [RelayCommand(CanExecute = nameof(CanEditDeleteRequest))]
        private async Task DeleteRequestAsync(RequestResponseDto? request)
        {
            if (request == null) return;

            bool confirmed = await Shell.Current.DisplayAlert("Confirm Delete", $"Are you sure you want to delete the request for {request.LeaveType} starting {request.RequestStartDate:d}?", "Delete", "Cancel");
            if (!confirmed) return;

            IsBusy = true;
            try
            {
                _logger.LogInformation("Attempting to delete request {RequestId}", request.RequestID);
                bool success = await _requestService.DeleteRequestAsync(request.RequestID);
                if (success)
                {
                    _logger.LogInformation("Successfully deleted request {RequestId}", request.RequestID);
                    Requests.Remove(request); // Remove from list locally
                    await Shell.Current.DisplayAlert("Success", "Request deleted.", "OK");
                }
                else
                {
                    _logger.LogWarning("Failed to delete request {RequestId} via API.", request.RequestID);
                    await Shell.Current.DisplayAlert("Error", "Failed to delete request.", "OK");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting request {RequestId}", request.RequestID);
                await Shell.Current.DisplayAlert("Error", "An error occurred while deleting.", "OK");
            }
            finally { IsBusy = false; }
        }

        partial void OnShowPendingOnlyChanged(bool value)
        {
            _logger.LogInformation("Filter changed: ShowPendingOnly = {Value}. Reloading requests.", value);
            _ = LoadRequestsAsync(); // Trigger background reload
        }

        /// <summary>
        /// Notifies all commands that their CanExecute status might have changed
        /// </summary>
        private void NotifyCommandsCanExecuteChanged()
        {
            ApproveRequestCommand.NotifyCanExecuteChanged();
            RejectRequestCommand.NotifyCanExecuteChanged();
            EditRequestCommand.NotifyCanExecuteChanged();
            DeleteRequestCommand.NotifyCanExecuteChanged();
        }
    }
}