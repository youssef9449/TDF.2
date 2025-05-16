using Microsoft.Extensions.Logging;

namespace TDFMAUI.Services
{
    /// <summary>
    /// Service implementation to monitor and check network connectivity
    /// </summary>
    public class ConnectivityService : IConnectivityService
    {
        private readonly ILogger<ConnectivityService> _logger;
        private IConnectivity _connectivity => Connectivity.Current;

        /// <summary>
        /// Event raised when connectivity status changes
        /// </summary>
        public event EventHandler<TDFConnectivityChangedEventArgs> ConnectivityChanged;

        /// <summary>
        /// Constructor with dependency injection
        /// </summary>
        public ConnectivityService(ILogger<ConnectivityService> logger)
        {
            _logger = logger;
            
            // Subscribe to system connectivity changes
            _connectivity.ConnectivityChanged += OnNativeConnectivityChanged;
        }

        /// <summary>
        /// Check if the device is currently connected to a network
        /// </summary>
        public bool IsConnected()
        {
            var isConnected = _connectivity.NetworkAccess == NetworkAccess.Internet;
            _logger.LogDebug($"Network connectivity check: {isConnected}");
            return isConnected;
        }

        /// <summary>
        /// Handle system connectivity changes
        /// </summary>
        private void OnNativeConnectivityChanged(object sender, Microsoft.Maui.Networking.ConnectivityChangedEventArgs e)
        {
            var isConnected = _connectivity.NetworkAccess == NetworkAccess.Internet;
            var connectionType = GetConnectionType();
            
            _logger.LogInformation($"Connectivity changed: Connected={isConnected}, Type={connectionType}");

            // Raise event for subscribers
            MainThread.BeginInvokeOnMainThread(() =>
            {
                ConnectivityChanged?.Invoke(this, new TDFConnectivityChangedEventArgs
                {
                    IsConnected = isConnected,
                    ConnectionType = connectionType,
                    Timestamp = DateTime.UtcNow
                });
            });
        }

        /// <summary>
        /// Get the current connection type description
        /// </summary>
        private string GetConnectionType()
        {
            if (_connectivity.NetworkAccess != NetworkAccess.Internet)
                return "None";

            var profiles = _connectivity.ConnectionProfiles;
            if (profiles.Contains(ConnectionProfile.WiFi))
                return "WiFi";
            if (profiles.Contains(ConnectionProfile.Cellular))
                return "Cellular";
            if (profiles.Contains(ConnectionProfile.Ethernet))
                return "Ethernet";
            
            return "Unknown";
        }

        /// <summary>
        /// Cleanup resources
        /// </summary>
        public void Dispose()
        {
            _connectivity.ConnectivityChanged -= OnNativeConnectivityChanged;
        }
    }
} 