using System;
using Microsoft.Maui.Controls;
using TDFMAUI.ViewModels; // Add ViewModel namespace

namespace TDFMAUI.Features.Auth // Updated namespace
{
    public partial class LoginPage : ContentPage
    {

        // Keep ViewModel reference if needed for specific interactions, but primarily use BindingContext
        private readonly LoginPageViewModel _viewModel;

        public LoginPage(LoginPageViewModel viewModel) // Inject the ViewModel
        {
            InitializeComponent();

            _viewModel = viewModel;
            BindingContext = _viewModel; // Set BindingContext to the ViewModel

        }

        protected override void OnAppearing() // Keep OnAppearing if needed for other reasons
        {
            base.OnAppearing();
        }

        private async void OnSignupClicked(object sender, EventArgs e)
        {
            try
            {
                // Let the DI container create both the ViewModel and the Page
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
                // Get the DiagnosticsPage from the DI container
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
    }
}