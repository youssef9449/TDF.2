using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using TDFMAUI.ViewModels;
using TDFMAUI.Helpers;
#if IOS || MACCATALYST
using UIKit;
#endif

namespace TDFMAUI.Features.Auth
{
    public partial class LoginPage : ContentPage
    {
        private readonly LoginPageViewModel _viewModel;
        private const double SMALL_SCREEN_PADDING = 12;
        private const double NORMAL_SCREEN_PADDING = 24;
        private const double LARGE_SCREEN_PADDING = 32;

        public LoginPage(LoginPageViewModel viewModel)
        {
            InitializeComponent();

            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            BindingContext = _viewModel;

            // Apply platform-specific styling using DeviceHelper
            ApplyPlatformSpecificStyles();
            
            // Subscribe to window maximization changes
            DeviceHelper.WindowMaximizationChanged += OnWindowMaximizationChanged;
        }
        
        private void OnWindowMaximizationChanged(object sender, bool isMaximized)
        {
            // Adjust UI when window is maximized
            if (isMaximized)
            {
                // Scale up for maximized window
                LogoImage.HeightRequest = 400;
            }
            else
            {
                // Default size for 1280x720
                LogoImage.HeightRequest = 150;
            }
        }

        private void ApplyPlatformSpecificStyles()
        {
            if (DeviceHelper.IsIOS || DeviceHelper.IsMacOS)
            {
#if IOS || MACCATALYST
                // iOS-specific handler customization
                Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("iOSEntryStyle", (handler, view) =>
                {
                    handler.PlatformView.BorderStyle = UITextBorderStyle.None;
                    handler.PlatformView.BackgroundColor = UIColor.Clear;
                    handler.PlatformView.TextColor = UIColor.Black; // Explicitly set text color to black
                });
#endif
            }

            // Apply responsive design based on screen size
            if (DeviceHelper.IsSmallScreen)
            {
                // Small screen adjustments - reduce padding for smaller screens
                var mainContentStack = FindMainContentStack();
                if (mainContentStack != null)
                {
                    mainContentStack.Padding = new Thickness(SMALL_SCREEN_PADDING, SMALL_SCREEN_PADDING);
                    mainContentStack.Spacing = 16;
                }

                // Scale down logo size for small screens, but still keep it larger than before
                var logoImage = FindLogoImage();
                if (logoImage != null)
                {
                    logoImage.HeightRequest = 100;
                }
            }
            else if (DeviceHelper.IsLargeScreen || DeviceHelper.IsDesktop)
            {
                // Large screen adjustments - increase padding and spacing
                var mainContentStack = FindMainContentStack();
                if (mainContentStack != null)
                {
                    mainContentStack.Padding = new Thickness(LARGE_SCREEN_PADDING, LARGE_SCREEN_PADDING);
                    mainContentStack.Spacing = 32;
                }

                // Increase logo size for large screens
                var logoImage = FindLogoImage();
                if (logoImage != null)
                {
                    logoImage.HeightRequest = 400;
                }

                // Increase main title font size
                var titleLabel = FindTitleLabel();
                if (titleLabel != null)
                {
                    titleLabel.FontSize = 32;
                }
            }
        }

        // Helper methods to find UI elements
        private VerticalStackLayout FindMainContentStack()
        {
            try
            {
                var scrollView = this.Content is Grid grid && grid.Children.Count > 1
                    ? grid.Children[1] as ScrollView
                    : null;

                return scrollView?.Content as VerticalStackLayout;
            }
            catch
            {
                return null;
            }
        }

        private Image FindLogoImage()
        {
            try
            {
                // In the new layout, we have a direct reference to the LogoImage
                return LogoImage;
            }
            catch
            {
                return null;
            }
        }

        private Label FindTitleLabel()
        {
            try
            {
                var mainStack = FindMainContentStack();
                if (mainStack?.Children.Count > 0 && mainStack.Children[0] is VerticalStackLayout logoStack && logoStack.Children.Count > 1)
                {
                    return logoStack.Children[1] as Label;
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            // Clear any previous login data when returning to this page
            _viewModel?.ClearLoginData();

            System.Diagnostics.Debug.WriteLine("[LoginPage] OnAppearing");
            
            // Apply current window state
            OnWindowMaximizationChanged(null, DeviceHelper.IsWindowMaximized);
        }
        
        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            
            // Unsubscribe from events to prevent memory leaks
            DeviceHelper.WindowMaximizationChanged -= OnWindowMaximizationChanged;
        }

        private async void OnSignupClicked(object sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[LoginPage] Navigating to SignupPage");
                
                // Check if Shell.Current is available (when app is in Shell mode)
                if (Shell.Current != null)
                {
                    System.Diagnostics.Debug.WriteLine("[LoginPage] Using Shell navigation");
                    await Shell.Current.GoToAsync("//SignupPage");
                }
                else
                {
                    // When LoginPage is set as MainPage directly (not in Shell), use Navigation.PushAsync
                    System.Diagnostics.Debug.WriteLine("[LoginPage] Shell.Current is null, using Navigation.PushAsync");
                    var signupPage = App.Services?.GetService<SignupPage>();
                    if (signupPage != null)
                    {
                        try
                        {
                            System.Diagnostics.Debug.WriteLine("[LoginPage] Attempting Navigation.PushAsync(signupPage)");
                            await Navigation.PushAsync(signupPage);
                            System.Diagnostics.Debug.WriteLine("[LoginPage] Navigation.PushAsync completed.");
                        }
                        catch (Exception navEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"[LoginPage] Error during Navigation.PushAsync: {navEx.Message}");
                            await DisplayAlert("Error", $"Navigation failed: {navEx.Message}", "OK");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[LoginPage] Could not resolve SignupPage from DI container");
                        await DisplayAlert("Error", "Could not navigate to signup page. Please try again.", "OK");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error navigating to SignupPage: {ex.Message}");
                await DisplayAlert("Error", "An error occurred while navigating to the signup page.", "OK");
            }
        }

        private async void OnDiagnosticsClicked(object sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[LoginPage] Navigating to DiagnosticsPage");
                var diagnosticsPage = App.Services?.GetService<Pages.DiagnosticsPage>();

                if (diagnosticsPage != null)
                {
                    await Navigation.PushAsync(diagnosticsPage);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Error: Could not resolve DiagnosticsPage from DI container");
                    await DisplayAlert("Error", "Could not navigate to diagnostics page. Please try again.", "OK");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error navigating to DiagnosticsPage: {ex.Message}");
                await DisplayAlert("Error", "An error occurred while navigating to the diagnostics page.", "OK");
            }
        }

        private async void OnForgotPasswordClicked(object sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[LoginPage] Handling Forgot Password");

                // Check if username was entered to pre-fill it in the recovery form
                string username = !string.IsNullOrEmpty(_viewModel?.Username)
                    ? _viewModel.Username
                    : string.Empty;

                // Show an input prompt for email
                string email = await DisplayPromptAsync(
                    "Password Recovery",
                    "Enter your email address to receive a password reset link",
                    initialValue: username,
                    keyboard: Keyboard.Email);

                if (!string.IsNullOrWhiteSpace(email))
                {
                    // Show loading indicator
                    IsBusy = true;

                    try
                    {
                        // Simulate API call
                        await Task.Delay(1500);

                        // In a real implementation, you would call an API service
                        // var result = await _authService.RequestPasswordResetAsync(email);

                        // Show success message
                        await DisplayAlert(
                            "Recovery Email Sent",
                            "If an account exists with this email, you will receive instructions to reset your password.",
                            "OK");

                        // Set the entered email as the username for convenience
                        if (_viewModel != null)
                        {
                            _viewModel.Username = email;
                        }
                    }
                    finally
                    {
                        IsBusy = false;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LoginPage] Error handling password recovery: {ex.Message}");
                await DisplayAlert("Error", "Unable to process your password recovery request. Please try again later.", "OK");
            }
        }
    }
}