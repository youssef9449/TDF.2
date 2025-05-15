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
                
                // Scale down logo size for small screens
                var logoImage = FindLogoImage();
                if (logoImage != null)
                {
                    logoImage.HeightRequest = 60;
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
                var mainStack = FindMainContentStack();
                if (mainStack?.Children.Count > 0 && mainStack.Children[0] is VerticalStackLayout logoStack && logoStack.Children.Count > 0)
                {
                    return logoStack.Children[0] as Image;
                }
                return null;
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
        }

        private async void OnSignupClicked(object sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[LoginPage] Navigating to SignupPage");
                var signupPage = App.Services?.GetService<SignupPage>();

                if (signupPage != null)
                {
                    await Navigation.PushAsync(signupPage);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Error: Could not resolve SignupPage from DI container");
                    await DisplayAlert("Error", "Could not navigate to signup page. Please try again.", "OK");
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
                var diagnosticsPage = App.Services?.GetService<TDFMAUI.Pages.DiagnosticsPage>();

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