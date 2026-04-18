using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using TDFShared.DTOs.Requests;
using TDFShared.DTOs.Users;
using TDFShared.Services;
using TDFShared.Enums;
using TDFShared.Utilities;
using TDFMAUI.Services;
using TDFShared.DTOs.Common;

namespace TDFMAUI.ViewModels
{
    [QueryProperty(nameof(UserId), "userId")]
    public partial class RequestApprovalViewModel : BaseViewModel, IDisposable
    {
        private readonly IRequestService _requestService;

        [ObservableProperty]
        private int _userId;

        partial void OnUserIdChanged(int value)
        {
            if (value > 0)
            {
                _ = LoadRequestsAsync();
            }
        }
        private readonly Services.INotificationClient _notificationService;
        private readonly IAuthService _authService;
        private readonly ILogger<RequestApprovalViewModel> _logger;
        private readonly ILookupService _lookupService;
        private readonly IUserSessionService _userSessionService;
        private readonly CancellationTokenSource _cts = new();

        [ObservableProperty]
        private ObservableCollection<RequestResponseDto> _requests = new();

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

        [ObservableProperty] private bool? _isAdmin;
        [ObservableProperty] private bool? _isManager;
        [ObservableProperty] private bool? _isHR;

        [ObservableProperty] private int _currentPage = 1;
        [ObservableProperty] private int _pageSize = 10;
        [ObservableProperty] private int _totalItems;
        [ObservableProperty] private int _totalPages;
        [ObservableProperty] private bool _canGoToFirstPage;
        [ObservableProperty] private bool _canGoToPreviousPage;
        [ObservableProperty] private bool _canGoToNextPage;
        [ObservableProperty] private bool _canGoToLastPage;
        [ObservableProperty] private ObservableCollection<int> _pageNumbers = new();
        [ObservableProperty] private ObservableCollection<int> _pageSizeOptions = new() { 5, 10, 20, 50 };
        [ObservableProperty] private bool _hasRequests;

        public string PageInfo => $"Page {CurrentPage} of {TotalPages}";

        public List<string> StatusOptions => RequestOptions.StatusOptions;
        public List<string> TypeOptions => RequestOptions.TypeOptions;

        public RequestApprovalViewModel(
            IRequestService requestService,
            Services.INotificationClient notificationService,
            IAuthService authService,
            ILogger<RequestApprovalViewModel> logger,
            ILookupService lookupService,
            IUserSessionService userSessionService)
        {
            _requestService = requestService;
            _notificationService = notificationService;
            _authService = authService;
            _logger = logger;
            _lookupService = lookupService;
            _userSessionService = userSessionService;
            Title = "Request Approval";

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            await LoadDepartmentsAsync();
            await LoadUserRolesAsync();
            await LoadRequestsAsync();
        }

        private async Task LoadUserRolesAsync()
        {
            var user = _userSessionService.CurrentUser;
            if (user != null)
            {
                IsAdmin = user.IsAdmin;
                IsManager = user.IsManager;
                IsHR = user.IsHR;
            }
        }

        private async Task LoadDepartmentsAsync()
        {
            try
            {
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
            catch (Exception ex) { _logger.LogError(ex, "Error loading departments"); }
        }

        [RelayCommand]
        public async Task LoadRequestsAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                var user = _userSessionService.CurrentUser;
                if (user == null || !RequestStateManager.CanManageRequests(user)) return;

                // If UserId is set (from My Team navigation), we filter for that specific user.
                // Otherwise we use the UI filters.
                var filterUserId = UserId > 0 ? UserId : (int?)null;
                var filterDept = UserId > 0 ? null : (SelectedDepartment?.Name == "All" ? null : SelectedDepartment?.Name);

                var result = await _requestService.GetRequestsForApprovalAsync(
                    CurrentPage, PageSize,
                    SelectedStatus == "All" ? null : SelectedStatus,
                    SelectedType == "All" ? null : SelectedType,
                    FromDate, ToDate,
                    filterDept,
                    filterUserId
                );

                // If result is null or empty but we have a userId, we might need a different approach
                // but GetRequestsForApprovalAsync should handle it if we pass the pagination correctly.
                // However, IRequestService.GetRequestsForApprovalAsync doesn't take userId.
                // Let's fix that.

                if (result?.Data?.Items != null)
                {
                    Requests.Clear();
                    var filtered = result.Data.Items.Where(r => RequestStateManager.CanViewRequest(r, user)).ToList();
                    foreach (var req in filtered) Requests.Add(req);

                    TotalItems = result.Data.TotalCount;
                    TotalPages = (int)Math.Ceiling((double)TotalItems / PageSize);
                    HasRequests = Requests.Any();

                    UpdatePaginationMetadata();

                    PendingCount = filtered.Count(r => r.Status == RequestStatus.Pending);
                    ApprovedCount = filtered.Count(r => r.Status == RequestStatus.ManagerApproved || r.Status == RequestStatus.HRApproved);
                    RejectedCount = filtered.Count(r => r.Status == RequestStatus.Rejected || r.Status == RequestStatus.ManagerRejected);
                }
            }
            catch (Exception ex) { _logger.LogError(ex, "Error loading requests"); }
            finally { IsBusy = false; }
        }

        private void UpdatePaginationMetadata()
        {
            CanGoToFirstPage = CurrentPage > 1;
            CanGoToPreviousPage = CurrentPage > 1;
            CanGoToNextPage = CurrentPage < TotalPages;
            CanGoToLastPage = CurrentPage < TotalPages;

            PageNumbers.Clear();
            for (int i = 1; i <= TotalPages; i++) PageNumbers.Add(i);

            OnPropertyChanged(nameof(PageInfo));
        }

        [RelayCommand]
        private async Task FirstPageAsync() { CurrentPage = 1; await LoadRequestsAsync(); }

        [RelayCommand]
        private async Task PreviousPageAsync() { if (CurrentPage > 1) { CurrentPage--; await LoadRequestsAsync(); } }

        [RelayCommand]
        private async Task NextPageAsync() { if (CurrentPage < TotalPages) { CurrentPage++; await LoadRequestsAsync(); } }

        [RelayCommand]
        private async Task LastPageAsync() { CurrentPage = TotalPages; await LoadRequestsAsync(); }

        [RelayCommand]
        private async Task GoToPageAsync(int page) { CurrentPage = page; await LoadRequestsAsync(); }

        [RelayCommand]
        private async Task ViewRequestAsync(int requestId)
        {
            await Shell.Current.GoToAsync($"RequestDetailsPage?RequestId={requestId}");
        }

        [RelayCommand]
        private async Task ApproveRequestAsync(int requestId)
        {
            var req = Requests.FirstOrDefault(r => r.RequestID == requestId);
            if (req == null) return;

            string remarks = await Shell.Current.DisplayPromptAsync("Approve", "Remarks (optional):");
            if (remarks == null) return;

            IsBusy = true;
            try
            {
                var user = _userSessionService.CurrentUser;
                ApiResponse<RequestResponseDto>? result = null;
                if (user?.IsManager == true) result = await _requestService.ManagerApproveRequestAsync(requestId, new ManagerApprovalDto { ManagerRemarks = remarks });
                else if (user?.IsHR == true || user?.IsAdmin == true) result = await _requestService.HRApproveRequestAsync(requestId, new HRApprovalDto { HRRemarks = remarks });

                if (result?.Success == true) await LoadRequestsAsync();
            }
            catch (Exception ex) { _logger.LogError(ex, "Approval failed"); }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        private async Task RejectRequestAsync(int requestId)
        {
            var req = Requests.FirstOrDefault(r => r.RequestID == requestId);
            if (req == null) return;

            string reason = await Shell.Current.DisplayPromptAsync("Reject", "Reason (required):");
            if (string.IsNullOrWhiteSpace(reason)) return;

            IsBusy = true;
            try
            {
                var user = _userSessionService.CurrentUser;
                ApiResponse<RequestResponseDto>? result = null;
                if (user?.IsManager == true) result = await _requestService.ManagerRejectRequestAsync(requestId, new ManagerRejectDto { ManagerRemarks = reason });
                else if (user?.IsHR == true || user?.IsAdmin == true) result = await _requestService.HRRejectRequestAsync(requestId, new HRRejectDto { HRRemarks = reason });

                if (result?.Success == true) await LoadRequestsAsync();
            }
            catch (Exception ex) { _logger.LogError(ex, "Rejection failed"); }
            finally { IsBusy = false; }
        }

        public void Dispose() => _cts.Dispose();
    }
}
