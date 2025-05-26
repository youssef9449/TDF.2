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
using TDFShared.DTOs.Users;
using TDFShared.Services;
using TDFMAUI.Services;
using TDFMAUI.Features.Requests; // For AddRequestPage navigation
using TDFShared.Exceptions;
using TDFShared.Enums;
using TDFShared.Utilities;

namespace TDFMAUI.ViewModels
{
    public partial class RequestsViewModel : ObservableObject
    {
        private readonly IRequestService _requestService;
        private readonly IAuthService _authService;
        private readonly ILogger<RequestsViewModel> _logger;
        private readonly IErrorHandlingService _errorHandlingService;

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

        // Computed Properties - Use TDFShared RequestStateManager for consistency
        public async Task<bool> CanManageRequestsAsync()
        {
            var user = await GetCurrentUserDtoAsync();
            return user != null && RequestStateManager.CanManageRequests(user);
        }

        public async Task<bool> CanApproveRejectAsync()
        {
            var user = await GetCurrentUserDtoAsync();
            return user != null && RequestStateManager.CanManageRequests(user);
        }

        public async Task<bool> CanEditDeleteAnyAsync()
        {
            var user = await GetCurrentUserDtoAsync();
            return user?.IsAdmin == true;
        }

        public async Task<bool> CanFilterByDepartmentAsync()
        {
            var user = await GetCurrentUserDtoAsync();
            return user?.IsManager == true;
        }

        public List<string> StatusOptions => RequestOptions.StatusOptions;
        public List<string> TypeOptions => RequestOptions.TypeOptions;
        public List<string> DepartmentOptions => RequestOptions.DepartmentOptions;

        public RequestsViewModel(
            IRequestService requestService,
            IAuthService authService,
            ILogger<RequestsViewModel> logger,
            IErrorHandlingService errorHandlingService)
        {
            _requestService = requestService;
            _authService = authService;
            _logger = logger;
            _errorHandlingService = errorHandlingService;
            _requests = new ObservableCollection<RequestResponseDto>();
            // Title will be set after loading roles
        }

        public async Task InitializeAsync()
        {
            await LoadUserRolesAsync();

            // Get and store the current user ID to avoid repeated async calls
            _currentUserId = await _authService.GetCurrentUserIdAsync();
            _logger.LogInformation("Current user ID loaded: {UserId}", _currentUserId);
            OnPropertyChanged(nameof(CurrentUserId));

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
                    OnPropertyChanged(nameof(IsCurrentUserAdmin));
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
                Title = "Team Requests"; // Manager can see own + department requests
            }
            else
            {
                Title = "My Requests";
            }
        }

        // Permission caches
        private readonly Dictionary<int, bool> _canEditDeleteCache = new();
        private readonly Dictionary<int, bool> _canApproveRejectCache = new();

        // Synchronous CanExecute methods for commands (read from cache)
        private bool CanEditDeleteRequest(RequestResponseDto? request)
        {
            if (request == null) return false;
            return _canEditDeleteCache.TryGetValue(request.RequestID, out var canEdit) && canEdit;
        }

        private bool CanApproveRejectRequest(RequestResponseDto? request)
        {
            if (request == null) return false;
            return _canApproveRejectCache.TryGetValue(request.RequestID, out var canApprove) && canApprove;
        }

        private async Task RefreshRequestPermissionsAsync()
        {
            var user = await GetCurrentUserDtoAsync();
            _canEditDeleteCache.Clear();
            _canApproveRejectCache.Clear();
            foreach (var req in Requests)
            {
                bool isOwner = req.RequestUserID == user?.UserID;
                // Use RequestStateManager for state-based checks
                _canEditDeleteCache[req.RequestID] = user != null && 
                    RequestStateManager.CanEdit(req, user.IsAdmin, isOwner);

                // Use AuthorizationUtilities for action-specific checks
                _canApproveRejectCache[req.RequestID] = user != null && 
                    AuthorizationUtilities.CanPerformRequestAction(user, req, TDFShared.Utilities.RequestAction.Approve);
            }
            NotifyCommandsCanExecuteChanged();
        }

        // After loading requests, refresh permissions
        [RelayCommand(CanExecute = nameof(IsNotBusy))]
        private async Task LoadRequestsAsync()
        {
            IsBusy = true;
            Requests.Clear();
            TDFMAUI.Services.DebugService.StartTimer("LoadRequests");
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
                var response = await _requestService.GetAllRequestsAsync(pagination);
                if (response?.Success == true && response.Data?.Items != null)
                {
                    foreach (var request in response.Data.Items)
                        Requests.Add(request);
                    _logger.LogInformation("Loaded {Count} requests.", Requests.Count);
                }
                else
                {
                    _logger.LogWarning("Received null or empty item list when loading requests.");
                }
                // Refresh permissions after loading requests
                await RefreshRequestPermissionsAsync();
            }
            catch (ApiException apiEx)
            {
                _logger.LogError(apiEx, "API Error loading requests: {ErrorMessage}", apiEx.Message);
                var friendlyMessage = _errorHandlingService.GetFriendlyErrorMessage(apiEx, "loading requests");
                await Shell.Current.DisplayAlert("API Error", friendlyMessage, "OK");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error loading requests.");
                var friendlyMessage = _errorHandlingService.GetFriendlyErrorMessage(ex, "loading requests");
                await Shell.Current.DisplayAlert("Error", friendlyMessage, "OK");
            }
            finally
            {
                TDFMAUI.Services.DebugService.StopTimer("LoadRequests");
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

        // Also refresh permissions after refresh
        [RelayCommand(CanExecute = nameof(IsNotBusy))]
        private async Task RefreshRequestsAsync()
        {
            _currentUserId = await _authService.GetCurrentUserIdAsync();
            _logger.LogInformation("Current user ID refreshed: {UserId}", _currentUserId);
            OnPropertyChanged(nameof(CurrentUserId));
            NotifyCommandsCanExecuteChanged();
            await LoadRequestsAsync();
            await RefreshRequestPermissionsAsync();
        }

        // --- Action Commands Implementation ---

        // Store the current user ID to avoid repeated async calls
        private int _currentUserId;

        // Properties for UI binding
        public int CurrentUserId => _currentUserId;
        public bool IsCurrentUserAdmin => IsAdmin == true;

        // Helper to check if current user can edit/delete a specific request
        private async Task<bool> CanEditRequestHelperAsync(RequestResponseDto request)
        {
            if (request == null) return false;

            var currentUser = await GetCurrentUserDtoAsync();
            if (currentUser == null) return false;

            bool isOwner = request.RequestUserID == _currentUserId;
            return RequestStateManager.CanEdit(request, currentUser.IsAdmin, isOwner);
        }

        /// <summary>
        /// Gets the current user as UserDto asynchronously
        /// </summary>
        private async Task<UserDto?> GetCurrentUserDtoAsync()
        {
            var currentUser = await _authService.GetCurrentUserAsync();
            return currentUser;
        }

        [RelayCommand(CanExecute = nameof(CanApproveRejectRequest))]
        private async Task ApproveRequestAsync(RequestResponseDto? request)
        {
            if (request == null) return;
            try
            {
                var response = await _requestService.ApproveRequestAsync(request.RequestID, "Approved via mobile app");
                if (response?.Success == true)
                {
                    await Shell.Current.DisplayAlert("Success", "Request approved successfully.", "OK");
                    await LoadRequestsAsync();
                    await RefreshRequestPermissionsAsync();
                }
                else
                {
                    await Shell.Current.DisplayAlert("Error", response?.Message ?? "Failed to approve request.", "OK");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving request {RequestId}", request.RequestID);
                await Shell.Current.DisplayAlert("Error", "Failed to approve request.", "OK");
            }
        }

        [RelayCommand(CanExecute = nameof(CanApproveRejectRequest))]
        private async Task RejectRequestAsync(RequestResponseDto? request)
        {
            if (request == null) return;
            try
            {
                var response = await _requestService.RejectRequestAsync(request.RequestID, "Rejected via mobile app");
                if (response?.Success == true)
                {
                    await Shell.Current.DisplayAlert("Success", "Request rejected successfully.", "OK");
                    await LoadRequestsAsync();
                    await RefreshRequestPermissionsAsync();
                }
                else
                {
                    await Shell.Current.DisplayAlert("Error", response?.Message ?? "Failed to reject request.", "OK");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting request {RequestId}", request.RequestID);
                await Shell.Current.DisplayAlert("Error", "Failed to reject request.", "OK");
            }
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
                var response = await _requestService.DeleteRequestAsync(request.RequestID);
                if (response?.Success == true)
                {
                    _logger.LogInformation("Successfully deleted request {RequestId}", request.RequestID);
                    Requests.Remove(request); // Remove from list locally
                    await Shell.Current.DisplayAlert("Success", "Request deleted.", "OK");
                    await RefreshRequestPermissionsAsync();
                }
                else
                {
                    _logger.LogWarning("Failed to delete request {RequestId} via API.", request.RequestID);
                    await Shell.Current.DisplayAlert("Error", response?.Message ?? "Failed to delete request.", "OK");
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