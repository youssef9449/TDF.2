using System;

namespace TDFShared.Models
{
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
} 