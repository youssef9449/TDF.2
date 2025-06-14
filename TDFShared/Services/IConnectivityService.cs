using System;
using System.Threading;
using System.Threading.Tasks;
using TDFShared.Models;

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
        /// Whether the API is reachable
        /// </summary>
        public bool IsApiReachable { get; set; }

        /// <summary>
        /// Network latency in milliseconds
        /// </summary>
        public int Latency { get; set; }

        /// <summary>
        /// Any error that occurred while checking connectivity
        /// </summary>
        public string? Error { get; set; }

        /// <summary>
        /// Additional connectivity information
        /// </summary>
        public ConnectivityInfo? ConnectivityInfo { get; set; }

        /// <summary>
        /// Creates a new instance of TDFConnectivityChangedEventArgs
        /// </summary>
        public TDFConnectivityChangedEventArgs()
        {
        }

        /// <summary>
        /// Creates a new instance of TDFConnectivityChangedEventArgs with the specified connection state
        /// </summary>
        /// <param name="isConnected">Whether the network is connected</param>
        public TDFConnectivityChangedEventArgs(bool isConnected)
        {
            IsConnected = isConnected;
        }

        /// <summary>
        /// Creates a new instance of TDFConnectivityChangedEventArgs with the specified values
        /// </summary>
        /// <param name="isConnected">Whether the network is connected</param>
        /// <param name="connectionType">The type of network connection</param>
        /// <param name="isApiReachable">Whether the API is reachable</param>
        /// <param name="latency">The network latency in milliseconds</param>
        public TDFConnectivityChangedEventArgs(bool isConnected, string connectionType, bool isApiReachable, int latency)
        {
            IsConnected = isConnected;
            ConnectionType = connectionType;
            IsApiReachable = isApiReachable;
            Latency = latency;
        }

        /// <summary>
        /// Creates a new instance of TDFConnectivityChangedEventArgs with an error
        /// </summary>
        /// <param name="error">The error message</param>
        public TDFConnectivityChangedEventArgs(string error)
        {
            IsConnected = false;
            Error = error;
        }
    }
}