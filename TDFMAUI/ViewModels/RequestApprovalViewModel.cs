using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Windows.Input;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Maui.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TDFShared.DTOs.Requests;
using TDFShared.Services;
using TDFShared.Enums;
using TDFMAUI.Services;

namespace TDFMAUI.ViewModels
{
    public static class RequestOptions
    {
        public static readonly List<string> StatusOptions = new List<string> { "All" }.Concat(Enum.GetNames(typeof(RequestStatus))).ToList();
        public static readonly List<string> TypeOptions = new List<string> { "All" }.Concat(Enum.GetNames(typeof(LeaveType))).ToList();
        public static readonly List<string> DepartmentOptions = new() { "All", "HR", "IT", "Finance", "Marketing", "Operations" };
    }

    public partial class RequestApprovalViewModel : ObservableObject
    {
        private readonly IRequestService _requestService;
        private readonly INotificationService _notificationService;
        private readonly IAuthService _authService;

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

        public ICommand ApproveCommand { get; private set; }
        public ICommand RejectCommand { get; private set; }
        public ICommand ViewCommand { get; private set; }
        public ICommand FilterCommand { get; private set; }
        public ICommand RefreshCommand { get; private set; }

        public List<string> StatusOptions => RequestOptions.StatusOptions;
        public List<string> TypeOptions => RequestOptions.TypeOptions;
        public List<string> DepartmentOptions => RequestOptions.DepartmentOptions;

        public bool CanManageRequests => RequestAuthorizationService.CanManageRequests(IsAdmin, IsManager, IsHR);
        public bool CanEditDeleteAny => RequestAuthorizationService.CanEditDeleteAny(IsAdmin);
        public bool CanFilterByDepartment => RequestAuthorizationService.CanFilterByDepartment(IsManager);

        public RequestApprovalViewModel(IRequestService requestService, INotificationService notificationService, IAuthService authService)
        {
            _title = "Request Approval";
            _requestService = requestService;
            _notificationService = notificationService;
            _authService = authService;
            Requests = new ObservableCollection<RequestResponseDto>();
            PendingRequests = new ObservableCollection<RequestResponseDto>();

            InitializeCommands();
            LoadUserRolesAsync().ConfigureAwait(false);
            LoadRequestsAsync().ConfigureAwait(false);
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
                    OnPropertyChanged(nameof(CanManageRequests));
                    OnPropertyChanged(nameof(CanEditDeleteAny));
                    OnPropertyChanged(nameof(CanFilterByDepartment));
                }
            }
            catch (Exception ex)
            {
                await _notificationService.ShowErrorAsync($"Error loading user roles: {ex.Message}");
            }
        }

        private void InitializeCommands()
        {
            ApproveCommand = new Command<int>(async (id) => await ApproveRequestAsync(id), CanExecuteApprove);
            RejectCommand = new Command<int>(async (id) => await RejectRequestAsync(id), CanExecuteReject);
            ViewCommand = new Command<int>(async (id) => await ViewRequestAsync(id));
            FilterCommand = new Command(async () => await LoadRequestsAsync());
            RefreshCommand = new Command(async () => await LoadRequestsAsync());
        }

        // Authorization checks using RequestAuthorizationService
        private bool CanApprove(RequestResponseDto request) 
        {
            if (request == null) return false;
            if (request.Status != RequestStatus.Pending) return false;

            var canManage = RequestAuthorizationService.CanManageRequests(IsAdmin, IsManager, IsHR);
            if (!canManage) return false;

            // Additional check for managers - can only approve requests from their department
            if (IsManager == true && IsAdmin != true && IsHR != true)
            {
                var currentUserDepartment = Task.Run(async () => await _authService.GetCurrentUserDepartmentAsync()).Result;
                return request.RequestDepartment == currentUserDepartment;
            }

            return true;
        }

        private bool CanReject(RequestResponseDto request) => CanApprove(request); // Same logic as approve

        public bool CanExecuteApprove(int id) => 
            Requests?.FirstOrDefault(r => r.RequestID == id) is RequestResponseDto req && CanApprove(req);

        public bool CanExecuteReject(int id) => 
            Requests?.FirstOrDefault(r => r.RequestID == id) is RequestResponseDto req && CanReject(req);

        partial void OnRequestsChanged(ObservableCollection<RequestResponseDto> value)
        {
            // Update command can-execute state when requests change
            (ApproveCommand as Command)?.ChangeCanExecute();
            (RejectCommand as Command)?.ChangeCanExecute();
        }

        private async Task ApproveRequestAsync(int requestId)
        {
            var request = Requests.FirstOrDefault(r => r.RequestID == requestId);
            if (request == null) return;

            try
            {
                IsLoading = true;
                var approvalDto = new RequestApprovalDto
                {
                    Status = RequestStatus.Approved,
                    Comment = "Approved via approval page"
                };
                
                bool success = await _requestService.ApproveRequestAsync(requestId, approvalDto);
                if (success)
                {
                    await _notificationService.ShowSuccessAsync("Request approved successfully");
                    await LoadRequestsAsync();
                }
                else
                {
                    await _notificationService.ShowErrorAsync("Failed to approve request");
                }
            }
            catch (Exception ex)
            {
                await _notificationService.ShowErrorAsync($"Error approving request: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task RejectRequestAsync(int requestId)
        {
            var request = Requests.FirstOrDefault(r => r.RequestID == requestId);
            if (request == null) return;

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
                    await _notificationService.ShowErrorAsync("A rejection reason is required");
                return;
            }

            try
            {
                IsLoading = true;
                var rejectDto = new RequestRejectDto { RejectReason = reason };
                
                bool success = await _requestService.RejectRequestAsync(requestId, rejectDto);
                if (success)
                {
                    await _notificationService.ShowSuccessAsync("Request rejected successfully");
                    await LoadRequestsAsync();
                }
                else
                {
                    await _notificationService.ShowErrorAsync("Failed to reject request");
                }
            }
            catch (Exception ex)
            {
                await _notificationService.ShowErrorAsync($"Error rejecting request: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task LoadRequestsAsync()
        {
            if (IsLoading) return;
            IsLoading = true;
            Requests.Clear();

            try
            {
                var requestPagination = new RequestPaginationDto
                {
                    Page = 1,
                    PageSize = 20,
                    FromDate = FromDate,
                    ToDate = ToDate,
                    FilterStatus = (string.IsNullOrEmpty(SelectedStatus) || SelectedStatus == "All") 
                        ? (RequestStatus?)null 
                        : Enum.TryParse<RequestStatus>(SelectedStatus, true, out var parsedStatus) ? parsedStatus : (RequestStatus?)null,
                    FilterType = (string.IsNullOrEmpty(SelectedType) || SelectedType == "All") 
                        ? (LeaveType?)null 
                        : Enum.TryParse<LeaveType>(SelectedType, true, out var parsedType) ? parsedType : (LeaveType?)null
                };

                var currentUserDepartment = IsManager == true && IsAdmin != true && IsHR != true
                    ? await _authService.GetCurrentUserDepartmentAsync()
                    : null;

                var requests = currentUserDepartment != null
                    ? await _requestService.GetRequestsByDepartmentAsync(currentUserDepartment, requestPagination)
                    : await _requestService.GetAllRequestsAsync(requestPagination);

                if (requests?.Items != null)
                {
                    foreach (var request in requests.Items)
                    {
                        Requests.Add(request);
                    }

                    PendingCount = requests.Items.Count(r => r.Status == RequestStatus.Pending);
                    ApprovedCount = requests.Items.Count(r => r.Status == RequestStatus.Approved);
                    RejectedCount = requests.Items.Count(r => r.Status == RequestStatus.Rejected);
                }
            }
            catch (Exception ex)
            {
                await _notificationService.ShowErrorAsync($"Error loading requests: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ViewRequestAsync(int requestId)
        {
            var request = Requests.FirstOrDefault(r => r.RequestID == requestId);
            if (request == null) return;

            try
            {
                var requestDetails = await _requestService.GetRequestByIdAsync(requestId);
                if (requestDetails == null)
                {
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
                await _notificationService.ShowErrorAsync($"Error viewing request details: {ex.Message}");
            }
        }

        public void FilterRequests()
        {
            LoadRequestsAsync().ConfigureAwait(false);
        }
    }
}