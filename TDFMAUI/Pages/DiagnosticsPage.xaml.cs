using Microsoft.Maui.Networking;
using TDFMAUI.Config;
using TDFMAUI.Services;

namespace TDFMAUI.Pages
{
    public partial class DiagnosticsPage : ContentPage
    {
        private readonly IConnectivity _connectivity;
        private readonly IApiService _apiService;
        
        public DiagnosticsPage(IConnectivity connectivity, IApiService apiService)
        {
            InitializeComponent();
            
            _connectivity = connectivity;
            _apiService = apiService;
            
            // Load initial data
            LoadConfigInfo();
            LoadDeviceInfo();
            UpdateNetworkStatus();
            RefreshLogs();
        }
        
        protected override void OnAppearing()
        {
            base.OnAppearing();
            
            // Refresh data when page appears
            UpdateNetworkStatus();
            RefreshLogs();
        }
        
        private void LoadConfigInfo()
        {
            try
            {
                string configInfo = $"Development Mode: {ApiConfig.DevelopmentMode}\n" +
                                   $"API URL: {ApiConfig.BaseUrl}\n" +
                                   $"WebSocket URL: {ApiConfig.WebSocketUrl}\n" +
                                   $"Timeout: {ApiConfig.Timeout} seconds";
                
                ConfigLabel.Text = configInfo;
            }
            catch (Exception ex)
            {
                ConfigLabel.Text = $"Error loading config: {ex.Message}";
            }
        }
        
        private void LoadDeviceInfo()
        {
            try
            {
                string deviceInfo = $"Device Platform: {DeviceInfo.Platform}\n" +
                                   $"Device Type: {DeviceInfo.DeviceType}\n" +
                                   $"OS Version: {DeviceInfo.VersionString}\n" +
                                   $"Manufacturer: {DeviceInfo.Manufacturer}\n" +
                                   $"Model: {DeviceInfo.Model}\n" +
                                   $"App Version: {AppInfo.VersionString}";
                
                DeviceInfoLabel.Text = deviceInfo;
            }
            catch (Exception ex)
            {
                DeviceInfoLabel.Text = $"Error loading device info: {ex.Message}";
            }
        }
        
        private void UpdateNetworkStatus()
        {
            NetworkStatusLabel.Text = "Checking network status..."; // Provide immediate feedback
            try
            {
                var networkAccess = _connectivity.NetworkAccess;
                var connectionProfiles = _connectivity.ConnectionProfiles;

                DebugService.LogInfo("DiagnosticsPage", $"UpdateNetworkStatus called. NetworkAccess: {networkAccess}");
                string profilesLog = "Connection Profiles: " + (connectionProfiles.Any() ? string.Join(", ", connectionProfiles) : "None");
                DebugService.LogInfo("DiagnosticsPage", profilesLog);
                
                string networkStatus = $"Network Access: {networkAccess}\n";
                networkStatus += "Connection Profiles: ";
                
                if (connectionProfiles.Any())
                {
                    networkStatus += string.Join(", ", connectionProfiles);
                }
                else
                {
                    networkStatus += "None";
                }
                
                NetworkStatusLabel.Text = networkStatus;
                DebugService.LogInfo("DiagnosticsPage", $"NetworkStatusLabel updated to: {networkStatus.Replace("\n", " ")}"); // Log what was set
            }
            catch (Exception ex)
            {
                NetworkStatusLabel.Text = $"Error checking network: {ex.Message}";
                DebugService.LogError("DiagnosticsPage", $"Error in UpdateNetworkStatus: {ex.Message}");
            }
            // Ensure logs are refreshed so we can see the new messages
            _ = RefreshLogs();
        }
        
        private async Task RefreshLogs()
        {
            try
            {
                var logs = DebugService.GetFormattedLogs();
                LogsLabel.Text = string.IsNullOrEmpty(logs) ? "No logs to display" : logs;
            }
            catch (Exception ex)
            {
                LogsLabel.Text = $"Error refreshing logs: {ex.Message}";
            }
        }
        
        private async void TestApiButton_Clicked(object sender, EventArgs e)
        {
            TestApiButton.IsEnabled = false;
            ApiStatusLabel.Text = "Testing...";
            
            try
            {
                bool isConnected = await ApiConfig.TestApiConnectivityAsync();
                ApiStatusLabel.Text = isConnected 
                    ? "Connected to API successfully" 
                    : "Failed to connect to API";
                
                // Log the result
                DebugService.LogInfo("DiagnosticsPage", $"API connection test result: {isConnected}");
            }
            catch (Exception ex)
            {
                ApiStatusLabel.Text = $"Error testing API: {ex.Message}";
                DebugService.LogError("DiagnosticsPage", $"API test error: {ex.Message}");
            }
            finally
            {
                TestApiButton.IsEnabled = true;
                // Refresh logs to display the API test result
                await RefreshLogs();
            }
        }
        
        private void CheckNetworkButton_Clicked(object sender, EventArgs e)
        {
            UpdateNetworkStatus();
        }
        
        private async void RefreshLogsButton_Clicked(object sender, EventArgs e)
        {
            await RefreshLogs();
        }
        
        private async void SaveLogsButton_Clicked(object sender, EventArgs e)
        {
            SaveLogsButton.IsEnabled = false;
            
            try
            {
                bool saved = await DebugService.SaveLogsToFile();
                if (saved)
                {
                    await DisplayAlert("Success", "Logs have been saved successfully.", "OK");
                }
                else
                {
                    await DisplayAlert("Error", "Failed to save logs.", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to save logs: {ex.Message}", "OK");
            }
            finally
            {
                SaveLogsButton.IsEnabled = true;
            }
        }
        
        private async void ClearCacheButton_Clicked(object sender, EventArgs e)
        {
            bool confirm = await DisplayAlert("Confirm", "This will clear all app cache and stored data. Continue?", "Yes", "No");
            
            if (confirm)
            {
                try
                {
                    // Clear secure storage
                    await SecureStorage.Default.SetAsync("auth_token", string.Empty);
                    await SecureStorage.Default.SetAsync("refresh_token", string.Empty);
                    
                    // Clear preferences
                    Preferences.Default.Clear();
                    
                    // Clear any other cached data
                    // ...
                    
                    await DisplayAlert("Success", "App cache cleared successfully. The app will now restart.", "OK");
                    
                    // Log the cache clear
                    DebugService.LogInfo("DiagnosticsPage", "App cache cleared by user");
                    
                    // Restart the app (this is a simple way to simulate a restart)
                    Application.Current.MainPage = new NavigationPage(new DiagnosticsPage(_connectivity, _apiService));
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error", $"Failed to clear cache: {ex.Message}", "OK");
                    DebugService.LogError("DiagnosticsPage", $"Cache clear error: {ex.Message}");
                }
            }
        }
    }
}
