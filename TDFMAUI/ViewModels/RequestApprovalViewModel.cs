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

namespace TDFMAUI.ViewModels
{
    public static class RequestOptions
    {
        public static readonly List<string> StatusOptions = new List<string> { "All" }.Concat(Enum.GetNames(typeof(RequestStatus))).ToList();
        public static readonly List<string> TypeOptions = new List<string> { "All" }.Concat(Enum.GetNames(typeof(LeaveType))).ToList();
        public static readonly List<string> DepartmentOptions = new() { "All", "HR", "IT", "Finance", "Marketing", "Operations" };
    }

    public partial class RequestApprovalViewModel : ObservableObject, IDisposable
    {
        private readonly TDFMAUI.Services.IRequestService _requestService;
        private readonly TDFMAUI.Services.INotificationService _notificationService;
        private readonly TDFShared.Services.IAuthService _authService;
        private readonly ILogger<RequestApprovalViewModel> _logger;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private bool _disposed = false;

        // Cached user for authorization checks to avoid Task.Run
        private UserDto? _cachedCurrentUser;
        private DateTime _userCacheExpiry = DateTime.MinValue;
        private readonly TimeSpan _userCacheTimeout = TimeSpan.FromMinutes(5);

        [ObservableProperty]
        private ObservableCollection<RequestResponseDto> _requests;

        [ObservableProperty]
        private RequestResponseDto _selectedRequest;

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
        private string _selectedDepartment = "All";

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

        public List<string> StatusOptions => RequestOptions.StatusOptions;
        public List<string> TypeOptions => RequestOptions.TypeOptions;
        public List<string> DepartmentOptions => RequestOptions.DepartmentOptions;

        // Authorization properties using cached user and RequestStateManager
        public bool CanManageRequests => GetCachedCurrentUser() is UserDto user && RequestStateManager.CanManageRequests(user);
        public bool CanEditDeleteAny => GetCachedCurrentUser() is UserDto user && user.IsAdmin;
        public bool CanFilterByDepartment => GetCachedCurrentUser() is UserDto user && user.IsManager;
        public bool CanManageDepartment(string department) => GetCachedCurrentUser() is UserDto user && RequestStateManager.CanManageDepartment(user, department);

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
            TDFMAUI.Services.IRequestService requestService,
            TDFMAUI.Services.INotificationService notificationService,
            TDFShared.Services.IAuthService authService,
            ILogger<RequestApprovalViewModel> logger)
        {
            _title = "Request Approval";
            _requestService = requestService ?? throw new ArgumentNullException(nameof(requestService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            Requests = new ObservableCollection<RequestResponseDto>();
            PendingRequests = new ObservableCollection<RequestResponseDto>();

            _logger.LogInformation("RequestApprovalViewModel initialized");

            // Initialize asynchronously without blocking constructor
            _ = Task.Run(async () =>
            {
                try
                {
                    // Initialize user cache first
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
                // If this is reached, it indicates a dependency injection configuration error
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
                _logger.LogDebug("Loading user roles");
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser != null)
                {
                    IsAdmin = currentUser.IsAdmin;
                    IsManager = currentUser.IsManager;
                    IsHR = currentUser.IsHR;

                    _logger.LogInformation("User roles loaded: Admin={IsAdmin}, Manager={IsManager}, HR={IsHR}",
                        IsAdmin, IsManager, IsHR);

                    OnPropertyChanged(nameof(CanManageRequests));
                    OnPropertyChanged(nameof(CanEditDeleteAny));
                    OnPropertyChanged(nameof(CanFilterByDepartment));
                }
                else
                {
                    _logger.LogWarning("No current user found when loading roles");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading user roles");
                await HandleErrorAsync("Error loading user roles", ex);
            }
        }

        private async Task HandleErrorAsync(string message, Exception ex)
        {
            ErrorMessage = message;
            HasError = true;

            try
            {
                await _notificationService.ShowErrorAsync($"{message}: {ex.Message}");
            }
            catch (Exception notificationEx)
            {
                _logger.LogError(notificationEx, "Failed to show error notification");
            }
        }

        private void ClearError()
        {
            ErrorMessage = string.Empty;
            HasError = false;
        }

        // Authorization checks using TDFShared RequestStateManager
        private bool CanApprove(RequestResponseDto request)
        {
            var currentUser = GetCachedCurrentUser();
            return currentUser != null && RequestStateManager.CanApproveOrRejectRequest(request, currentUser);
        }

        private bool CanReject(RequestResponseDto request) => CanApprove(request); // Same logic as approve

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
                _logger.LogWarning($"Attempted to approve non-existent request with ID {requestId}");
                return;
            }

            try
            {
                ClearError();
                IsLoading = true;

                _logger.LogInformation($"Approving request {requestId} for user {request.UserName}");

                var approvalDto = new RequestApprovalDto
                {
                    Status = RequestStatus.Approved,
                    Comment = "Approved via approval page"
                };

                bool success = await _requestService.ApproveRequestAsync(requestId, approvalDto);
                if (success)
                {
                    _logger.LogInformation($"Request {requestId} approved successfully");
                    await _notificationService.ShowSuccessAsync("Request approved successfully");
                    await LoadRequestsInternalAsync(cancellationToken);
                }
                else
                {
                    _logger.LogWarning($"Failed to approve request {requestId}");
                    await HandleErrorAsync("Failed to approve request", new InvalidOperationException("Approval operation failed"));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error approving request {requestId}");
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
                _logger.LogWarning("Attempted to reject non-existent request with ID {RequestId}", requestId);
                return;
            }

            string reason = await Shell.Current.DisplayPromptAsync(
                "Reject Request",
                "Please provide a reason for rejection:",
                "OK",
                "Cancel",
                keyboard: Keyboard.Text
            );

            if (string.IsNullOrWhiteSpace(reason))
            {
                if (reason != null) // User didn't cancel
                {
                    _logger.LogInformation("User attempted to reject request {RequestId} without providing a reason", requestId);
                    await _notificationService.ShowErrorAsync("A rejection reason is required");
                }
                return;
            }

            try
            {
                ClearError();
                IsLoading = true;

                _logger.LogInformation($"Rejecting request {requestId} for user {request.UserName} with reason: {reason}");

                var rejectDto = new RequestRejectDto { RejectReason = reason };

                bool success = await _requestService.RejectRequestAsync(requestId, rejectDto);
                if (success)
                {
                    _logger.LogInformation($"Request {requestId} rejected successfully");
                    await _notificationService.ShowSuccessAsync("Request rejected successfully");
                    await LoadRequestsInternalAsync(cancellationToken);
                }
                else
                {
                    _logger.LogWarning($"Failed to reject request {requestId}");
                    await HandleErrorAsync("Failed to reject request", new InvalidOperationException("Rejection operation failed"));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error rejecting request {requestId}");
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

                _logger.LogDebug($"Loading requests with filters: Status={SelectedStatus}, Type={SelectedType}, Department={SelectedDepartment}");

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
                        : Enum.TryParse<LeaveType>(SelectedType, true, out var parsedType) ? parsedType : (LeaveType?)null
                };

                // Use RequestStateManager to ensure proper access control
                // Refresh user cache if needed
                if (_cachedCurrentUser == null || DateTime.UtcNow >= _userCacheExpiry)
                {
                    await RefreshUserCacheAsync();
                }

                var currentUser = _cachedCurrentUser;
                if (currentUser == null)
                {
                    _logger.LogWarning("No current user found when loading requests");
                    await HandleErrorAsync("Authentication required", new UnauthorizedAccessException("No current user"));
                    return;
                }

                // Verify user can manage requests using RequestStateManager
                if (!RequestStateManager.CanManageRequests(currentUser))
                {
                    _logger.LogWarning($"User {currentUser.UserID} attempted to access request approval without proper permissions");
                    await HandleErrorAsync("Access denied", new UnauthorizedAccessException("Insufficient permissions to manage requests"));
                    return;
                }

                var requests = await _requestService.GetAllRequestsAsync(requestPagination);

                if (requests?.Items != null)
                {
                    var requestCount = requests.Items.Count();
                    _logger.LogInformation($"Loaded {requestCount} requests");

                    // Filter requests based on user permissions using RequestStateManager
                    var filteredRequests = requests.Items.Where(request =>
                        RequestStateManager.CanViewRequest(request, currentUser)).ToList();

                    _logger.LogDebug($"Filtered to {filteredRequests.Count} viewable requests out of {requestCount} total");

                    foreach (var request in filteredRequests)
                    {
                        Requests.Add(request);
                    }

                    // Update statistics based on filtered requests
                    PendingCount = filteredRequests.Count(r => r.Status == RequestStatus.Pending);
                    ApprovedCount = filteredRequests.Count(r => r.Status == RequestStatus.Approved);
                    RejectedCount = filteredRequests.Count(r => r.Status == RequestStatus.Rejected);

                    _logger.LogDebug($"Request statistics: Pending={PendingCount}, Approved={ApprovedCount}, Rejected={RejectedCount}");
                }
                else
                {
                    _logger.LogWarning("No requests returned from service");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading requests");
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
                _logger.LogWarning($"Attempted to view non-existent request with ID {requestId}");
                return;
            }

            // Check if user can view this request using RequestStateManager
            var currentUser = GetCachedCurrentUser();
            if (currentUser == null || !RequestStateManager.CanViewRequest(request, currentUser))
            {
                _logger.LogWarning($"User {currentUser?.UserID} attempted to view request {requestId} without proper permissions");
                await _notificationService.ShowErrorAsync("You don't have permission to view this request");
                return;
            }

            try
            {
                _logger.LogInformation($"Viewing request details for request {requestId}");

                var requestDetails = await _requestService.GetRequestByIdAsync(requestId);
                if (requestDetails == null)
                {
                    _logger.LogWarning($"Could not find request details for ID {requestId}");
                    await _notificationService.ShowErrorAsync("Could not find request details");
                    return;
                }

                await Shell.Current.GoToAsync($"RequestDetailsPage", new Dictionary<string, object>
                {
                    {"RequestId", requestDetails.RequestID}
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error viewing request details for ID {requestId}");
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
            // Return cached user if still valid
            if (_cachedCurrentUser != null && DateTime.UtcNow < _userCacheExpiry)
            {
                return _cachedCurrentUser;
            }

            // Cache is expired or empty, but we can't await here in a synchronous method
            // For property bindings, we return null and trigger async refresh
            if (_cachedCurrentUser == null)
            {
                _logger.LogDebug("User cache is empty, triggering async refresh");
                _ = RefreshUserCacheAsync();
            }

            return _cachedCurrentUser; // May be null if cache is empty
        }

        /// <summary>
        /// Refreshes the user cache asynchronously
        /// </summary>
        private async Task RefreshUserCacheAsync()
        {
            try
            {
                _logger.LogDebug("Refreshing user cache");
                var user = await _authService.GetCurrentUserAsync();
                if (user != null)
                {
                    _cachedCurrentUser = user;
                    _userCacheExpiry = DateTime.UtcNow.Add(_userCacheTimeout);
                    _logger.LogDebug("User cache refreshed successfully");

                    // Notify property changes for authorization-dependent properties
                    OnPropertyChanged(nameof(CanManageRequests));
                    OnPropertyChanged(nameof(CanEditDeleteAny));
                    OnPropertyChanged(nameof(CanFilterByDepartment));
                }
                else
                {
                    _logger.LogWarning("Failed to refresh user cache - no user returned");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing user cache");
            }
        }

        /// <summary>
        /// Invalidates the user cache, forcing a refresh on next access
        /// </summary>
        private void InvalidateUserCache()
        {
            _cachedCurrentUser = null;
            _userCacheExpiry = DateTime.MinValue;
            _logger.LogDebug("User cache invalidated");
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
                _logger.LogDebug("Disposing RequestApprovalViewModel");

                try
                {
                    _cancellationTokenSource?.Cancel();
                    _cancellationTokenSource?.Dispose();

                    // Clear user cache
                    InvalidateUserCache();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during RequestApprovalViewModel disposal");
                }

                _disposed = true;
            }
        }

        #endregion
    }
}