using System;
using System.Threading;
using System.Threading.Tasks;

namespace TDFShared.Services
{
    /// <summary>
    /// Service to check the device's network connectivity status and monitor changes
    /// </summary>
    public interface IConnectivityService : IDisposable
    {
        /// <summary>
        /// Determines if the device is currently connected to a network
        /// </summary>
        /// <returns>True if connected, false otherwise</returns>
        bool IsConnected();

        /// <summary>
        /// Asynchronously checks network connectivity
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if connected, false otherwise</returns>
        Task<bool> IsConnectedAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets detailed connectivity information
        /// </summary>
        /// <returns>Connectivity information</returns>
        Task<ConnectivityInfo> GetConnectivityInfoAsync();

        /// <summary>
        /// Tests connectivity to a specific host
        /// </summary>
        /// <param name="host">Host to test (e.g., "google.com")</param>
        /// <param name="timeout">Timeout for the test</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if host is reachable, false otherwise</returns>
        Task<bool> TestConnectivityAsync(string host, TimeSpan timeout, CancellationToken cancellationToken = default);

        /// <summary>
        /// Waits for network connectivity to be restored
        /// </summary>
        /// <param name="timeout">Maximum time to wait</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if connectivity was restored within timeout, false otherwise</returns>
        Task<bool> WaitForConnectivityAsync(TimeSpan timeout, CancellationToken cancellationToken = default);

        /// <summary>
        /// Event raised when the connectivity status changes
        /// </summary>
        event EventHandler<TDFConnectivityChangedEventArgs> ConnectivityChanged;

        /// <summary>
        /// Event raised when network is restored after being lost
        /// </summary>
        event EventHandler NetworkRestored;

        /// <summary>
        /// Event raised when network is lost
        /// </summary>
        event EventHandler NetworkLost;
    }

    /// <summary>
    /// Detailed connectivity information
    /// </summary>
    public class ConnectivityInfo
    {
        /// <summary>
        /// Whether the device is connected to a network
        /// </summary>
        public bool IsConnected { get; set; }

        /// <summary>
        /// Type of network connection (WiFi, Cellular, Ethernet, etc.)
        /// </summary>
        public string ConnectionType { get; set; } = string.Empty;

        /// <summary>
        /// Available connection profiles
        /// </summary>
        public string[] ConnectionProfiles { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Network access level (Internet, Local, None, etc.)
        /// </summary>
        public string NetworkAccess { get; set; } = string.Empty;

        /// <summary>
        /// Whether the connection is metered (limited data)
        /// </summary>
        public bool IsMetered { get; set; }

        /// <summary>
        /// Signal strength (0-100, if available)
        /// </summary>
        public int? SignalStrength { get; set; }

        /// <summary>
        /// When this information was last updated
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Event args for connectivity status changes
    /// </summary>
    public class TDFConnectivityChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Indicates if the device is currently connected
        /// </summary>
        public bool IsConnected { get; set; }

        /// <summary>
        /// The type of network connection, if connected
        /// </summary>
        public string ConnectionType { get; set; } = string.Empty;

        /// <summary>
        /// Time when the connectivity status changed
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Previous connectivity state
        /// </summary>
        public bool PreviousState { get; set; }

        /// <summary>
        /// Additional connectivity information
        /// </summary>
        public ConnectivityInfo? ConnectivityInfo { get; set; }
    }
}