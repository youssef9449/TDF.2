namespace TDFMAUI.Services
{
    /// <summary>
    /// Service to check the device's network connectivity status
    /// </summary>
    public interface IConnectivityService
    {
        /// <summary>
        /// Determines if the device is currently connected to a network
        /// </summary>
        /// <returns>True if connected, false otherwise</returns>
        bool IsConnected();

        /// <summary>
        /// Event raised when the connectivity status changes
        /// </summary>
        event EventHandler<TDFConnectivityChangedEventArgs> ConnectivityChanged;
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
        public string ConnectionType { get; set; }

        /// <summary>
        /// Time when the connectivity status changed
        /// </summary>
        public DateTime Timestamp { get; set; }
    }
} 