using System;
using System.Threading.Tasks;
using TDFMAUI.Config;

namespace TDFMAUI.Services
{
    public class NetworkMonitorService
    {
        // Event for network connectivity changes
        public event EventHandler<NetworkStatusChangedEventArgs> NetworkStatusChanged;
        
        // Event specifically for network restoration
        public event EventHandler NetworkRestored;
        
        // Current network status
        public bool IsConnected => Connectivity.NetworkAccess == NetworkAccess.Internet ||
                                  Connectivity.NetworkAccess == NetworkAccess.ConstrainedInternet;
        
        private bool _wasConnected = false;
        
        public NetworkMonitorService()
        {
            // Subscribe to connectivity changes
            Connectivity.ConnectivityChanged += Connectivity_ConnectivityChanged;
            
            // Log initial state
            DebugService.LogInfo("NetworkMonitor", $"Initial network state: {(IsConnected ? "Connected" : "Disconnected")}");
        }
        
        private void Connectivity_ConnectivityChanged(object sender, ConnectivityChangedEventArgs e)
        {
            bool isConnected = e.NetworkAccess == NetworkAccess.Internet || 
                              e.NetworkAccess == NetworkAccess.ConstrainedInternet;
                              
            DebugService.LogInfo("NetworkMonitor", $"Network connectivity changed: {(isConnected ? "Connected" : "Disconnected")}");
            
            // Detect if this is a network restoration
            bool isNetworkRestored = isConnected && !_wasConnected;
            _wasConnected = isConnected;
            
            // Notify subscribers of the change
            NetworkStatusChanged?.Invoke(this, new NetworkStatusChangedEventArgs(isConnected));
            
            // If connection was restored, notify interested parties and trigger reconnection attempts
            if (isNetworkRestored)
            {
                DebugService.LogInfo("NetworkMonitor", "Network connection restored");
                NetworkRestored?.Invoke(this, EventArgs.Empty);
                Task.Run(async () => await TriggerReconnectionAttempts());
            }
        }
        
        private async Task TriggerReconnectionAttempts()
        {
            try
            {
                // Wait a moment for network to stabilize
                await Task.Delay(1000);
                
                // Test API connectivity - fix nullable method group
                bool apiAvailable = false;
                try
                {
                    apiAvailable = await ApiConfig.TestApiConnectivityAsync();
                }
                catch (Exception ex)
                {
                    DebugService.LogError("NetworkMonitor", $"API connectivity test failed: {ex.Message}");
                    apiAvailable = false;
                }
                
                DebugService.LogInfo("NetworkMonitor", $"API connectivity test: {(apiAvailable ? "Available" : "Unavailable")}");
                
                // If services need to be restarted/reconnected when network becomes available,
                // this would be the place to do it
            }
            catch (Exception ex)
            {
                DebugService.LogError("NetworkMonitor", $"Error during reconnection attempt: {ex.Message}");
            }
        }
        
        // Method to manually test connectivity
        public async Task<(bool IsConnected, bool IsApiReachable)> TestConnectivityAsync()
        {
            bool isConnected = IsConnected;
            bool isApiReachable = false;
            
            if (isConnected)
            {
                try
                {
                    // Fix nullable method group
                    isApiReachable = await ApiConfig.TestApiConnectivityAsync();
                }
                catch (Exception ex)
                {
                    DebugService.LogError("NetworkMonitor", $"API connectivity test failed: {ex.Message}");
                    isApiReachable = false;
                }
            }
            
            return (isConnected, isApiReachable);
        }
    }
    
    public class NetworkStatusChangedEventArgs : EventArgs
    {
        public bool IsConnected { get; }
        
        public NetworkStatusChangedEventArgs(bool isConnected)
        {
            IsConnected = isConnected;
        }
    }
} 