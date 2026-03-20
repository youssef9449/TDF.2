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
    public partial class RequestApprovalViewModel : BaseViewModel, IDisposable
    {
        private readonly IRequestService _requestService;
        private readonly Services.INotificationService _notificationService;
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

        public List<string> StatusOptions => RequestOptions.StatusOptions;
        public List<string> TypeOptions => RequestOptions.TypeOptions;

        public RequestApprovalViewModel(
            IRequestService requestService,
            Services.INotificationService notificationService,
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
                var user = _userSessionService.CurrentUser;
                var departmentsList = allDepartments.ToList();
                departmentsList.Insert(0, new LookupItem { Name = "All", Id = "0" });
                Departments = new ObservableCollection<LookupItem>(departmentsList);
                SelectedDepartment = Departments[0];
            }
            catch (Exception ex) { _logger.LogError(ex, "Error loading departments"); }
        }

        [RelayCommand]
        private async Task LoadRequestsAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                var user = _userSessionService.CurrentUser;
                if (user == null || !RequestStateManager.CanManageRequests(user)) return;

                var result = await _requestService.GetRequestsForApprovalAsync(
                    1, 50,
                    SelectedStatus == "All" ? null : SelectedStatus,
                    SelectedType == "All" ? null : SelectedType,
                    FromDate, ToDate,
                    SelectedDepartment?.Name == "All" ? null : SelectedDepartment?.Name
                );

                if (result?.Data?.Items != null)
                {
                    Requests.Clear();
                    var filtered = result.Data.Items.Where(r => RequestStateManager.CanViewRequest(r, user)).ToList();
                    foreach (var req in filtered) Requests.Add(req);

                    PendingCount = filtered.Count(r => r.Status == RequestStatus.Pending);
                    ApprovedCount = filtered.Count(r => r.Status == RequestStatus.ManagerApproved || r.Status == RequestStatus.HRApproved);
                    RejectedCount = filtered.Count(r => r.Status == RequestStatus.Rejected || r.Status == RequestStatus.ManagerRejected);
                }
            }
            catch (Exception ex) { _logger.LogError(ex, "Error loading requests"); }
            finally { IsBusy = false; }
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
