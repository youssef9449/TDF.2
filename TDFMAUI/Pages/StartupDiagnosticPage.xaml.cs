using System;
using System.Linq;
using System.Text;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Networking;
using TDFMAUI.Config;
using TDFMAUI.Services;

namespace TDFMAUI.Pages
{
    public partial class StartupDiagnosticPage : ContentPage
    {
        // Property to store the next page to navigate to
        public Page? NextPage { get; set; }

        public StartupDiagnosticPage()
        {
            InitializeComponent();
            LoadInformation();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            // Subscribe to connectivity changes
            Connectivity.ConnectivityChanged += Connectivity_ConnectivityChanged;

            // Update network status
            UpdateNetworkStatus();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            // Unsubscribe from connectivity changes
            Connectivity.ConnectivityChanged -= Connectivity_ConnectivityChanged;
        }

        private void Connectivity_ConnectivityChanged(object sender, ConnectivityChangedEventArgs e)
        {
            // Update network status when connectivity changes
            UpdateNetworkStatus();
        }

        private void LoadInformation()
        {
            try
            {
                // Load API configuration
                ApiUrlLabel.Text = $"API URL: {ApiConfig.BaseUrl ?? "Not configured"}";
                WebSocketUrlLabel.Text = $"WebSocket URL: {ApiConfig.WebSocketUrl ?? "Not configured"}";
                DevelopmentModeLabel.Text = $"Development Mode: {ApiConfig.DevelopmentMode}";

                // Load device information
                var deviceInfo = new StringBuilder();
                deviceInfo.AppendLine($"Manufacturer: {DeviceInfo.Manufacturer}");
                deviceInfo.AppendLine($"Model: {DeviceInfo.Model}");
                deviceInfo.AppendLine($"Platform: {DeviceInfo.Platform}");
                deviceInfo.AppendLine($"OS Version: {DeviceInfo.VersionString}");
                deviceInfo.AppendLine($"App Version: {AppInfo.VersionString}");
                deviceInfo.AppendLine($"App Build: {AppInfo.BuildString}");

                DeviceInfoLabel.Text = deviceInfo.ToString();

                // Load initialization errors
                if (ApiConfig.InitializationErrors.Count > 0)
                {
                    var errors = new StringBuilder();
                    foreach (var error in ApiConfig.InitializationErrors)
                    {
                        errors.AppendLine($"• {error}");
                    }
                    ErrorsLabel.Text = errors.ToString();
                    ErrorsLabel.TextColor = Colors.Red;
                }
                else
                {
                    ErrorsLabel.Text = "No initialization errors detected";
                }
            }
            catch (Exception ex)
            {
                ErrorsLabel.Text = $"Error loading diagnostic information: {ex.Message}";
                ErrorsLabel.TextColor = Colors.Red;
                DebugService.LogError("StartupDiagnosticPage", ex);
            }
        }

        private void UpdateNetworkStatus()
        {
            var status = Connectivity.NetworkAccess;
            var profiles = Connectivity.ConnectionProfiles;

            var statusText = new StringBuilder();
            statusText.AppendLine($"Network Access: {status}");

            statusText.Append("Connection Profiles: ");
            if (profiles == null || !profiles.Any())
            {
                statusText.AppendLine("None");
            }
            else
            {
                statusText.AppendLine(string.Join(", ", profiles));
            }

            NetworkStatusLabel.Text = statusText.ToString();

            // Update color based on status
            NetworkStatusLabel.TextColor = status == NetworkAccess.Internet ?
                Colors.Green : Colors.Red;
        }

        private async void TestApiButton_Clicked(object sender, EventArgs e)
        {
            TestApiButton.IsEnabled = false;
            ApiStatusLabel.Text = "Testing API connection...";

            try
            {
                // Use fallback implementation since this page doesn't use dependency injection
                bool isConnected = await ApiConfig.TestApiConnectivityAsync(null);
                ApiStatusLabel.Text = isConnected
                    ? "✓ Connected to API successfully"
                    : "✗ Failed to connect to API";

                ApiStatusLabel.TextColor = isConnected ? Colors.Green : Colors.Red;

                // Log the result
                DebugService.LogInfo("StartupDiagnosticPage", $"API connection test result: {isConnected}");
            }
            catch (Exception ex)
            {
                ApiStatusLabel.Text = $"Error testing API: {ex.Message}";
                ApiStatusLabel.TextColor = Colors.Red;
                DebugService.LogError("StartupDiagnosticPage", $"API test error: {ex.Message}");
            }
            finally
            {
                TestApiButton.IsEnabled = true;
            }
        }

        private void CheckNetworkButton_Clicked(object sender, EventArgs e)
        {
            UpdateNetworkStatus();
        }

        private async void ContinueButton_Clicked(object sender, EventArgs e)
        {
            try
            {
                // Continue to the main app
                if (NextPage != null)
                {
                    // If we have a next page set, use it
                    DebugService.LogInfo("StartupDiagnosticPage", $"Navigating to {NextPage.GetType().Name}");

                    if (NextPage is AppShell)
                    {
                        // For AppShell, we need to set it as the Application.Current.MainPage
                        Application.Current.MainPage = NextPage;
                    }
                    else
                    {
                        // For other pages, navigate within the NavigationPage
                        await Navigation.PushAsync(NextPage);
                        // Remove the diagnostic page from the navigation stack
                        var existingPages = Navigation.NavigationStack.ToList();
                        foreach (var page in existingPages)
                        {
                            if (page != NextPage)
                            {
                                Navigation.RemovePage(page);
                            }
                        }
                    }
                }
                else
                {
                    // If no next page is set, just pop this page
                    DebugService.LogWarning("StartupDiagnosticPage", "No next page set, popping current page");
                    await Navigation.PopAsync();
                }
            }
            catch (Exception ex)
            {
                DebugService.LogError("StartupDiagnosticPage", $"Error navigating to next page: {ex.Message}");
                await DisplayAlert("Navigation Error",
                    $"Error navigating to the next page: {ex.Message}", "OK");
            }
        }

        private async void ViewLogsButton_Clicked(object sender, EventArgs e)
        {
            // Show logs in a popup
            var logs = DebugService.GetFormattedLogs();
            await DisplayAlert("Application Logs", logs, "Close");
        }
    }
}
