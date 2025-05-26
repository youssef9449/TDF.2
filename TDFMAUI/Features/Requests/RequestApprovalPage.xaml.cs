using System.Collections.ObjectModel;
using System.Threading.Tasks;
using TDFMAUI.Helpers;
using TDFMAUI.Services;
using Microsoft.Maui.Controls;
using Microsoft.Extensions.Logging;
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
        private readonly INotificationService _notificationService;
        private readonly IRequestService _requestService;
        private readonly TDFShared.Services.IAuthService _authService;
        private RequestApprovalViewModel _viewModel;

        public RequestApprovalPage(
            INotificationService notificationService,
            IRequestService requestService,
            TDFShared.Services.IAuthService authService,
            ILogger<RequestApprovalViewModel> logger)
        {
            InitializeComponent();
            _notificationService = notificationService;
            _requestService = requestService;
            _authService = authService;

            // Add converters as resources
            this.Resources.Add("StatusColorConverter", new StatusColorConverter(this));
            this.Resources.Add("StatusTextColorConverter", new StatusTextColorConverter(this));

            // Set the binding context
            _viewModel = new RequestApprovalViewModel(_requestService, _notificationService, _authService, logger);
            BindingContext = _viewModel;

            // Device-specific config and events
            ConfigureForCurrentDevice();
            SizeChanged += OnPageSizeChanged;

            // Load requests after everything is set up
            LoadRequestsAsync();
        }

        private async void LoadRequestsAsync()
        {
            await _viewModel.LoadRequestsCommand.ExecuteAsync(null);
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
                FiltersPanel.IsVisible = false;
            }
            else
            {
                VisualStateManager.GoToState(this, "DefaultView");
                FiltersPanel.IsVisible = true;
            }
            ApplyPlatformSpecificUI();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            ConfigureForCurrentDevice();
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            if (_viewModel.Requests == null || !_viewModel.Requests.Any())
            {
                await _viewModel.LoadRequestsCommand.ExecuteAsync(null);
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
                // Update UI based on ViewModel changes
            }
        }

        private void OnToggleFiltersClicked(object sender, EventArgs e)
        {
            if (FiltersPanel != null)
            {
                FiltersPanel.IsVisible = !FiltersPanel.IsVisible;
            }
        }
    }
}