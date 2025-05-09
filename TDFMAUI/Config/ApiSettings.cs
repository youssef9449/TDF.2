using System;

namespace TDFMAUI.Config
{
    /// <summary>
    /// API settings for use with the options pattern
    /// </summary>
    public class ApiSettings
    {
        /// <summary>
        /// Base URL for the API
        /// </summary>
        public string BaseUrl { get; set; }
        
        /// <summary>
        /// WebSocket URL
        /// </summary>
        public string WebSocketUrl { get; set; }
        
        /// <summary>
        /// Whether development mode is enabled
        /// </summary>
        public bool DevelopmentMode { get; set; }
        
        /// <summary>
        /// Timeout in seconds
        /// </summary>
        public int Timeout { get; set; } = 30;
        
        /// <summary>
        /// Maximum number of retries
        /// </summary>
        public int MaxRetries { get; set; } = 3;
        
        /// <summary>
        /// Retry delay in milliseconds
        /// </summary>
        public int RetryDelay { get; set; } = 1000;
        
        /// <summary>
        /// Retry multiplier for exponential backoff
        /// </summary>
        public double RetryMultiplier { get; set; } = 2.0;
    }
} 