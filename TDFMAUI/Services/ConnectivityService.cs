using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Networking;
using System;
using System.Linq;
using System.Threading.Tasks;
using TDFMAUI.Config;
using TDFShared.Models;
using TDFShared.Services;

namespace TDFMAUI.Services
{
    /// <summary>
    /// MAUI-specific connectivity service implementation with platform-native connectivity monitoring
    /// Inherits from TDFShared base class and adds platform-specific features
    /// </summary>
    public class ConnectivityService : TDFShared.Services.ConnectivityService
    {
        private readonly IConnectivity _connectivity;
        private bool _isInitialized = false;

        // Event for network connectivity changes
        public event EventHandler<TDFConnectivityChangedEventArgs> NetworkStatusChanged;

        /// <summary>
        /// Constructor with dependency injection
        /// </summary>
        public ConnectivityService(ILogger<ConnectivityService> logger) : base(logger)
        {
            try
            {
                _connectivity = Connectivity.Current;
                if (_connectivity != null)
                {
                    _connectivity.ConnectivityChanged += OnNativeConnectivityChanged;
                    _isInitialized = true;
                    _logger.LogInformation("MAUI ConnectivityService initialized with platform-native monitoring");
                }
                else
                {
                    _logger.LogWarning("MAUI Connectivity.Current is null, falling back to base implementation");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing MAUI ConnectivityService");
            }
        }

        /// <summary>
        /// Check if the device is currently connected to a network using platform-native APIs
        /// Overrides base implementation for better accuracy on mobile platforms
        /// </summary>
        public override bool IsConnected()
        {
            try
            {
                if (!_isInitialized || _connectivity == null)
                {
                    _logger.LogDebug("MAUI Connectivity service not initialized, falling back to base implementation");
                    return base.IsConnected();
                }

                var isConnected = _connectivity.NetworkAccess == NetworkAccess.Internet ||
                                 _connectivity.NetworkAccess == NetworkAccess.ConstrainedInternet;
                _logger.LogDebug("MAUI platform connectivity check: {IsConnected}", isConnected);
                return isConnected;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking MAUI platform connectivity, falling back to base implementation");
                return base.IsConnected();
            }
        }

        /// <summary>
        /// Gets detailed connectivity information using MAUI platform APIs
        /// Overrides base implementation to provide platform-specific details
        /// </summary>
        public override async Task<ConnectivityInfo> GetConnectivityInfoAsync()
        {
            try
            {
                var isConnected = IsConnected();
                var connectionType = GetConnectionType();
                var profiles = _connectivity.ConnectionProfiles.Select(p => p.ToString()).ToArray();
                var networkAccess = _connectivity.NetworkAccess.ToString();

                // Get enhanced platform-specific information
                var enhancedInfo = GetEnhancedPlatformInfo();

                var connectivityInfo = new ConnectivityInfo
                {
                    IsConnected = isConnected,
                    ConnectionType = connectionType,
                    NetworkAccess = networkAccess,
                    ConnectionProfiles = profiles,
                    LastUpdated = DateTime.UtcNow
                };

                // Add platform-specific properties if available
                if (enhancedInfo.ContainsKey("SignalStrength"))
                {
                    _logger.LogDebug("Platform signal strength: {SignalStrength}", enhancedInfo["SignalStrength"]);
                }

                if (enhancedInfo.ContainsKey("NetworkSpeed"))
                {
                    _logger.LogDebug("Estimated network speed: {NetworkSpeed}", enhancedInfo["NetworkSpeed"]);
                }

                _logger.LogDebug("MAUI connectivity info: Connected={IsConnected}, Type={ConnectionType}, Profiles=[{Profiles}]",
                    isConnected, connectionType, string.Join(", ", profiles));

                return connectivityInfo;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting MAUI connectivity info, falling back to base implementation");
                return await base.GetConnectivityInfoAsync();
            }
        }

        /// <summary>
        /// Tests connectivity using platform-specific methods combined with base implementation
        /// Overrides base implementation to provide platform-aware testing
        /// </summary>
        public override async Task<bool> TestConnectivityAsync(string host, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            try
            {
                // First check platform connectivity state
                if (!IsConnected())
                {
                    _logger.LogDebug("Platform reports no connectivity, skipping ping test to {Host}", host);
                    return false;
                }

                // Get current connection type for optimized testing
                var connectionType = GetConnectionType();
                _logger.LogDebug("Testing connectivity to {Host} via {ConnectionType}", host, connectionType);

                // Adjust timeout based on connection type
                var adjustedTimeout = connectionType switch
                {
                    "Cellular" => TimeSpan.FromMilliseconds(Math.Min(timeout.TotalMilliseconds * 1.5, 10000)), // Longer for cellular
                    "WiFi" => timeout,
                    "Ethernet" => TimeSpan.FromMilliseconds(Math.Max(timeout.TotalMilliseconds * 0.8, 1000)), // Shorter for ethernet
                    _ => timeout
                };

                // Use base implementation with adjusted timeout
                var result = await base.TestConnectivityAsync(host, adjustedTimeout, cancellationToken);

                _logger.LogDebug("Connectivity test to {Host} via {ConnectionType}: {Result}", host, connectionType, result);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error in platform-specific connectivity test to {Host}, falling back to base implementation", host);
                return await base.TestConnectivityAsync(host, timeout, cancellationToken);
            }
        }

        /// <summary>
        /// Gets enhanced platform-specific connectivity information
        /// </summary>
        private Dictionary<string, object> GetEnhancedPlatformInfo()
        {
            var info = new Dictionary<string, object>();

            try
            {
                // Get detailed connection profiles with additional metadata
                var profiles = _connectivity.ConnectionProfiles;
                info["DetailedProfiles"] = profiles.Select(p => new
                {
                    Type = p.ToString(),
                    IsAvailable = true,
                    Priority = GetConnectionPriority(p)
                }).ToArray();

                // Estimate network quality based on connection type
                if (profiles.Contains(ConnectionProfile.WiFi))
                {
                    info["NetworkSpeed"] = "High";
                    info["Reliability"] = "High";
                }
                else if (profiles.Contains(ConnectionProfile.Cellular))
                {
                    info["NetworkSpeed"] = "Variable";
                    info["Reliability"] = "Medium";
                }
                else if (profiles.Contains(ConnectionProfile.Ethernet))
                {
                    info["NetworkSpeed"] = "Very High";
                    info["Reliability"] = "Very High";
                }

                // Add platform capabilities
                info["SupportsRealTimeMonitoring"] = true;
                info["PlatformSpecific"] = true;
                info["CanDetectConnectionType"] = true;

            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error getting enhanced platform info");
            }

            return info;
        }

        /// <summary>
        /// Gets the priority of a connection profile for selection
        /// </summary>
        private int GetConnectionPriority(ConnectionProfile profile)
        {
            return profile switch
            {
                ConnectionProfile.Ethernet => 1,    // Highest priority
                ConnectionProfile.WiFi => 2,
                ConnectionProfile.Cellular => 3,
                ConnectionProfile.Bluetooth => 4,
                _ => 5                              // Lowest priority
            };
        }

        /// <summary>
        /// Handle system connectivity changes from MAUI platform
        /// Real-time connectivity detection with platform-specific details
        /// </summary>
        private void OnNativeConnectivityChanged(object sender, ConnectivityChangedEventArgs e)
        {
            try
            {
                var isConnected = _connectivity.NetworkAccess == NetworkAccess.Internet ||
                                 _connectivity.NetworkAccess == NetworkAccess.ConstrainedInternet;
                var connectionType = GetConnectionType();
                var profiles = _connectivity.ConnectionProfiles.Select(p => p.ToString()).ToArray();

                _logger.LogInformation("MAUI platform connectivity changed: Connected={IsConnected}, Type={ConnectionType}, Profiles=[{Profiles}]",
                    isConnected, connectionType, string.Join(", ", profiles));

                // Get enhanced connectivity info for the event
                var connectivityInfo = new ConnectivityInfo
                {
                    IsConnected = isConnected,
                    ConnectionType = connectionType,
                    NetworkAccess = _connectivity.NetworkAccess.ToString(),
                    ConnectionProfiles = profiles,
                    LastUpdated = DateTime.UtcNow
                };

                // Create enhanced event args with platform-specific information
                var eventArgs = new TDFConnectivityChangedEventArgs
                {
                    IsConnected = isConnected,
                    ConnectionType = connectionType,
                    Timestamp = DateTime.UtcNow,
                    PreviousState = _lastKnownState,
                    ConnectivityInfo = connectivityInfo
                };

                // Update state and raise events on main thread (critical for MAUI UI updates)
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        lock (_stateLock)
                        {
                            _lastKnownState = isConnected;
                        }

                        // Raise all events using base class protected methods
                        OnConnectivityChanged(eventArgs);

                        if (isConnected)
                        {
                            _logger.LogInformation("Network restored - Type: {ConnectionType}, Profiles: [{Profiles}]",
                                connectionType, string.Join(", ", profiles));
                            OnNetworkRestored();
                        }
                        else
                        {
                            _logger.LogWarning("Network lost - Previous type: {ConnectionType}", connectionType);
                            OnNetworkLost();
                        }
                    }
                    catch (Exception mainThreadEx)
                    {
                        _logger.LogError(mainThreadEx, "Error raising connectivity events on main thread");
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling MAUI connectivity change event");
            }
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
            if (profiles.Contains(ConnectionProfile.Bluetooth))
                return "Bluetooth";

            return "Unknown";
        }

        /// <summary>
        /// Cleanup resources including platform-specific subscriptions
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing && _isInitialized && _connectivity != null)
            {
                try
                {
                    _connectivity.ConnectivityChanged -= OnNativeConnectivityChanged;
                    _logger.LogDebug("MAUI ConnectivityService disposed");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error disposing MAUI ConnectivityService");
                }
            }

            base.Dispose(disposing);
        }
    }
}