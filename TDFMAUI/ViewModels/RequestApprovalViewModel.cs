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

namespace TDFMAUI.ViewModels
{
    public partial class RequestApprovalViewModel : BaseViewModel, INotifyPropertyChanged
    {
        private readonly ApiService _apiService;
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

        public event PropertyChangedEventHandler PropertyChanged;

        public RequestApprovalViewModel()
        {
            Title = "Request Approval";
            _apiService = App.Current.Handler.MauiContext?.Services.GetService<ApiService>();
            _notificationService = App.Current.Handler.MauiContext?.Services.GetService<INotificationService>();

            InitializeCommands();
            LoadRequestsAsync().ConfigureAwait(false);
        }

        public RequestApprovalViewModel(ApiService apiService, INotificationService notificationService)
        {
            Title = "Request Approval";
            _apiService = apiService;
            _notificationService = notificationService;
            Requests = new ObservableCollection<RequestResponseDto>();

            InitializeCommands();
            LoadRequestsAsync().ConfigureAwait(false);
        }

        private void InitializeCommands()
        {
            ApproveCommand = new Command<int>(async (id) => await ApproveRequestAsync(id));
            RejectCommand = new Command<int>(async (id) => await RejectRequestAsync(id));
            ViewCommand = new Command<int>(async (id) => await ViewRequestAsync(id));
            FilterCommand = new Command(async () => await LoadRequestsAsync());
            RefreshCommand = new Command(async () => await LoadRequestsAsync());
        }

        [RelayCommand]
        public async Task LoadRequestsAsync()
        {
            if (IsLoading) return;
            IsLoading = true;
            if (Requests == null)
            {
                Requests = new ObservableCollection<RequestResponseDto>();
            }
            Requests.Clear();

            try
            {
                var requestPagination = new RequestPaginationDto
                {
                    Page = 1,
                    PageSize = 20,
                    FromDate = FromDate,
                    ToDate = ToDate,
                    FilterStatus = SelectedStatus,
                    FilterType = SelectedType
                };

                string department = SelectedDepartment == "All" ? null : SelectedDepartment;
                var requests = await _apiService.GetRequestsAsync(requestPagination, null, department);

                if (requests?.Items != null)
                {
                    foreach (var request in requests.Items)
                    {
                        Requests.Add(request);
                    }

                    PendingCount = requests.Items.Count(r => r.Status == "Pending");
                    ApprovedCount = requests.Items.Count(r => r.Status == "Approved");
                    RejectedCount = requests.Items.Count(r => r.Status == "Rejected");
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
            try
            {
                IsLoading = true;
                var guidRequestId = new Guid(requestId.ToString("D").PadLeft(32, '0'));
                await _apiService.ApproveRequestAsync(guidRequestId, new RequestApprovalDto 
                { 
                    Status = "Approved",
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
            try
            {
                IsLoading = true;
                var guidRequestId = new Guid(requestId.ToString("D").PadLeft(32, '0'));
                await _apiService.RejectRequestAsync(guidRequestId, new RequestRejectDto 
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
            if (requestId <= 0) return;
            
            try
            {
                // Properly handle Guid conversion by using the request ID to construct a properly formatted Guid
                var guidRequestId = new Guid(requestId.ToString("D").PadLeft(32, '0'));
                var request = await _apiService.GetRequestByIdAsync(guidRequestId);
                if (request == null)
                {
                    await Shell.Current.DisplayAlert("Error", "Could not find request details.", "OK");
                    return;
                }
                
                // Navigate via Shell rather than direct navigation
                await Shell.Current.GoToAsync($"TDFMAUI/Features/Requests/RequestDetailsPage", new Dictionary<string, object>
                {
                    {"RequestId", request.Id}
                });
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", $"Failed to load request details: {ex.Message}", "OK");
            }
        }

        public List<string> StatusOptions => new List<string> { "All", "Pending", "Approved", "Rejected" };
        public List<string> TypeOptions => new List<string> { "All", "Vacation", "Sick Leave", "Personal", "Other" };
        public List<string> DepartmentOptions => new List<string> { "All", "HR", "IT", "Finance", "Marketing", "Operations" };

        /// <summary>
        /// Filters the Requests collection based on specified criteria
        /// </summary>
        public void FilterRequests(string status, string type, string department, DateTime? fromDate, DateTime? toDate)
        {
            if (IsLoading) return;

            IsLoading = true;
            try
            {
                // Start with all requests or fetch them if needed
                var allRequests = new List<RequestResponseDto>(Requests);

                // Apply status filter
                if (!string.IsNullOrEmpty(status) && !status.Equals("All", StringComparison.OrdinalIgnoreCase))
                {
                    allRequests = allRequests.Where(r => r.Status?.Equals(status, StringComparison.OrdinalIgnoreCase) == true).ToList();
                }

                // Apply type filter
                if (!string.IsNullOrEmpty(type) && !type.Equals("All", StringComparison.OrdinalIgnoreCase))
                {
                    allRequests = allRequests.Where(r => r.LeaveType?.Equals(type, StringComparison.OrdinalIgnoreCase) == true).ToList();
                }

                // Apply department filter
                if (!string.IsNullOrEmpty(department) && !department.Equals("All", StringComparison.OrdinalIgnoreCase))
                {
                    // Note: This assumes requests have a department property or similar
                    // You may need to adjust based on your actual data model
                    allRequests = allRequests.Where(r => r.RequestDepartment?.Equals(department, StringComparison.OrdinalIgnoreCase) == true).ToList();
                }

                // Apply date filters
                if (fromDate.HasValue)
                {
                    allRequests = allRequests.Where(r => r.RequestStartDate >= fromDate.Value.Date).ToList();
                }

                if (toDate.HasValue)
                {
                    allRequests = allRequests.Where(r => r.RequestStartDate <= toDate.Value.Date.AddDays(1).AddTicks(-1)).ToList();
                }

                // Update the Requests collection
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

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
} 