using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Windows.Input;
using TDFMAUI.Services;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TDFShared.DTOs.Requests;
using System.ComponentModel;
using TDFShared.Enums;

namespace TDFMAUI.ViewModels
{
    public static class RequestOptions
    {
        public static readonly List<string> StatusOptions = new List<string> { "All" }.Concat(Enum.GetNames(typeof(RequestStatus))).ToList();
        public static readonly List<string> TypeOptions = new List<string> { "All" }.Concat(Enum.GetNames(typeof(LeaveType))).ToList();
        public static readonly List<string> DepartmentOptions = new() { "All", "HR", "IT", "Finance", "Marketing", "Operations" };
    }

    public partial class RequestApprovalViewModel : BaseViewModel, INotifyPropertyChanged
    {
        private readonly IRequestService _requestService;
        private readonly INotificationService _notificationService;

        [ObservableProperty]
        private ObservableCollection<RequestResponseDto> _requests;

        [ObservableProperty]
        private RequestResponseDto _selectedRequest;

        [ObservableProperty]
        ObservableCollection<RequestResponseDto> pendingRequests;

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private int _pendingCount;
        public int PendingCount
        {
            get => _pendingCount;
            set => SetProperty(ref _pendingCount, value);
        }

        private int _approvedCount;
        public int ApprovedCount
        {
            get => _approvedCount;
            set => SetProperty(ref _approvedCount, value);
        }

        private int _rejectedCount;
        public int RejectedCount
        {
            get => _rejectedCount;
            set => SetProperty(ref _rejectedCount, value);
        }

        private DateTime _fromDate = DateTime.Now.AddDays(-30);
        public DateTime FromDate
        {
            get => _fromDate;
            set => SetProperty(ref _fromDate, value);
        }

        private DateTime _toDate = DateTime.Now;
        public DateTime ToDate
        {
            get => _toDate;
            set => SetProperty(ref _toDate, value);
        }

        private string _selectedStatus = "All";
        public string SelectedStatus
        {
            get => _selectedStatus;
            set => SetProperty(ref _selectedStatus, value);
        }

        private string _selectedType = "All";
        public string SelectedType
        {
            get => _selectedType;
            set => SetProperty(ref _selectedType, value);
        }

        private string _selectedDepartment = "All";
        public string SelectedDepartment
        {
            get => _selectedDepartment;
            set => SetProperty(ref _selectedDepartment, value);
        }

        public bool IsHR => App.CurrentUser?.Department?.Contains("HR") == true ||
                          App.CurrentUser?.Title?.Contains("HR") == true;

        public ICommand ApproveCommand { get; private set; }
        public ICommand RejectCommand { get; private set; }
        public ICommand ViewCommand { get; private set; }
        public ICommand FilterCommand { get; private set; }
        public ICommand RefreshCommand { get; private set; }

        public new event PropertyChangedEventHandler PropertyChanged;

        public List<string> StatusOptions => RequestOptions.StatusOptions;
        public List<string> TypeOptions => RequestOptions.TypeOptions;
        public List<string> DepartmentOptions => RequestOptions.DepartmentOptions;

        public RequestApprovalViewModel(IRequestService requestService, INotificationService notificationService)
        {
            Title = "Request Approval";
            _requestService = requestService;
            _notificationService = notificationService;
            Requests = new ObservableCollection<RequestResponseDto>();

            InitializeCommands();
            LoadRequestsAsync().ConfigureAwait(false);
        }

        // --- Centralized Validation and Command Enablement ---
        private bool CanApprove(RequestResponseDto request) => request != null && request.Status == RequestStatus.Pending;
        private bool CanReject(RequestResponseDto request) => request != null && request.Status == RequestStatus.Pending;
        private bool CanView(RequestResponseDto request) => request != null;

        // DRY up command enablement for int ID commands
        public bool CanExecuteApprove(int id) => Requests?.FirstOrDefault(r => r.RequestID == id) is RequestResponseDto req && CanApprove(req);
        public bool CanExecuteReject(int id) => Requests?.FirstOrDefault(r => r.RequestID == id) is RequestResponseDto req && CanReject(req);
        public bool CanExecuteView(int id) => Requests?.FirstOrDefault(r => r.RequestID == id) is RequestResponseDto req && CanView(req);

        // Robust client-side validation for leave requests
        private List<string> ValidateRequest(RequestResponseDto request)
        {
            var errors = new List<string>();
            if (request == null) { errors.Add("Request is null."); return errors; }
            // Enum 'LeaveType' cannot be null or whitespace. If a specific 'None' or 'Undefined' value exists, check against that.
            // For now, assuming the enum type itself makes it 'required' in a sense.
            if (request.RequestStartDate == default) errors.Add("Start date is required.");
            if (request.LeaveType == LeaveType.Permission) {
                if (!request.RequestBeginningTime.HasValue || !request.RequestEndingTime.HasValue)
                    errors.Add("Both beginning and ending times are required for Permission leave.");
                if (request.RequestEndDate.HasValue && request.RequestEndDate.Value.Date != request.RequestStartDate.Date)
                    errors.Add("Permission leave must start and end on the same day.");
                if (request.RequestEndingTime <= request.RequestBeginningTime)
                    errors.Add("Ending time must be after beginning time for Permission leave.");
            }
            if (request.LeaveType == LeaveType.WorkFromHome) {
                if (request.RequestBeginningTime.HasValue || request.RequestEndingTime.HasValue)
                    errors.Add("Work From Home leave cannot have specific times; only full days are allowed.");
            }
            if (request.RequestEndDate.HasValue && request.RequestEndDate.Value < request.RequestStartDate)
                errors.Add("End date cannot be before start date.");
            return errors;
        }

        private void InitializeCommands()
        {
            ApproveCommand = new Command<int>(async (id) => await ApproveRequestAsync(id), CanExecuteApprove);
            RejectCommand = new Command<int>(async (id) => await RejectRequestAsync(id), CanExecuteReject);
            ViewCommand = new Command<int>(async (id) => await ViewRequestAsync(id), CanExecuteView);
            FilterCommand = new Command(async () => await LoadRequestsAsync());
            RefreshCommand = new Command(async () => await LoadRequestsAsync());
        }

        [RelayCommand]
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
                    FilterStatus = (string.IsNullOrEmpty(SelectedStatus) || SelectedStatus == "All") ? (RequestStatus?)null : Enum.TryParse<RequestStatus>(SelectedStatus, true, out var parsedStatus) ? parsedStatus : (RequestStatus?)null,
                    FilterType = (string.IsNullOrEmpty(SelectedType) || SelectedType == "All") ? (LeaveType?)null : Enum.TryParse<LeaveType>(SelectedType, true, out var parsedType) ? parsedType : (LeaveType?)null
                };

                string department = (string.IsNullOrEmpty(SelectedDepartment) || SelectedDepartment == "All") ? null : SelectedDepartment;
                var requests = await _requestService.GetRequestsByDepartmentAsync(department, requestPagination);

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

        private async Task ApproveRequestAsync(int requestId)
        {
            var request = Requests.FirstOrDefault(r => r.RequestID == requestId);
            var errors = ValidateRequest(request);
            if (errors.Any())
            {
                await _notificationService.ShowErrorAsync(string.Join("\n", errors));
                return;
            }

            try
            {
                IsLoading = true;
                await _requestService.ApproveRequestAsync(requestId, new RequestApprovalDto
                {
                    Status = RequestStatus.Approved,
                    Comment = "Approved by manager"
                });
                await _notificationService.ShowSuccessAsync("Request approved successfully");
                await LoadRequestsAsync();
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
            var errors = ValidateRequest(request);
            if (errors.Any())
            {
                await _notificationService.ShowErrorAsync(string.Join("\n", errors));
                return;
            }

            try
            {
                IsLoading = true;
                await _requestService.RejectRequestAsync(requestId, new RequestRejectDto
                {
                    RejectReason = "Rejected by manager"
                });
                await _notificationService.ShowSuccessAsync("Request rejected successfully");
                await LoadRequestsAsync();
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

        private async Task ViewRequestAsync(int requestId)
        {
            var request = Requests.FirstOrDefault(r => r.RequestID == requestId);
            var errors = ValidateRequest(request);
            if (errors.Any())
            {
                await Shell.Current.DisplayAlert("Validation Error", string.Join("\n", errors), "OK");
                return;
            }

            if (requestId <= 0) return;

            try
            {
                var requestDetails = await _requestService.GetRequestByIdAsync(requestId);
                if (requestDetails == null)
                {
                    await Shell.Current.DisplayAlert("Error", "Could not find request details.", "OK");
                    return;
                }

                await Shell.Current.GoToAsync($"TDFMAUI/Features/Requests/RequestDetailsPage", new Dictionary<string, object>
                {
                    {"RequestId", requestDetails.RequestID}
                });
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", $"Failed to load request details: {ex.Message}", "OK");
            }
        }

        public void FilterRequests(string status, string type, string department, DateTime? fromDate, DateTime? toDate)
        {
            if (IsLoading) return;

            IsLoading = true;
            try
            {
                var allRequests = new List<RequestResponseDto>(Requests);

                if (!string.IsNullOrEmpty(status) && !status.Equals("All", StringComparison.OrdinalIgnoreCase))
                {
                    allRequests = allRequests.Where(r => r.Status.ToString().Equals(status, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                if (!string.IsNullOrEmpty(type) && !type.Equals("All", StringComparison.OrdinalIgnoreCase))
                {
                    allRequests = allRequests.Where(r => r.LeaveType.ToString().Equals(type, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                if (!string.IsNullOrEmpty(department) && !department.Equals("All", StringComparison.OrdinalIgnoreCase))
                {
                    allRequests = allRequests.Where(r => r.RequestDepartment?.Equals(department, StringComparison.OrdinalIgnoreCase) == true).ToList();
                }

                if (fromDate.HasValue)
                {
                    allRequests = allRequests.Where(r => r.RequestStartDate >= fromDate.Value.Date).ToList();
                }

                if (toDate.HasValue)
                {
                    allRequests = allRequests.Where(r => r.RequestStartDate <= toDate.Value.Date.AddDays(1).AddTicks(-1)).ToList();
                }

                Requests.Clear();
                foreach (var request in allRequests)
                {
                    Requests.Add(request);
                }

                OnPropertyChanged(nameof(Requests));
            }
            finally
            {
                IsLoading = false;
            }
        }

        protected new void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}