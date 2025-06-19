using System;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TDFShared.Models;

namespace TDFShared.Services
{
    /// <summary>
    /// Base connectivity service implementation with platform-agnostic network monitoring
    /// Platform-specific implementations should inherit from this class
    /// </summary>
    public class ConnectivityService : IConnectivityService
    {
        /// <summary>
        /// Logger for connectivity events and errors.
        /// </summary>
        protected readonly ILogger<ConnectivityService> _logger;
        /// <summary>
        /// Indicates whether network monitoring is active.
        /// </summary>
        protected bool _isMonitoring = false;
        /// <summary>
        /// Stores the last known network connectivity state.
        /// </summary>
        protected bool _lastKnownState = false;
        /// <summary>
        /// Lock object for thread-safe state changes.
        /// </summary>
        protected readonly object _stateLock = new object();
        /// <summary>
        /// Timer for periodic connectivity checks.
        /// </summary>
        protected Timer? _connectivityTimer;

        /// <summary>
        /// Occurs when network connectivity changes.
        /// </summary>
        public event EventHandler<TDFConnectivityChangedEventArgs>? ConnectivityChanged;
        /// <summary>
        /// Occurs when network connectivity is restored.
        /// </summary>
        public event EventHandler? NetworkRestored;
        /// <summary>
        /// Occurs when network connectivity is lost.
        /// </summary>
        public event EventHandler? NetworkLost;

        public ConnectivityService(ILogger<ConnectivityService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Initialize with current state
            _lastKnownState = IsConnected();

            // Start periodic monitoring
            StartPeriodicMonitoring();

            _logger.LogInformation("ConnectivityService initialized. Initial state: {IsConnected}", _lastKnownState);
        }

        /// <summary>
        /// Basic network connectivity check using NetworkInterface
        /// Platform-specific implementations should override this for better accuracy
        /// </summary>
        public virtual bool IsConnected()
        {
            try
            {
                return NetworkInterface.GetIsNetworkAvailable();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking network availability");
                return false;
            }
        }

        /// <summary>
        /// Asynchronous connectivity check
        /// </summary>
        public virtual async Task<bool> IsConnectedAsync(CancellationToken cancellationToken = default)
        {
            return await Task.Run(() => IsConnected(), cancellationToken);
        }

        /// <summary>
        /// Gets basic connectivity information
        /// Platform-specific implementations should override for detailed info
        /// </summary>
        public virtual async Task<ConnectivityInfo> GetConnectivityInfoAsync()
        {
            var isConnected = await IsConnectedAsync();

            return new ConnectivityInfo
            {
                IsConnected = isConnected,
                ConnectionType = isConnected ? "Unknown" : "None",
                NetworkAccess = isConnected ? "Internet" : "None",
                ConnectionProfiles = isConnected ? new[] { "Unknown" } : Array.Empty<string>(),
                LastUpdated = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Tests connectivity to a specific host using ping
        /// </summary>
        public virtual async Task<bool> TestConnectivityAsync(string host, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(host))
                throw new ArgumentException("Host cannot be null or empty", nameof(host));

            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(host, (int)timeout.TotalMilliseconds);
                var isReachable = reply.Status == IPStatus.Success;

                _logger.LogDebug("Ping test to {Host}: {Status} ({RoundtripTime}ms)",
                    host, reply.Status, reply.RoundtripTime);

                return isReachable;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to ping {Host}", host);
                return false;
            }
        }

        /// <summary>
        /// Waits for network connectivity to be restored
        /// </summary>
        public virtual async Task<bool> WaitForConnectivityAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            if (IsConnected())
                return true;

            var tcs = new TaskCompletionSource<bool>();

            void OnConnectivityChanged(object? sender, TDFConnectivityChangedEventArgs e)
            {
                if (e.IsConnected)
                    tcs.TrySetResult(true);
            }

            try
            {
                ConnectivityChanged += OnConnectivityChanged;

                var timeoutTask = Task.Delay(timeout, cancellationToken);
                var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

                if (completedTask == tcs.Task)
                {
                    return await tcs.Task;
                }
                else
                {
                    _logger.LogWarning("Timeout waiting for network connectivity after {Timeout}", timeout);
                    tcs.TrySetResult(false);
                    return false;
                }
            }
            finally
            {
                ConnectivityChanged -= OnConnectivityChanged;
            }
        }

        /// <summary>
        /// Starts periodic monitoring of network connectivity
        /// </summary>
        protected virtual void StartPeriodicMonitoring()
        {
            if (_isMonitoring)
                return;

            _isMonitoring = true;

            // Check connectivity every 5 seconds
            _connectivityTimer = new Timer(CheckConnectivityCallback, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));

            _logger.LogDebug("Started periodic connectivity monitoring");
        }

        /// <summary>
        /// Stops periodic monitoring
        /// </summary>
        protected virtual void StopPeriodicMonitoring()
        {
            if (!_isMonitoring)
                return;

            _isMonitoring = false;
            _connectivityTimer?.Dispose();
            _connectivityTimer = null;

            _logger.LogDebug("Stopped periodic connectivity monitoring");
        }

        /// <summary>
        /// Timer callback to check connectivity
        /// </summary>
        private void CheckConnectivityCallback(object? state)
        {
            try
            {
                var currentState = IsConnected();

                lock (_stateLock)
                {
                    if (currentState != _lastKnownState)
                    {
                        _logger.LogInformation("Network connectivity changed: {PreviousState} -> {CurrentState}",
                            _lastKnownState, currentState);

                        var eventArgs = new TDFConnectivityChangedEventArgs
                        {
                            IsConnected = currentState,
                            PreviousState = _lastKnownState,
                            Timestamp = DateTime.UtcNow,
                            ConnectionType = currentState ? "Unknown" : "None"
                        };

                        _lastKnownState = currentState;

                        // Raise events using protected methods
                        OnConnectivityChanged(eventArgs);

                        if (currentState)
                        {
                            OnNetworkRestored();
                        }
                        else
                        {
                            OnNetworkLost();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during connectivity check");
            }
        }

        /// <summary>
        /// Manually triggers a connectivity check
        /// </summary>
        public virtual void RefreshConnectivityStatus()
        {
            CheckConnectivityCallback(null);
        }

        /// <summary>
        /// Protected method to raise ConnectivityChanged event from derived classes
        /// </summary>
        protected virtual void OnConnectivityChanged(TDFConnectivityChangedEventArgs eventArgs)
        {
            ConnectivityChanged?.Invoke(this, eventArgs);
        }

        /// <summary>
        /// Protected method to raise NetworkRestored event from derived classes
        /// </summary>
        protected virtual void OnNetworkRestored()
        {
            NetworkRestored?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Protected method to raise NetworkLost event from derived classes
        /// </summary>
        protected virtual void OnNetworkLost()
        {
            NetworkLost?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Disposes the service and stops monitoring
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected dispose method for inheritance
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                StopPeriodicMonitoring();
                _logger.LogDebug("ConnectivityService disposed");
            }
        }
    }
}
