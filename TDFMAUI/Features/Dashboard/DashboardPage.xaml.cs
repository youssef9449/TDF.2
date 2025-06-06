using Microsoft.Maui.Controls;
using TDFMAUI.Helpers;
using TDFMAUI.Services;
using TDFMAUI.Features.Dashboard;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using System;
using TDFShared.Services;

namespace TDFMAUI.Features.Dashboard
{
    public partial class DashboardPage : ContentPage
    {
        private DashboardViewModel _viewModel;
        
        public DashboardPage(DashboardViewModel viewModel = null)
        {
            InitializeComponent();
            
            // Use the provided ViewModel or resolve from DI
            if (viewModel != null)
            {
                _viewModel = viewModel;
            }
            else
            {
                _viewModel = IPlatformApplication.Current?.Services.GetService<DashboardViewModel>();
                
                // If still null, create a new instance with required services
                if (_viewModel == null)
                {
                    var requestService = IPlatformApplication.Current?.Services.GetService<IRequestService>();
                    var notificationService = IPlatformApplication.Current?.Services.GetService<TDFMAUI.Services.INotificationService>();
                    var logger = IPlatformApplication.Current?.Services.GetService<Microsoft.Extensions.Logging.ILogger<DashboardViewModel>>();
                    var authService = IPlatformApplication.Current?.Services.GetService<IAuthService>();
                    
                    if (requestService != null && notificationService != null && authService != null)
                    {
                        _viewModel = new DashboardViewModel(
                            requestService, 
                            notificationService, 
                            logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DashboardViewModel>.Instance,
                            authService);
                    }
                    else
                    {
                        // Show error if required services are not available
                        DisplayAlert("Error", "Required services are not available", "OK");
                    }
                }
            }
            
            // Set the BindingContext
            BindingContext = _viewModel;
            
            // Register for size changes to update layout if needed
            SizeChanged += OnPageSizeChanged;
        }
        
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            
            // If not authenticated, redirect to login page
            if (App.CurrentUser == null)
            {
                await Shell.Current.GoToAsync("LoginPage");
                return;
            }
            
            // Refresh data when the page appears
            if (_viewModel != null)
            {
                Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(() => _viewModel.RefreshCommand.Execute(null));
            }
        }
        
        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            
            // Clean up event subscriptions when page disappears
            if (_viewModel != null)
            {
                _viewModel.Cleanup();
            }
        }
        
        private void OnPageSizeChanged(object sender, EventArgs e)
        {
            // Handle any additional layout adjustments needed when size/orientation changes
            ConfigureForCurrentDevice();
        }
        
        private void ConfigureForCurrentDevice()
        {
            // Apply any additional platform-specific adjustments beyond what XAML can handle
            
            if (DeviceHelper.IsDesktop)
            {
                // Additional desktop-specific adjustments
                if (DeviceHelper.IsLargeScreen)
                {
                    // For large desktop screens, possibly adjust layout properties
                    Padding = new Thickness(20);
                }
                else
                {
                    // For smaller desktop screens
                    Padding = new Thickness(10);
                }
            }
            else if (DeviceHelper.IsMobile)
            {
                // Additional mobile-specific adjustments
                if (DeviceHelper.IsPortrait)
                {
                    // Portrait adjustments
                    Padding = new Thickness(10);
                }
                else
                {
                    // Landscape adjustments
                    Padding = new Thickness(20, 10);
                }
            }
        }
    }
} 