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
using TDFMAUI.Helpers;

namespace TDFMAUI.ViewModels
{
    public partial class RequestsViewModel : ObservableObject
    {
        private readonly IRequestService _requestService;
        private readonly IAuthService _authService;
        private readonly ILogger<RequestsViewModel> _logger;
        private readonly IErrorHandlingService _errorHandlingService;
        private readonly ILookupService? _lookupService;

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

        // Remove broken DepartmentOptions property and replace with dynamic department loading
        // public List<string> DepartmentOptions => RequestOptions.DepartmentOptions;
        [ObservableProperty]
        private ObservableCollection<LookupItem> _departments = new();
        [ObservableProperty]
        private LookupItem? _selectedDepartment;

        public RequestsViewModel(
            IRequestService requestService,
            IAuthService authService,
            ILogger<RequestsViewModel> logger,
            IErrorHandlingService errorHandlingService,
            ILookupService lookupService)
        {
            _requestService = requestService;
            _authService = authService;
            _logger = logger;
            _errorHandlingService = errorHandlingService;
            _lookupService = lookupService;
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
            await LoadDepartmentsAsync(); // Load departments
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
                var canEdit = user != null && RequestStateManager.CanEdit(req, user.IsAdmin ?? false, isOwner);
                var canDelete = canEdit; // Same logic for delete
                
                // Use AuthorizationUtilities for action-specific checks
                var canApprove = user != null && AuthorizationUtilities.CanPerformRequestAction(user, req, TDFShared.Utilities.RequestAction.Approve);
                var canReject = user != null && AuthorizationUtilities.CanPerformRequestAction(user, req, TDFShared.Utilities.RequestAction.Reject);
                
                // Cache the permissions
                _canEditDeleteCache[req.RequestID] = canEdit;
                _canApproveRejectCache[req.RequestID] = canApprove;
                
                // Set the properties on the DTO for UI binding
                req.CanEdit = canEdit;
                req.CanDelete = canDelete;
                req.CanApprove = canApprove;
                req.CanReject = canReject;
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
                _logger.LogInformation("=== Starting LoadRequestsAsync ===");
                
                // Enhanced authentication handling for all platforms
                _logger.LogInformation("Platform: {Platform}, checking authentication...", DeviceHelper.IsDesktop ? "Desktop" : "Mobile");
                
                // Always ensure we have a valid token set in the HTTP client
                var token = await _authService.GetCurrentTokenAsync();
                if (!string.IsNullOrEmpty(token))
                {
                    _logger.LogInformation("Setting authentication token (length: {Length})", token.Length);
                    await _authService.SetAuthenticationTokenAsync(token);
                    
                    // For desktop, also ensure ApiConfig has the token
                    if (DeviceHelper.IsDesktop)
                    {
                        TDFMAUI.Config.ApiConfig.CurrentToken = token;
                    }
                }
                else
                {
                    _logger.LogError("No authentication token available - user may need to log in again");
                    await Shell.Current.DisplayAlert("Authentication Error", 
                        "No valid authentication token found. Please log in again.", "OK");
                    IsBusy = false;
                    return;
                }

                // Get current user ID and validate authentication
                _currentUserId = await _authService.GetCurrentUserIdAsync();
                _logger.LogInformation("Current user ID: {UserId}", _currentUserId);
                
                if (_currentUserId == 0)
                {
                    _logger.LogWarning("User ID is 0, checking if user has elevated roles...");
                    var currentUser = await _authService.GetCurrentUserAsync();
                    if (currentUser == null)
                    {
                        _logger.LogError("Cannot load requests: User not authenticated and no user data available.");
                        await Shell.Current.DisplayAlert("Auth Error", "Could not verify user. Please log in again.", "OK");
                        IsBusy = false;
                        return;
                    }
                    _logger.LogInformation("User found: {UserName}, Admin: {IsAdmin}, HR: {IsHR}, Manager: {IsManager}", 
                        currentUser.FullName, currentUser.IsAdmin, currentUser.IsHR, currentUser.IsManager);
                }
                var pagination = new RequestPaginationDto
                {
                    Page = 1,
                    PageSize = 50,
                    FilterStatus = ShowPendingOnly ? RequestStatus.Pending : null
                };

                _logger.LogInformation("Pagination settings: Page={Page}, PageSize={PageSize}, FilterStatus={FilterStatus}", 
                    pagination.Page, pagination.PageSize, pagination.FilterStatus);

                ApiResponse<PaginatedResult<RequestResponseDto>>? response = null;
                var currentUserDto = await GetCurrentUserDtoAsync();
                var accessLevel = AuthorizationUtilities.GetRequestAccessLevel(currentUserDto);
                
                _logger.LogInformation("User access level: {AccessLevel}", accessLevel);
                _logger.LogInformation("Current user: {UserName}, Admin: {IsAdmin}, HR: {IsHR}, Manager: {IsManager}, Department: {Department}", 
                    currentUserDto?.FullName, currentUserDto?.IsAdmin, currentUserDto?.IsHR, currentUserDto?.IsManager, currentUserDto?.Department);

                switch (accessLevel)
                {
                    case RequestAccessLevel.All:
                        _logger.LogInformation("Loading all requests for Admin/HR user");
                        response = await _requestService.GetAllRequestsAsync(pagination);
                        break;
                    case RequestAccessLevel.Department:
                        if (SelectedDepartment != null && SelectedDepartment.Id != "0")
                        {
                            _logger.LogInformation("Loading requests for department: {Department}", SelectedDepartment.Name);
                            response = await _requestService.GetRequestsByDepartmentAsync(SelectedDepartment.Name, pagination);
                        }
                        else
                        {
                            // For managers, if "All" is selected, load requests for their own department
                            var managerDepartment = currentUserDto?.Department;
                            if (!string.IsNullOrEmpty(managerDepartment))
                            {
                                _logger.LogInformation("Loading requests for manager's department: {Department}", managerDepartment);
                                response = await _requestService.GetRequestsByDepartmentAsync(managerDepartment, pagination);
                            }
                            else
                            {
                                _logger.LogWarning("Manager has no department assigned, loading own requests only");
                                response = await _requestService.GetMyRequestsAsync(pagination);
                            }
                        }
                        break;
                    case RequestAccessLevel.Own:
                    case RequestAccessLevel.None:
                    default:
                        _logger.LogInformation("Loading own requests for regular user");
                        response = await _requestService.GetMyRequestsAsync(pagination);
                        break;
                }

                _logger.LogInformation("API Response - Success: {Success}, Message: {Message}", 
                    response?.Success, response?.Message);

                if (response?.Success == true)
                {
                    if (response.Data?.Items != null)
                    {
                        _logger.LogInformation("Received {Count} requests from API", response.Data.Items.Count());
                        
                        foreach (var request in response.Data.Items)
                        {
                            // Apply client-side filtering for managers if "All" departments is selected
                            if (accessLevel == RequestAccessLevel.Department && SelectedDepartment?.Id == "0")
                            {
                                var currentUser = await GetCurrentUserDtoAsync();
                                if (currentUser != null && AuthorizationUtilities.CanViewRequest(currentUser, request))
                                {
                                    Requests.Add(request);
                                    _logger.LogDebug("Added request {RequestId} for user {UserName}", request.RequestID, request.UserName);
                                }
                                else
                                {
                                    _logger.LogDebug("Filtered out request {RequestId} for user {UserName}", request.RequestID, request.UserName);
                                }
                            }
                            else
                            {
                                Requests.Add(request);
                                _logger.LogDebug("Added request {RequestId} for user {UserName}", request.RequestID, request.UserName);
                            }
                        }
                        _logger.LogInformation("Successfully loaded {Count} requests after filtering.", Requests.Count);
                    }
                    else
                    {
                        _logger.LogWarning("API returned success but Data.Items is null");
                    }
                }
                else
                {
                    _logger.LogError("API request failed - Success: {Success}, Message: {Message}", 
                        response?.Success, response?.Message);
                    
                    if (response?.Message != null)
                    {
                        await Shell.Current.DisplayAlert("Error", $"Failed to load requests: {response.Message}", "OK");
                    }
                    else
                    {
                        await Shell.Current.DisplayAlert("Error", "Failed to load requests. Please check your connection and try again.", "OK");
                    }
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
            return RequestStateManager.CanEdit(request, currentUser.IsAdmin ?? false, isOwner);
        }

        /// <summary>
        /// Gets the current user as UserDto asynchronously
        /// </summary>
        private async Task<UserDto?> GetCurrentUserDtoAsync()
        {
            var currentUser = await _authService.GetCurrentUserAsync();
            return currentUser;
        }

        // Remove obsolete ApproveRequestAsync/RejectRequestAsync usages and use new role-specific methods
        [RelayCommand(CanExecute = nameof(CanApproveRejectRequest))]
        private async Task ApproveRequestAsync(RequestResponseDto? request)
        {
            if (request == null) return;
            try
            {
                var currentUser = await GetCurrentUserDtoAsync();
                ApiResponse<RequestResponseDto>? response = null;
                if (currentUser?.IsManager == true)
                {
                    var approvalDto = new ManagerApprovalDto { ManagerRemarks = "Approved via mobile app" };
                    response = await _requestService.ManagerApproveRequestAsync(request.RequestID, approvalDto);
                }
                else if (currentUser?.IsHR == true)
                {
                    var approvalDto = new HRApprovalDto { HRRemarks = "Approved via mobile app" };
                    response = await _requestService.HRApproveRequestAsync(request.RequestID, approvalDto);
                }
                else
                {
                    await Shell.Current.DisplayAlert("Error", "You do not have permission to approve this request.", "OK");
                    return;
                }
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
                var currentUser = await GetCurrentUserDtoAsync();
                ApiResponse<RequestResponseDto>? response = null;
                if (currentUser?.IsManager == true)
                {
                    var rejectDto = new ManagerRejectDto { ManagerRemarks = "Rejected via mobile app" };
                    response = await _requestService.ManagerRejectRequestAsync(request.RequestID, rejectDto);
                }
                else if (currentUser?.IsHR == true)
                {
                    var rejectDto = new HRRejectDto { HRRemarks = "Rejected via mobile app" };
                    response = await _requestService.HRRejectRequestAsync(request.RequestID, rejectDto);
                }
                else
                {
                    await Shell.Current.DisplayAlert("Error", "You do not have permission to reject this request.", "OK");
                    return;
                }
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

        // Add a method to load departments dynamically (similar to SignupViewModel)
        public async Task LoadDepartmentsAsync()
        {
            if (_lookupService == null) return;
            var allDepartments = await _lookupService.GetDepartmentsAsync();
            var user = await GetCurrentUserDtoAsync();
            List<LookupItem> filteredDepartments;
            if (user == null || user.IsAdmin == true || user.IsHR == true)
            {
                filteredDepartments = allDepartments.ToList();
            }
            else if (user.IsManager == true)
            {
                var accessible = AuthorizationUtilities.GetAccessibleDepartments(user, allDepartments.Select(d => d.Name)).ToHashSet(StringComparer.OrdinalIgnoreCase);
                filteredDepartments = allDepartments.Where(d => accessible.Contains(d.Name)).ToList();
            }
            else
            {
                filteredDepartments = allDepartments.ToList();
            }
            filteredDepartments.Insert(0, new LookupItem { Name = "All", Id = "0" });
            Departments = new ObservableCollection<LookupItem>(filteredDepartments);
            if (Departments.Count > 0)
                SelectedDepartment = Departments[0];
        }

        /// <summary>
        /// Diagnostic method to test API connectivity and authentication
        /// </summary>
        [RelayCommand]
        private async Task TestApiConnectivityAsync()
        {
            try
            {
                _logger.LogInformation("=== Testing API Connectivity ===");
                
                // Test 1: Check current user
                var currentUser = await _authService.GetCurrentUserAsync();
                _logger.LogInformation("Current User Test - Success: {Success}, User: {UserName}", 
                    currentUser != null, currentUser?.FullName);
                
                // Test 2: Check token
                var token = await _authService.GetCurrentTokenAsync();
                _logger.LogInformation("Token Test - Has Token: {HasToken}, Length: {Length}", 
                    !string.IsNullOrEmpty(token), token?.Length ?? 0);
                
                // Test 3: Try to get user ID
                var userId = await _authService.GetCurrentUserIdAsync();
                _logger.LogInformation("User ID Test - User ID: {UserId}", userId);
                
                // Test 4: Try a simple API call
                try
                {
                    var testResponse = await _requestService.GetMyRequestsAsync(new RequestPaginationDto { Page = 1, PageSize = 1 });
                    _logger.LogInformation("API Test - Success: {Success}, Message: {Message}", 
                        testResponse?.Success, testResponse?.Message);
                }
                catch (Exception apiEx)
                {
                    _logger.LogError(apiEx, "API Test failed: {Message}", apiEx.Message);
                }
                
                await Shell.Current.DisplayAlert("Diagnostic Complete", 
                    $"User: {currentUser?.FullName ?? "None"}\nUser ID: {userId}\nHas Token: {!string.IsNullOrEmpty(token)}", 
                    "OK");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Diagnostic test failed: {Message}", ex.Message);
                await Shell.Current.DisplayAlert("Diagnostic Failed", ex.Message, "OK");
            }
        }
    }
}