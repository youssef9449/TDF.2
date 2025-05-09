using System.Collections.ObjectModel;
using System.Windows.Input;
using TDFMAUI.Helpers;
using TDFMAUI.Services;
using Microsoft.Maui.Controls;
using TDFMAUI.ViewModels;
using System;
using System.Globalization;
using System.Linq;
using TDFShared.DTOs.Requests;
using TDFMAUI.Features.Requests;

namespace TDFMAUI.Pages
{
    // Status color converter class that uses GetStatusColor method
    public class StatusColorConverter : IValueConverter
    {
        private readonly RequestApprovalPage _page;
        
        public StatusColorConverter(RequestApprovalPage page)
        {
            _page = page;
        }
        
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string status)
            {
                return _page.GetStatusColor(status);
            }
            return Colors.Gray;
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("Converting Color back to Status string is not supported.");
        }
    }

    // Status text color converter class that uses GetStatusTextColor method
    public class StatusTextColorConverter : IValueConverter
    {
        private readonly RequestApprovalPage _page;
        
        public StatusTextColorConverter(RequestApprovalPage page)
        {
            _page = page;
        }
        
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string status)
            {
                return _page.GetStatusTextColor(status);
            }
            return Colors.Black;
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("Converting Color back to Status string is not supported.");
        }
    }

    public partial class RequestApprovalPage : ContentPage
    {
        // Added for XAML event handler fix
        private void OnToggleFiltersClicked(object sender, EventArgs e)
        {
            ToggleFiltersPanelVisibility();
        }

        private readonly INotificationService _notificationService;
        private readonly ApiService _apiService;
        
        // View model properties
        public List<string> StatusOptions { get; set; } = new List<string> { "All", "Pending", "Approved", "Rejected" };
        public List<string> TypeOptions { get; set; } = new List<string>();
        public List<string> DepartmentOptions { get; set; } = new List<string>();
        
        public string SelectedStatus { get; set; } = "Pending";
        public string SelectedType { get; set; } = "All";
        public string SelectedDepartment { get; set; } = "All";
        
        public bool IsManager { get; set; }
        public bool IsHR { get; set; }
        public string UserDepartment { get; set; }
        
        // Statistics for desktop view
        public int PendingCount { get; set; }
        public int ApprovedCount { get; set; }
        public int RejectedCount { get; set; }
        
        // Commands for the UI
        public ICommand ApproveCommand { get; private set; }
        public ICommand RejectCommand { get; private set; }
        public ICommand ViewCommand { get; private set; }
        
        // Date range for filtering
        private DateTime? _fromDate;
        private DateTime? _toDate;
        
        // Track if mobile filters panel is showing
        private bool _isFiltersPanelVisible = false;
        
        private RequestApprovalViewModel _viewModel;
        
        public RequestApprovalPage(
            INotificationService notificationService, 
            ApiService apiService)
        {
            InitializeComponent();
            
            _notificationService = notificationService;
            _apiService = apiService;
            
            // Add the StatusColorConverter as a resource
            this.Resources.Add("StatusColorConverter", new StatusColorConverter(this));
            
            // Add the StatusTextColorConverter as a resource
            this.Resources.Add("StatusTextColorConverter", new StatusTextColorConverter(this));
            
            // Set up commands
            ApproveCommand = new Command<int>(ApproveRequest);
            RejectCommand = new Command<int>(RejectRequest);
            ViewCommand = new Command<int>(ViewRequest);
            
            // Initialize request types
            TypeOptions = GetStaticRequestTypes();
            
            // Determine user role and permissions
            InitializeRoles();
            
            // Load departments if HR
            if (IsHR)
            {
                LoadDepartments();
            }
            
            // Default to user's department for managers
            if (IsManager && !IsHR && !string.IsNullOrEmpty(UserDepartment))
            {
                SelectedDepartment = UserDepartment;
            }
            
            // Set the binding context
            _viewModel = new RequestApprovalViewModel(_apiService, _notificationService);
            BindingContext = _viewModel;
            
            // Set up initial device-specific configurations
            ConfigureForCurrentDevice();
            
            // Subscribe to size change events for responsive design
            SizeChanged += OnPageSizeChanged;
            
            // Load requests after everything is set up
            LoadData();
        }
        
        private void ApplyPlatformSpecificUI()
        {
            bool isDesktop = DeviceHelper.IsDesktop;
            
            var desktopStatsView = this.FindByName<View>("DesktopStatsView");
            
            if (desktopStatsView != null)
                desktopStatsView.IsVisible = isDesktop;

            if (RequestsApprovalCollectionView == null) return;

            int optimalColumns = DeviceHelper.GetOptimalColumnCount();

            if (optimalColumns > 1 && (DeviceHelper.IsDesktop || DeviceHelper.DeviceIdiom == DeviceIdiom.Tablet))
            {
                RequestsApprovalCollectionView.ItemsLayout = new GridItemsLayout(optimalColumns, ItemsLayoutOrientation.Vertical)
                {
                    VerticalItemSpacing = 10,
                    HorizontalItemSpacing = 10
                };
                 RequestsApprovalCollectionView.EmptyView = "No requests match your criteria. Try adjusting your filters.";
            }
            else
            {
                RequestsApprovalCollectionView.ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical)
                {
                    ItemSpacing = 5
                };
                RequestsApprovalCollectionView.EmptyView = "No requests found.";
            }
        }
        
        private void InitializeRoles()
        {
            if (App.CurrentUser == null)
                return;
                
            // Use IsAdmin property instead of Role which doesn't exist
            IsManager = App.CurrentUser.IsAdmin;
            
            // Check if user is in HR department or has HR-related title
            IsHR = App.CurrentUser.Department?.Contains("HR", StringComparison.OrdinalIgnoreCase) == true || 
                   App.CurrentUser.Title?.Contains("HR", StringComparison.OrdinalIgnoreCase) == true;
                   
            UserDepartment = App.CurrentUser.Department;
            
            // Add default department option
            DepartmentOptions = new List<string> { "All" };
            
            if (!string.IsNullOrEmpty(UserDepartment))
            {
                if(!DepartmentOptions.Contains(UserDepartment, StringComparer.OrdinalIgnoreCase))
                {
                    DepartmentOptions.Add(UserDepartment);
                }
                SelectedDepartment = IsHR ? "All" : UserDepartment;
            }
        }
        
        private async void LoadDepartments()
        {
            try
            {
                var departmentLookupItems = await _apiService.GetDepartmentsAsync(); 
                if (departmentLookupItems != null)
                {
                    foreach (var item in departmentLookupItems)
                    {
                        if (!DepartmentOptions.Contains(item.Value, StringComparer.OrdinalIgnoreCase))
                        {
                            DepartmentOptions.Add(item.Value);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading departments: {ex.Message}");
                await DisplayAlert("Error", "Could not load departments.", "OK");
            }
        }
        
        private async void LoadData()
        {
            await _viewModel.LoadRequestsAsync();
            UpdateCounts();
        }
        
        private void UpdateCounts()
        {
             if (_viewModel.Requests != null)
            {
                PendingCount = _viewModel.Requests.Count(r => r.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase));
                ApprovedCount = _viewModel.Requests.Count(r => r.Status.Equals("Approved", StringComparison.OrdinalIgnoreCase));
                RejectedCount = _viewModel.Requests.Count(r => r.Status.Equals("Rejected", StringComparison.OrdinalIgnoreCase));
                OnPropertyChanged(nameof(PendingCount));
                OnPropertyChanged(nameof(ApprovedCount));
                OnPropertyChanged(nameof(RejectedCount));
            }
        }
        
        public Color GetStatusColor(string status)
        {
            if (string.IsNullOrEmpty(status)) return Colors.Gray;
            return status.ToLowerInvariant() switch
            {
                "pending" => Colors.Orange,
                "approved" => Colors.Green,
                "rejected" => Colors.Red,
                _ => Colors.Gray,
            };
        }
        
        public Color GetStatusTextColor(string status)
        {
             if (string.IsNullOrEmpty(status)) return Colors.Black;
            return status.ToLowerInvariant() switch
            {
                "pending" => Colors.DarkOrange,
                "approved" => Colors.DarkGreen,
                "rejected" => Colors.DarkRed,
                _ => Colors.Black,
            };
        }
        
        private void OnFilterChanged(object sender, EventArgs e)
        {
            if (_viewModel == null || _viewModel.Requests == null) return;
            
            // Direct filtering logic instead of calling ViewModel's FilterRequests
            var filteredRequests = _viewModel.Requests.ToList();
            
            // Apply status filter
            if (!string.IsNullOrEmpty(SelectedStatus) && !SelectedStatus.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                filteredRequests = filteredRequests.Where(r => r.Status?.Equals(SelectedStatus, StringComparison.OrdinalIgnoreCase) == true).ToList();
            }
            
            // Apply type filter
            if (!string.IsNullOrEmpty(SelectedType) && !SelectedType.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                filteredRequests = filteredRequests.Where(r => r.LeaveType?.Equals(SelectedType, StringComparison.OrdinalIgnoreCase) == true).ToList();
            }
            
            // Apply department filter
            if (!string.IsNullOrEmpty(SelectedDepartment) && !SelectedDepartment.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                filteredRequests = filteredRequests.Where(r => r.RequestDepartment?.Equals(SelectedDepartment, StringComparison.OrdinalIgnoreCase) == true).ToList();
            }
            
            // Apply date filters
            if (_fromDate.HasValue)
            {
                filteredRequests = filteredRequests.Where(r => r.RequestStartDate >= _fromDate.Value.Date).ToList();
            }
            
            if (_toDate.HasValue)
            {
                filteredRequests = filteredRequests.Where(r => r.RequestStartDate <= _toDate.Value.Date.AddDays(1).AddTicks(-1)).ToList();
            }
            
            // Update the collection
            _viewModel.Requests.Clear();
            foreach (var request in filteredRequests)
            {
                _viewModel.Requests.Add(request);
            }
            
            UpdateCounts();
        }
        
        private void OnApplyFiltersClicked(object sender, EventArgs e)
        {
            OnFilterChanged(this, EventArgs.Empty);
            if (DeviceHelper.UseCompactUI)
            {
                FiltersPanel.IsVisible = false;
            }
        }
        
        private void OnResetFiltersClicked(object sender, EventArgs e)
        {
            SelectedStatus = "Pending";
            SelectedType = "All";
            SelectedDepartment = (IsManager && !IsHR && !string.IsNullOrEmpty(UserDepartment)) ? UserDepartment : "All";
            _fromDate = null;
            _toDate = null;
            OnPropertyChanged(nameof(SelectedStatus));
            OnPropertyChanged(nameof(SelectedType));
            OnPropertyChanged(nameof(SelectedDepartment));
            OnPropertyChanged(nameof(FromDate)); 
            OnPropertyChanged(nameof(ToDate));   
            _viewModel.LoadRequestsAsync(); 
            UpdateCounts();
        }
        
        public DateTime? FromDate
        {
            get => _fromDate;
            set
            {
                _fromDate = value;
                OnPropertyChanged();
                OnFilterChanged(this, EventArgs.Empty);
            }
        }

        public DateTime? ToDate
        {
            get => _toDate;
            set
            {
                _toDate = value;
                OnPropertyChanged();
                OnFilterChanged(this, EventArgs.Empty);
            }
        }
        
        private void ToggleFiltersPanelVisibility()
        {
            _isFiltersPanelVisible = !_isFiltersPanelVisible;
            FiltersPanel.IsVisible = _isFiltersPanelVisible;
        }
        
        private async void OnRefreshClicked(object sender, EventArgs e)
        {
            await _viewModel.LoadRequestsAsync();
            UpdateCounts();
        }
        
        private async void OnRequestSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is RequestResponseDto selectedRequest)
            {
                _viewModel.SelectedRequest = selectedRequest;
                await Shell.Current.GoToAsync(nameof(RequestDetailsPage), new Dictionary<string, object>
                {
                    { "RequestId", selectedRequest.Id }
                });
            }
            ((CollectionView)sender).SelectedItem = null;
        }
        
        private async void ApproveRequest(int requestId)
        {
            if (_apiService == null) return;
            
            try
            {
                // Convert int to Guid
                var guidRequestId = new Guid(requestId.ToString("D").PadLeft(32, '0'));
                
                var result = await _apiService.ApproveRequestAsync(guidRequestId, new RequestApprovalDto 
                { 
                    Status = "Approved",
                    Comment = "Approved by manager"
                });
                
                if (result)
                {
                    await _notificationService.ShowSuccessAsync("Request approved successfully");
                    await _viewModel.LoadRequestsAsync();
                    UpdateCounts();
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
        }
        
        private async void RejectRequest(int requestId)
        {
            string reason = await DisplayPromptAsync("Reject Request", "Enter reason for rejection:", maxLength: 200, keyboard: Keyboard.Text);
            if (reason == null) 
                return; 

            if (string.IsNullOrWhiteSpace(reason))
            {
                await DisplayAlert("Validation Error", "Rejection reason cannot be empty.", "OK");
                return;
            }
            
            if (_apiService == null) return;
            
            try
            {
                // Convert int to Guid
                var guidRequestId = new Guid(requestId.ToString("D").PadLeft(32, '0'));
                
                var result = await _apiService.RejectRequestAsync(guidRequestId, new RequestRejectDto 
                { 
                    RejectReason = reason
                });
                
                if (result)
                {
                    await _notificationService.ShowSuccessAsync("Request rejected successfully");
                    await _viewModel.LoadRequestsAsync();
                    UpdateCounts();
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
        }
        
        private async void ViewRequest(int requestId)
        {
            await Shell.Current.GoToAsync($"{nameof(RequestDetailsPage)}?RequestId={requestId}");
        }
        
        protected override void OnAppearing()
        {
            base.OnAppearing();
            ConfigureForCurrentDevice();
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            if (_viewModel.Requests == null || !_viewModel.Requests.Any())
            {
                 LoadData(); 
            }
            else
            {
                OnFilterChanged(this, EventArgs.Empty); 
            }
        }
        
        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RequestApprovalViewModel.Requests))
            {
                UpdateCounts();
            }
        }
        
        private void OnPageSizeChanged(object sender, EventArgs e)
        {
            ConfigureForCurrentDevice();
        }
        
        private void ConfigureForCurrentDevice()
        {
            bool isDesktop = DeviceHelper.IsDesktop;
            bool isMobile = !isDesktop && (DeviceHelper.IsAndroid || DeviceHelper.IsIOS);

            if (isDesktop)
            {
                VisualStateManager.GoToState(this, "Desktop");
                FiltersPanel.IsVisible = true; 
            }
            else if (isMobile) 
            {
                VisualStateManager.GoToState(this, "Mobile");
                FiltersPanel.IsVisible = _isFiltersPanelVisible; 
            }
            else 
            {
                VisualStateManager.GoToState(this, "DefaultView"); 
                FiltersPanel.IsVisible = true; 
            }
            ApplyPlatformSpecificUI();
        }

        // Added static method for request types (alternative to service call)
        private static List<string> GetStaticRequestTypes()
        {
            return new List<string>
            {
                "All",
                "Annual Leave",
                "Sick Leave",
                "Casual Leave",
                "Unpaid Leave",
                "Bereavement Leave",
                "Maternity Leave",
                "Paternity Leave",
                "Emergency Leave",
                "Permission",
                "Other"
            };
        }
    }
} 