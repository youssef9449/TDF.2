using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Threading;
using Microsoft.Maui.Controls;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TDFShared.DTOs.Requests;
using TDFShared.DTOs.Users;
using TDFShared.Services;
using TDFShared.Enums;
using TDFShared.Utilities;
using TDFMAUI.Services;
using TDFShared.DTOs.Common;

namespace TDFMAUI.ViewModels
{

    public static class RequestOptions
    {
        public static readonly List<string> StatusOptions = new List<string> { "All" }.Concat(Enum.GetNames(typeof(RequestStatus))).ToList();
        public static readonly List<string> TypeOptions = new List<string> { "All" }.Concat(Enum.GetNames(typeof(LeaveType))).ToList();
    }

    public partial class RequestApprovalViewModel : ObservableObject, IDisposable
    {
        private readonly IRequestService? _requestService;
        private readonly Services.INotificationService? _notificationService;
        private readonly IAuthService? _authService;
        private readonly ILogger<RequestApprovalViewModel>? _logger;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private bool _disposed = false;
        private readonly IUserSessionService? _userSessionService;

        private readonly ILookupService? _lookupService;

        [ObservableProperty]
        private ObservableCollection<RequestResponseDto> _requests;

        [ObservableProperty]
        private RequestResponseDto? _selectedRequest;

        [ObservableProperty]
        private ObservableCollection<RequestResponseDto> _pendingRequests;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private int _pendingCount;

        [ObservableProperty]
        private int _approvedCount;

        [ObservableProperty]
        private int _rejectedCount;

        [ObservableProperty]
        private DateTime _fromDate = DateTime.Now.AddDays(-30);

        [ObservableProperty]
        private DateTime _toDate = DateTime.Now;

        [ObservableProperty]
        private string _selectedStatus = "All";

        [ObservableProperty]
        private string _selectedType = "All";

        [ObservableProperty]
        private ObservableCollection<LookupItem> _departments = new();

        [ObservableProperty]
        private LookupItem? _selectedDepartment;

        // Role properties using nullable bool
        [ObservableProperty]
        private bool? _isAdmin;

        [ObservableProperty]
        private bool? _isManager;

        [ObservableProperty]
        private bool? _isHR;

        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private bool _hasError = false;

        // Pagination Properties
        [ObservableProperty]
        private int _currentPage = 1;

        [ObservableProperty]
        private int _pageSize = 10;

        [ObservableProperty]
        private int _totalItems;

        [ObservableProperty]
        private int _totalPages;

        [ObservableProperty]
        private bool _canGoToFirstPage;

        [ObservableProperty]
        private bool _canGoToPreviousPage;

        [ObservableProperty]
        private bool _canGoToNextPage;

        [ObservableProperty]
        private bool _canGoToLastPage;

        [ObservableProperty]
        private ObservableCollection<int> _pageNumbers;

        [ObservableProperty]
        private ObservableCollection<int> _pageSizeOptions;

        [ObservableProperty]
        private bool _hasRequests;

        public string PageInfo => $"Page {CurrentPage} of {TotalPages}";

        public List<string> StatusOptions => RequestOptions.StatusOptions;
        public List<string> TypeOptions => RequestOptions.TypeOptions;

        // Authorization properties using cached user and RequestStateManager
        public bool CanManageRequests => GetCachedCurrentUser() is UserDto user && RequestStateManager.CanManageRequests(user);
        public bool CanEditDeleteAny => GetCachedCurrentUser() is UserDto user && (user.IsAdmin ?? false);
        public bool CanFilterByDepartment => GetCachedCurrentUser() is UserDto user &&
            ((user.IsAdmin ?? false) || (user.IsHR ?? false) || (user.IsManager ?? false));

        // UI: Populate Departments based on user role
        private async Task LoadDepartmentsAsync()
        {
            try
            {
                var allDepartments = await _lookupService!.GetDepartmentsAsync();
                var user = GetCachedCurrentUser();
                List<LookupItem> filteredDepartments;
                if (user == null)
                {
                    filteredDepartments = allDepartments.ToList();
                }
                else if ((user.IsAdmin ?? false) || (user.IsHR ?? false))
                {
                    filteredDepartments = allDepartments.ToList();
                }
                else if (user.IsManager ?? false)
                {
                    // Only show departments the manager can manage
                    var accessible = TDFShared.Utilities.AuthorizationUtilities.GetAccessibleDepartments(
                        user,
                        allDepartments.Select(d => d.Name)
                    ).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    filteredDepartments = allDepartments.Where(d => accessible.Contains(d.Name)).ToList();
                }
                else
                {
                    filteredDepartments = allDepartments.ToList();
                }
                // Always add "All" at the top
                filteredDepartments.Insert(0, new LookupItem { Name = "All", Id = "0" });
                Departments = new ObservableCollection<LookupItem>(filteredDepartments);
                if (Departments.Count > 0)
                    SelectedDepartment = Departments[0];
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading departments");
            }
        }

        // Commands using RelayCommand for better performance and cancellation support
        [RelayCommand(CanExecute = nameof(CanExecuteApprove))]
        private async Task ApproveRequestAsync(int requestId, CancellationToken cancellationToken = default)
        {
            await ApproveRequestInternalAsync(requestId, cancellationToken);
        }

        [RelayCommand(CanExecute = nameof(CanExecuteReject))]
        private async Task RejectRequestAsync(int requestId, CancellationToken cancellationToken = default)
        {
            await RejectRequestInternalAsync(requestId, cancellationToken);
        }

        [RelayCommand]
        private async Task ViewRequestAsync(int requestId, CancellationToken cancellationToken = default)
        {
            await ViewRequestInternalAsync(requestId, cancellationToken);
        }

        [RelayCommand]
        private async Task LoadRequestsAsync(CancellationToken cancellationToken = default)
        {
            await LoadRequestsInternalAsync(cancellationToken);
        }

        [RelayCommand]
        private async Task RefreshRequestsAsync(CancellationToken cancellationToken = default)
        {
            // Invalidate user cache to ensure fresh permissions
            InvalidateUserCache();
            await LoadRequestsInternalAsync(cancellationToken);
        }
        
        [RelayCommand(CanExecute = nameof(CanGoToFirstPage))]
        private async Task FirstPageAsync()
        {
            CurrentPage = 1;
            await LoadRequestsInternalAsync();
        }

        [RelayCommand(CanExecute = nameof(CanGoToPreviousPage))]
        private async Task PreviousPageAsync()
        {
            CurrentPage--;
            await LoadRequestsInternalAsync();
        }

        [RelayCommand(CanExecute = nameof(CanGoToNextPage))]
        private async Task NextPageAsync()
        {
            CurrentPage++;
            await LoadRequestsInternalAsync();
        }

        [RelayCommand(CanExecute = nameof(CanGoToLastPage))]
        private async Task LastPageAsync()
        {
            CurrentPage = TotalPages;
            await LoadRequestsInternalAsync();
        }

        /// <summary>
        /// Public method to refresh user permissions cache
        /// Call this when user permissions might have changed
        /// </summary>
        public async Task RefreshUserPermissionsAsync()
        {
            InvalidateUserCache();
            await RefreshUserCacheAsync();
        }

        public RequestApprovalViewModel(
            IRequestService requestService,
            Services.INotificationService notificationService,
            IAuthService authService,
            ILogger<RequestApprovalViewModel> logger,
            ILookupService lookupService,
            IUserSessionService userSessionService)
        {
            _title = "Request Approval";
            _requestService = requestService ?? throw new ArgumentNullException(nameof(requestService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _lookupService = lookupService ?? throw new ArgumentNullException(nameof(lookupService));
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));

            Requests = new ObservableCollection<RequestResponseDto>();
            PendingRequests = new ObservableCollection<RequestResponseDto>();
            PageNumbers = new ObservableCollection<int>();
            PageSizeOptions = new ObservableCollection<int> { 5, 10, 20, 50 };
            _selectedRequest = null;

            _logger.LogInformation("RequestApprovalViewModel initialized");

            // Initialize asynchronously without blocking constructor
            _ = Task.Run(async () =>
            {
                try
                {
                    await LoadDepartmentsAsync();
                    await RefreshUserCacheAsync();
                    await LoadUserRolesAsync(_cancellationTokenSource.Token);
                    await LoadRequestsInternalAsync(_cancellationTokenSource.Token);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during RequestApprovalViewModel initialization");
                    await HandleErrorAsync("Failed to initialize request approval view", ex);
                }
            });
        }

        // Default parameterless constructor for design-time support ONLY
        public RequestApprovalViewModel()
        {
            // This constructor is ONLY for design-time support (XAML previews, designers)
            // Production code MUST use the dependency injection constructor above

            if (Microsoft.Maui.Controls.DesignMode.IsDesignModeEnabled)
            {
                // Design-time initialization only
                _title = "Request Approval";
                Requests = new ObservableCollection<RequestResponseDto>();
                PendingRequests = new ObservableCollection<RequestResponseDto>();
                PageNumbers = new ObservableCollection<int>();
                PageSizeOptions = new ObservableCollection<int> { 5, 10, 20, 50 };
                _selectedRequest = null;
                _requestService = null;
                _notificationService = null;
                _authService = null;
                _logger = null;
                _lookupService = null;

                // Add sample data for design-time preview
                Requests.Add(new RequestResponseDto
                {
                    RequestID = 1,
                    UserName = "John Doe",
                    LeaveType = TDFShared.Enums.LeaveType.Annual,
                    RequestStartDate = DateTime.Today.AddDays(7),
                    Status = TDFShared.Enums.RequestStatus.Pending
                });

                PendingCount = 1;
                ApprovedCount = 0;
                RejectedCount = 0;
            }
            else
            {
                // Production runtime - this should NEVER be called
                throw new InvalidOperationException(
                    "RequestApprovalViewModel parameterless constructor should only be used for design-time support. " +
                    "In production, use dependency injection with the constructor that accepts IRequestService, " +
                    "INotificationService, and IAuthService parameters. Check your service registration in MauiProgram.cs.");
            }
        }

        private async Task LoadUserRolesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger?.LogDebug("Loading user roles");
                var currentUser = _authService != null ? await _authService.GetCurrentUserAsync() : null;
                if (currentUser != null)
                {
                    IsAdmin = currentUser.IsAdmin;
                    IsManager = currentUser.IsManager;
                    IsHR = currentUser.IsHR;

                    _logger?.LogInformation("User roles loaded: Admin={IsAdmin}, Manager={IsManager}, HR={IsHR}",
                        IsAdmin, IsManager, IsHR);

                    OnPropertyChanged(nameof(CanManageRequests));
                    OnPropertyChanged(nameof(CanEditDeleteAny));
                    OnPropertyChanged(nameof(CanFilterByDepartment));
                }
                else
                {
                    _logger?.LogWarning("No current user found when loading roles");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading user roles");
                if (_notificationService != null)
                    await _notificationService.ShowErrorAsync($"Error loading user roles: {ex.Message}");
            }
        }

        private async Task HandleErrorAsync(string message, Exception ex)
        {
            ErrorMessage = message;
            HasError = true;

            try
            {
                if (_notificationService != null)
                    await _notificationService.ShowErrorAsync($"{message}: {ex.Message}");
            }
            catch (Exception notificationEx)
            {
                _logger?.LogError(notificationEx, "Failed to show error notification");
            }
        }

        private void ClearError()
        {
            ErrorMessage = string.Empty;
            HasError = false;
        }

        // Authorization checks using AuthorizationUtilities
        private bool CanApprove(RequestResponseDto request)
        {
            var currentUser = GetCachedCurrentUser();
            return currentUser != null && AuthorizationUtilities.CanPerformRequestAction(currentUser, request, TDFShared.Utilities.RequestAction.Approve);
        }

        private bool CanReject(RequestResponseDto request)
        {
            var currentUser = GetCachedCurrentUser();
            return currentUser != null && AuthorizationUtilities.CanPerformRequestAction(currentUser, request, TDFShared.Utilities.RequestAction.Reject);
        }

        public bool CanExecuteApprove(int id) =>
            Requests?.FirstOrDefault(r => r.RequestID == id) is RequestResponseDto req && CanApprove(req);

        public bool CanExecuteReject(int id) =>
            Requests?.FirstOrDefault(r => r.RequestID == id) is RequestResponseDto req && CanReject(req);

        partial void OnRequestsChanged(ObservableCollection<RequestResponseDto> value)
        {
            // Update command can-execute state when requests change
            ApproveRequestCommand?.NotifyCanExecuteChanged();
            RejectRequestCommand?.NotifyCanExecuteChanged();
        }

        private async Task ApproveRequestInternalAsync(int requestId, CancellationToken cancellationToken = default)
        {
            var request = Requests.FirstOrDefault(r => r.RequestID == requestId);
            if (request == null)
            {
                _logger?.LogWarning($"Attempted to approve non-existent request with ID {requestId}");
                return;
            }

            try
            {
                ClearError();
                IsLoading = true;

                _logger?.LogInformation($"Approving request {requestId} for user {request.UserName}");

                var currentUser = GetCachedCurrentUser();
                if (currentUser == null)
                {
                    if (_notificationService != null)
                        await _notificationService.ShowErrorAsync("User not authenticated");
                    return;
                }

                string remarks = await Shell.Current.DisplayPromptAsync(
                    "Approve Request",
                    currentUser.IsManager == true ? "Manager remarks (optional):" : 
                    currentUser.IsAdmin == true ? "Admin remarks (optional):" : "HR remarks (optional):",
                    "OK",
                    "Cancel",
                    keyboard: Keyboard.Text
                );
                if (remarks == null) remarks = string.Empty; // User cancelled

                ApiResponse<RequestResponseDto> result = new();
                if (currentUser.IsManager == true)
                {
                    var approvalDto = new ManagerApprovalDto { ManagerRemarks = remarks };
                    if (_requestService != null)
                        result = await _requestService.ManagerApproveRequestAsync(requestId, approvalDto);
                }
                else if (currentUser.IsHR == true || currentUser.IsAdmin == true)
                {
                    // Both HR and Admin users use HR approval endpoint
                    var approvalDto = new HRApprovalDto { HRRemarks = remarks };
                    if (_requestService != null)
                        result = await _requestService.HRApproveRequestAsync(requestId, approvalDto);
                }
                else
                {
                    if (_notificationService != null)
                        await _notificationService.ShowErrorAsync("You do not have permission to approve this request.");
                    return;
                }

                if (result?.Success == true)
                {
                    var approvedId = result.Data != null ? result.Data.RequestID : requestId;
                    _logger?.LogInformation($"Request {approvedId} approved successfully");
                    if (_notificationService != null)
                        await _notificationService.ShowSuccessAsync("Request approved successfully");
                    await LoadRequestsInternalAsync(cancellationToken);
                }
                else
                {
                    _logger?.LogWarning($"Failed to approve request {requestId}");
                    await HandleErrorAsync(result?.Message ?? "Failed to approve request", new InvalidOperationException("Approval operation failed"));
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error approving request {requestId}");
                await HandleErrorAsync("Error approving request", ex);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task RejectRequestInternalAsync(int requestId, CancellationToken cancellationToken = default)
        {
            var request = Requests.FirstOrDefault(r => r.RequestID == requestId);
            if (request == null)
            {
                _logger?.LogWarning($"Attempted to reject non-existent request with ID {requestId}");
                return;
            }

            var currentUser = GetCachedCurrentUser();
            if (currentUser == null)
            {
                if (_notificationService != null)
                    await _notificationService.ShowErrorAsync("User not authenticated");
                return;
            }

            string reason = await Shell.Current.DisplayPromptAsync(
                "Reject Request",
                currentUser.IsManager == true ? "Manager rejection reason:" : 
                currentUser.IsAdmin == true ? "Admin rejection reason:" : "HR rejection reason:",
                "OK",
                "Cancel",
                keyboard: Keyboard.Text
            );

            if (string.IsNullOrWhiteSpace(reason))
            {
                if (reason != null) // User didn't cancel
                {
                    _logger?.LogInformation($"User attempted to reject request {requestId} without providing a reason");
                    if (_notificationService != null)
                        await _notificationService.ShowErrorAsync("A rejection reason is required");
                }
                return;
            }

            try
            {
                ClearError();
                IsLoading = true;

                _logger?.LogInformation($"Rejecting request {requestId} for user {request.UserName} with reason: {reason}");

                ApiResponse<RequestResponseDto> result = new();
                if (currentUser.IsManager == true)
                {
                    var rejectDto = new ManagerRejectDto { ManagerRemarks = reason };
                    if (_requestService != null)
                        result = await _requestService.ManagerRejectRequestAsync(requestId, rejectDto);
                }
                else if (currentUser.IsHR == true || currentUser.IsAdmin == true)
                {
                    // Both HR and Admin users use HR rejection endpoint
                    var rejectDto = new HRRejectDto { HRRemarks = reason };
                    if (_requestService != null)
                        result = await _requestService.HRRejectRequestAsync(requestId, rejectDto);
                }
                else
                {
                    if (_notificationService != null)
                        await _notificationService.ShowErrorAsync("You do not have permission to reject this request.");
                    return;
                }

                if (result?.Success == true)
                {
                    var rejectedId = result.Data != null ? result.Data.RequestID : requestId;
                    _logger?.LogInformation($"Request {rejectedId} rejected successfully");
                    if (_notificationService != null)
                        await _notificationService.ShowSuccessAsync("Request rejected successfully");
                    await LoadRequestsInternalAsync(cancellationToken);
                }
                else
                {
                    _logger?.LogWarning($"Failed to reject request {requestId}");
                    await HandleErrorAsync(result?.Message ?? "Failed to reject request", new InvalidOperationException("Rejection operation failed"));
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error rejecting request {requestId}");
                await HandleErrorAsync("Error rejecting request", ex);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadRequestsInternalAsync(CancellationToken cancellationToken = default)
        {
            if (IsLoading) return;

            try
            {
                ClearError();
                IsLoading = true;

                _logger?.LogDebug($"Loading requests with filters: Status={SelectedStatus}, Type={SelectedType}, Department={SelectedDepartment?.Name}");

                Requests.Clear();

                var requestPagination = new RequestPaginationDto
                {
                    Page = 1,
                    PageSize = 50, // Increased page size for better performance
                    FromDate = FromDate,
                    ToDate = ToDate,
                    FilterStatus = (string.IsNullOrEmpty(SelectedStatus) || SelectedStatus == "All")
                        ? (RequestStatus?)null
                        : Enum.TryParse<RequestStatus>(SelectedStatus, true, out var parsedStatus) ? parsedStatus : (RequestStatus?)null,
                    FilterType = (string.IsNullOrEmpty(SelectedType) || SelectedType == "All")
                        ? (LeaveType?)null
                        : Enum.TryParse<LeaveType>(SelectedType, true, out var parsedType) ? parsedType : (LeaveType?)null,
                    Department = SelectedDepartment?.Name == "All" ? null : SelectedDepartment?.Name
                };

                // Use RequestStateManager to ensure proper access control
                // Get current user from session service
                var currentUser = _userSessionService?.CurrentUser;
                if (currentUser == null)
                {
                    _logger?.LogWarning("No current user found when loading requests");
                    await HandleErrorAsync("Authentication required", new UnauthorizedAccessException("No current user"));
                    return;
                }

                // Verify user can manage requests using RequestStateManager
                if (!RequestStateManager.CanManageRequests(currentUser))
                {
                    _logger?.LogWarning($"User {currentUser.UserID} attempted to access request approval without proper permissions");
                    await HandleErrorAsync("Access denied", new UnauthorizedAccessException("Insufficient permissions to manage requests"));
                    return;
                }

                var result = _requestService != null ? await _requestService.GetRequestsForApprovalAsync(
                    requestPagination.Page,
                    requestPagination.PageSize,
                    SelectedStatus == "All" ? null : SelectedStatus,
                    SelectedType == "All" ? null : SelectedType,
                    FromDate,
                    ToDate,
                    SelectedDepartment?.Name == "All" ? null : SelectedDepartment?.Name
                ) : null;

                if (result?.Data?.Items != null)
                {
                    var requestCount = result.Data.Items.Count();
                    _logger?.LogInformation($"Loaded {requestCount} requests");

                    // Filter requests based on user permissions using RequestStateManager
                    var filteredRequests = result.Data.Items.Where(request =>
                        RequestStateManager.CanViewRequest(request, currentUser)).ToList();

                    _logger?.LogDebug($"Filtered to {filteredRequests.Count} viewable requests out of {requestCount} total");

                    foreach (var request in filteredRequests)
                    {
                        Requests.Add(request);
                    }

                    // Update statistics based on filtered requests
                    PendingCount = filteredRequests.Count(r => r.Status == RequestStatus.Pending);
                    ApprovedCount = filteredRequests.Count(r => r.Status == RequestStatus.ManagerApproved || r.Status == RequestStatus.HRApproved);
                    RejectedCount = filteredRequests.Count(r => r.Status == RequestStatus.Rejected || r.Status == RequestStatus.ManagerRejected);

                    _logger?.LogDebug($"Request statistics: Pending={PendingCount}, Approved={ApprovedCount}, Rejected={RejectedCount}");
                }
                else
                {
                    _logger?.LogWarning("No requests returned from service");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading requests");
                await HandleErrorAsync("Error loading requests", ex);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ViewRequestInternalAsync(int requestId, CancellationToken cancellationToken = default)
        {
            var request = Requests.FirstOrDefault(r => r.RequestID == requestId);
            if (request == null)
            {
                _logger?.LogWarning($"Attempted to view non-existent request with ID {requestId}");
                return;
            }

            // Check if user can view this request using RequestStateManager
            var currentUser = GetCachedCurrentUser();
            if (currentUser == null || !RequestStateManager.CanViewRequest(request, currentUser))
            {
                _logger?.LogWarning($"User {currentUser?.UserID} attempted to view request {requestId} without proper permissions");
                if (_notificationService != null)
                    await _notificationService.ShowErrorAsync("You don't have permission to view this request");
                return;
            }

            try
            {
                _logger?.LogInformation($"Viewing request details for request {requestId}");

                var requestDetails = _requestService != null ? await _requestService.GetRequestByIdAsync(requestId) : null;
                if (requestDetails?.Data == null)
                {
                    _logger?.LogWarning($"Could not find request details for ID {requestId}");
                    if (_notificationService != null)
                        await _notificationService.ShowErrorAsync("Could not find request details");
                    return;
                }

                await Shell.Current.GoToAsync($"RequestDetailsPage", new Dictionary<string, object>
                {
                    {"RequestId", requestDetails.Data.RequestID}
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error viewing request details for ID {requestId}");
                await HandleErrorAsync("Error viewing request details", ex);
            }
        }

        #region Authorization Helper Methods

        /// <summary>
        /// Gets the current user from cache or loads it asynchronously if cache is expired.
        /// This avoids the Task.Run anti-pattern for better performance.
        /// </summary>
        private UserDto? GetCachedCurrentUser()
        {
            // Use the centralized session service instead of local caching
            return _userSessionService?.CurrentUser;
        }

        /// <summary>
        /// Refreshes the user data via session service
        /// </summary>
        private async Task RefreshUserCacheAsync()
        {
            try
            {
                _logger?.LogDebug("Refreshing user data via session service");
                var user = _authService != null ? await _authService.GetCurrentUserAsync() : null;
                if (user != null)
                {
                    // The AuthService will update the UserSessionService automatically
                    _logger?.LogDebug("User data refreshed successfully via session service");

                    // Notify property changes for authorization-dependent properties
                    OnPropertyChanged(nameof(CanManageRequests));
                    OnPropertyChanged(nameof(CanEditDeleteAny));
                    OnPropertyChanged(nameof(CanFilterByDepartment));
                }
                else
                {
                    _logger?.LogWarning("Failed to refresh user data - no user returned");
                    // Clear session if no user found
                    _userSessionService?.ClearUserData();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error refreshing user data");
                // Clear session on error
                _userSessionService?.ClearUserData();
            }
        }

        /// <summary>
        /// Invalidates the user cache, forcing a refresh on next access
        /// </summary>
        private void InvalidateUserCache()
        {
            // No longer needed as we use centralized session service
            // But keeping method for backward compatibility
            _logger?.LogDebug("User cache invalidation requested - using centralized session service");
        }

        #endregion

        #region IDisposable Implementation

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _logger?.LogDebug("Disposing RequestApprovalViewModel");

                try
                {
                    _cancellationTokenSource?.Cancel();
                    _cancellationTokenSource?.Dispose();

                    // Clear user cache
                    InvalidateUserCache();
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error during RequestApprovalViewModel disposal");
                }

                _disposed = true;
            }
        }

        #endregion
    }
}