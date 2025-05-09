using TDFMAUI.Services;
using System.Text;
using TDFMAUI.Config;

namespace TDFMAUI.Pages
{
    public partial class DebugPage : ContentPage
    {
        private readonly ApiService _apiService;
        private readonly SecureStorageService _secureStorage;
        private readonly NetworkMonitorService _networkMonitor;
        private readonly StringBuilder _performanceResults = new StringBuilder();
        
        public DebugPage(ApiService apiService, SecureStorageService secureStorage, NetworkMonitorService networkMonitor)
        {
            InitializeComponent();
            
            _apiService = apiService;
            _secureStorage = secureStorage;
            _networkMonitor = networkMonitor;
            
            // Register for network status change events
            _networkMonitor.NetworkStatusChanged += OnNetworkStatusChanged;
            
            // Disable any buttons that require network if we're offline
            InitializeDebugInfo();
        }
        
        protected override void OnAppearing()
        {
            base.OnAppearing();
            RefreshAllInfo();
        }
        
        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _networkMonitor.NetworkStatusChanged -= OnNetworkStatusChanged;
        }
        
        private void OnNetworkStatusChanged(object sender, NetworkStatusChangedEventArgs e)
        {
            // Update network status on the UI thread
            MainThread.BeginInvokeOnMainThread(() => 
            {
                UpdateNetworkStatus(e.IsConnected);
            });
        }
        
        private void InitializeDebugInfo()
        {
            // Set API URL info
            var apiUrlLabel = this.FindByName<Label>("ApiUrlLabel");
            if (apiUrlLabel != null)
            {
                apiUrlLabel.Text = $"API URL: {ApiConfig.BaseUrl}";
                apiUrlLabel.TextColor = ApiConfig.DevelopmentMode ? Colors.Orange : Colors.Black;
            }
            
            // Set network status
            UpdateNetworkStatus(_networkMonitor.IsConnected);
        }

        // Added for XAML event handler fix
        private void OnRefreshClicked(object sender, EventArgs e)
        {
            RefreshAllInfo();
        }
    
        private async void RefreshAllInfo()
        {
            await RefreshTokenInfo();
            await RefreshNetworkInfo();
            await RefreshLogs();
        }
        
        private async Task RefreshTokenInfo()
        {
            var tokenStatusLabel = this.FindByName<Label>("TokenStatusLabel");
            var tokenExpirationLabel = this.FindByName<Label>("TokenExpirationLabel");
            
            // Skip if controls don't exist
            if (tokenStatusLabel == null || tokenExpirationLabel == null)
                return;
                
            try
            {
                var (token, expiration) = await _secureStorage.GetTokenAsync();
                
                if (!string.IsNullOrEmpty(token))
                {
                    tokenStatusLabel.Text = $"Token: {token.Substring(0, 10)}...";
                    tokenStatusLabel.TextColor = Colors.Green;
                    
                    var timeUntilExpiration = expiration - DateTime.UtcNow;
                    if (timeUntilExpiration.TotalMinutes < 0)
                    {
                        tokenExpirationLabel.Text = "Expiration: EXPIRED";
                        tokenExpirationLabel.TextColor = Colors.Red;
                    }
                    else if (timeUntilExpiration.TotalMinutes < 10)
                    {
                        tokenExpirationLabel.Text = $"Expiration: {expiration} (expires soon!)";
                        tokenExpirationLabel.TextColor = Colors.Orange;
                    }
                    else
                    {
                        tokenExpirationLabel.Text = $"Expiration: {expiration}";
                        tokenExpirationLabel.TextColor = Colors.Green;
                    }
                }
                else
                {
                    tokenStatusLabel.Text = "Token: None";
                    tokenStatusLabel.TextColor = Colors.Red;
                    tokenExpirationLabel.Text = "Expiration: N/A";
                    tokenExpirationLabel.TextColor = Colors.Red;
                }
            }
            catch (Exception ex)
            {
                tokenStatusLabel.Text = "Token: Error getting token";
                tokenStatusLabel.TextColor = Colors.Red;
                tokenExpirationLabel.Text = $"Error: {ex.Message}";
                tokenExpirationLabel.TextColor = Colors.Red;
                
                DebugService.LogError("DebugPage", ex);
            }
        }
        
        private async Task RefreshNetworkInfo()
        {
            var networkStatusLabel = this.FindByName<Label>("NetworkStatusLabel");
            var apiReachableLabel = this.FindByName<Label>("ApiReachableLabel");
            
            // Skip if controls don't exist
            if (networkStatusLabel == null || apiReachableLabel == null)
                return;
                
            try
            {
                var (isConnected, isApiReachable) = await _networkMonitor.TestConnectivityAsync();
                
                UpdateNetworkStatus(isConnected);
                
                apiReachableLabel.Text = $"API Reachable: {(isApiReachable ? "Yes" : "No")}";
                apiReachableLabel.TextColor = isApiReachable ? Colors.Green : Colors.Red;
            }
            catch (Exception ex)
            {
                DebugService.LogError("DebugPage", $"Error refreshing network info: {ex.Message}");
                
                networkStatusLabel.Text = $"Network Status: Error ({ex.Message})";
                networkStatusLabel.TextColor = Colors.Red;
                
                apiReachableLabel.Text = "API Reachable: Error";
                apiReachableLabel.TextColor = Colors.Red;
            }
        }
        
        private void UpdateNetworkStatus(bool isConnected)
        {
            var networkStatusLabel = this.FindByName<Label>("NetworkStatusLabel");
            if (networkStatusLabel != null)
            {
                networkStatusLabel.Text = $"Network Status: {(isConnected ? "Connected" : "Disconnected")}";
                networkStatusLabel.TextColor = isConnected ? Colors.Green : Colors.Red;
            }
        }
        
        private async Task RefreshLogs()
        {
            var logsLabel = this.FindByName<Label>("LogsLabel");
            if (logsLabel != null)
            {
                var logs = DebugService.GetFormattedLogs();
                logsLabel.Text = string.IsNullOrEmpty(logs) ? "No logs to display" : logs;
            }
        }
        
        // Button event handlers
        private async void TestApiButton_Clicked(object sender, EventArgs e)
        {
            var testApiButton = this.FindByName<Button>("TestApiButton");
            var apiStatusLabel = this.FindByName<Label>("ApiStatusLabel");
            
            if (testApiButton == null || apiStatusLabel == null)
                return;
                
            testApiButton.IsEnabled = false;
            apiStatusLabel.Text = "Status: Testing...";
            
            try
            {
                // Try to access a simple API endpoint that doesn't require authentication
                var httpClient = new HttpClient();
                var response = await httpClient.GetAsync(ApiConfig.BaseUrl + "healthcheck");
                
                if (response.IsSuccessStatusCode)
                {
                    apiStatusLabel.Text = $"Status: OK ({(int)response.StatusCode})";
                    apiStatusLabel.TextColor = Colors.Green;
                    DebugService.LogInfo("DebugPage", $"API test successful: {response.StatusCode}");
                }
                else
                {
                    apiStatusLabel.Text = $"Status: ERROR ({(int)response.StatusCode})";
                    apiStatusLabel.TextColor = Colors.Red;
                    DebugService.LogWarning("DebugPage", $"API test failed: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                apiStatusLabel.Text = $"Status: FAILED (Connection Error)";
                apiStatusLabel.TextColor = Colors.Red;
                DebugService.LogError("DebugPage", ex);
            }
            finally
            {
                testApiButton.IsEnabled = true;
            }
        }
        
        private async void RefreshTokenButton_Clicked(object sender, EventArgs e)
        {
            var refreshTokenButton = this.FindByName<Button>("RefreshTokenButton");
            if (refreshTokenButton != null)
            {
                refreshTokenButton.IsEnabled = false;
                await RefreshTokenInfo();
                refreshTokenButton.IsEnabled = true;
            }
        }
        
        private async void RefreshLogsButton_Clicked(object sender, EventArgs e)
        {
            var refreshLogsButton = this.FindByName<Button>("RefreshLogsButton");
            if (refreshLogsButton != null)
            {
                refreshLogsButton.IsEnabled = false;
                await RefreshLogs();
                refreshLogsButton.IsEnabled = true;
            }
        }
        
        private void ClearLogsButton_Clicked(object sender, EventArgs e)
        {
            var logsLabel = this.FindByName<Label>("LogsLabel");
            if (logsLabel != null)
            {
                logsLabel.Text = "Logs cleared from display";
            }
        }
        
        private async void SaveLogsButton_Clicked(object sender, EventArgs e)
        {
            var saveLogsButton = this.FindByName<Button>("SaveLogsButton");
            if (saveLogsButton == null)
                return;
                
            saveLogsButton.IsEnabled = false;
            
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
                saveLogsButton.IsEnabled = true;
            }
        }
        
        private async void TriggerExceptionButton_Clicked(object sender, EventArgs e)
        {
            // Ask for confirmation first
            bool confirm = await DisplayAlert("Confirm", 
                "This will intentionally trigger an exception to test error handling. Continue?", 
                "Yes", "No");
                
            if (confirm)
            {
                try
                {
                    DebugService.LogInfo("DebugPage", "Intentionally triggering a test exception");
                    
                    // This will throw a NullReferenceException
                    string nullString = null;
                    int length = nullString.Length;
                }
                catch (Exception ex)
                {
                    // This exception should be caught and handled properly
                    DebugService.LogError("DebugPage", ex);
                    await DisplayAlert("Exception Caught", 
                        $"Test exception was caught successfully: {ex.Message}", 
                        "OK");
                }
            }
        }
        
        private async void TestNetworkErrorButton_Clicked(object sender, EventArgs e)
        {
            // Ask for confirmation first
            bool confirm = await DisplayAlert("Confirm", 
                "This will attempt to connect to a non-existent server to test network error handling. Continue?", 
                "Yes", "No");
                
            if (confirm)
            {
                var testNetworkErrorButton = this.FindByName<Button>("TestNetworkErrorButton");
                if (testNetworkErrorButton != null)
                {
                    testNetworkErrorButton.IsEnabled = false;
                    
                    try
                    {
                        DebugService.LogInfo("DebugPage", "Testing network error handling");
                        
                        // Attempt to connect to a non-existent server
                        var httpClient = new HttpClient();
                        httpClient.Timeout = TimeSpan.FromSeconds(5); // Short timeout
                        
                        var response = await httpClient.GetAsync("https://nonexistent-server-12345.example.com");
                        // We should never reach this point
                        DebugService.LogWarning("DebugPage", "Network error test failed - no exception was thrown");
                    }
                    catch (Exception ex)
                    {
                        // This should catch a network error
                        DebugService.LogInfo("DebugPage", $"Network error test successful: {ex.GetType().Name}");
                        await DisplayAlert("Network Error Caught", 
                            $"Network error was caught successfully: {ex.Message}", 
                            "OK");
                    }
                    finally
                    {
                        testNetworkErrorButton.IsEnabled = true;
                    }
                }
            }
        }
        
        private async void TestConnectivityButton_Clicked(object sender, EventArgs e)
        {
            var testConnectivityButton = this.FindByName<Button>("TestConnectivityButton");
            var networkStatusLabel = this.FindByName<Label>("NetworkStatusLabel");
            var apiReachableLabel = this.FindByName<Label>("ApiReachableLabel");
            
            if (testConnectivityButton == null)
                return;
                
            testConnectivityButton.IsEnabled = false;
            
            if (networkStatusLabel != null)
                networkStatusLabel.Text = "Network Status: Testing...";
                
            if (apiReachableLabel != null)
                apiReachableLabel.Text = "API Reachable: Testing...";
            
            try
            {
                await RefreshNetworkInfo();
                DebugService.LogInfo("DebugPage", "Manual network connectivity test completed");
            }
            catch (Exception ex)
            {
                DebugService.LogError("DebugPage", ex);
            }
            finally
            {
                testConnectivityButton.IsEnabled = true;
            }
        }
        
        private async void TestApiPerformanceButton_Clicked(object sender, EventArgs e)
        {
            var testApiPerformanceButton = this.FindByName<Button>("TestApiPerformanceButton");
            var performanceLabel = this.FindByName<Label>("PerformanceLabel");
            
            if (testApiPerformanceButton == null || performanceLabel == null)
                return;
                
            testApiPerformanceButton.IsEnabled = false;
            performanceLabel.Text = "Running performance tests...";
            
            try
            {
                // Clear previous results
                _performanceResults.Clear();
                _performanceResults.AppendLine("API PERFORMANCE TEST RESULTS:");
                _performanceResults.AppendLine("===========================");
                
                // Perform a series of API tests
                await RunApiPerformanceTests();
                
                // Update the UI
                performanceLabel.Text = _performanceResults.ToString();
                DebugService.LogInfo("DebugPage", "API performance tests completed");
            }
            catch (Exception ex)
            {
                DebugService.LogError("DebugPage", ex);
                _performanceResults.AppendLine($"ERROR: {ex.Message}");
                performanceLabel.Text = _performanceResults.ToString();
            }
            finally
            {
                testApiPerformanceButton.IsEnabled = true;
            }
        }
        
        private void ClearPerformanceButton_Clicked(object sender, EventArgs e)
        {
            var performanceLabel = this.FindByName<Label>("PerformanceLabel");
            if (performanceLabel != null)
            {
                _performanceResults.Clear();
                performanceLabel.Text = "Performance results cleared";
            }
        }
        
        private async Task RunApiPerformanceTests()
        {
            try
            {
                // Test network latency first
                _performanceResults.AppendLine("\nAPI CONNECTIVITY TEST:");
                var networkTestStart = DateTime.Now;
                
                // Fix the nullable method group error by ensuring proper method invocation
                bool isApiReachable = false;
                try 
                {
                    if (_apiService != null)
                    {
                        // Split into two separate statements to avoid method group error
                        isApiReachable = await _apiService.TestConnectivityAsync();
                    }
                    else
                    {
                        _performanceResults.AppendLine("API Service is not available");
                        isApiReachable = false;
                    }
                }
                catch (Exception ex)
                {
                    _performanceResults.AppendLine($"API Connectivity Check Failed: {ex.Message}");
                    isApiReachable = false;
                }
                
                var networkTestDuration = DateTime.Now - networkTestStart;
                
                _performanceResults.AppendLine(
                    $"API Reachable: {(isApiReachable ? "Yes" : "No")} " +
                    $"(Time: {networkTestDuration.TotalMilliseconds:0.00}ms)");
                
                if (!isApiReachable)
                {
                    _performanceResults.AppendLine("Cannot perform API tests - API is not reachable");
                    return;
                }
                
                // Test health check endpoint as it doesn't require authentication
                _performanceResults.AppendLine("\nHEALTH CHECK ENDPOINT TEST:");
                
                for (int i = 0; i < 3; i++)
                {
                    DebugService.StartTimer("HealthCheck" + i);
                    try
                    {
                        using (var httpClient = new HttpClient())
                        {
                            var response = await httpClient.GetAsync(ApiConfig.BaseUrl + "healthcheck");
                            var success = response.IsSuccessStatusCode;
                            var elapsed = DebugService.StopTimer("HealthCheck" + i, false);
                            
                            _performanceResults.AppendLine(
                                $"Request {i+1}: Status {(int)response.StatusCode} " +
                                $"(Time: {elapsed.TotalMilliseconds:0.00}ms)");
                        }
                    }
                    catch (Exception ex)
                    {
                        var elapsed = DebugService.StopTimer("HealthCheck" + i, false);
                        _performanceResults.AppendLine(
                            $"Request {i+1}: FAILED - {ex.Message} " +
                            $"(Time: {elapsed.TotalMilliseconds:0.00}ms)");
                    }
                    
                    // Short delay between requests
                    await Task.Delay(500);
                }
                
                // Test if we have authentication
                var (token, _) = await _secureStorage.GetTokenAsync();
                bool isAuthenticated = !string.IsNullOrEmpty(token);
                
                if (isAuthenticated)
                {
                    _performanceResults.AppendLine("\nAUTHENTICATED ENDPOINT TEST:");
                    
                    // Test an authenticated endpoint
                    DebugService.StartTimer("UsersEndpoint");
                    try
                    {
                        var users = await _apiService.GetAllUsersAsync(1, 10);
                        var elapsed = DebugService.StopTimer("UsersEndpoint", false);
                        _performanceResults.AppendLine(
                            $"Get Users: Success - Returned {users.Items?.Count() ?? 0} users " +
                            $"(Time: {elapsed.TotalMilliseconds:0.00}ms)");
                    }
                    catch (Exception ex)
                    {
                        var elapsed = DebugService.StopTimer("UsersEndpoint", false);
                        _performanceResults.AppendLine(
                            $"Get Users: FAILED - {ex.Message} " +
                            $"(Time: {elapsed.TotalMilliseconds:0.00}ms)");
                    }
                }
                else
                {
                    _performanceResults.AppendLine("\nNot authenticated - skipping authenticated endpoint tests");
                }
            }
            catch (Exception ex)
            {
                _performanceResults.AppendLine($"Performance test error: {ex.Message}");
            }
        }
    }
} 