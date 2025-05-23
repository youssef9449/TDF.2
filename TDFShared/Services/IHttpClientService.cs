using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TDFShared.DTOs.Common;

namespace TDFShared.Services
{
    /// <summary>
    /// Interface for HTTP client operations with retry logic, error handling, and authentication
    /// </summary>
    public interface IHttpClientService : IDisposable
    {
        /// <summary>
        /// Base URL for API requests
        /// </summary>
        string BaseUrl { get; set; }

        /// <summary>
        /// Request timeout in seconds
        /// </summary>
        int TimeoutSeconds { get; set; }

        /// <summary>
        /// Maximum number of retry attempts
        /// </summary>
        int MaxRetries { get; set; }

        /// <summary>
        /// Initial delay between retries
        /// </summary>
        TimeSpan InitialRetryDelay { get; set; }

        /// <summary>
        /// Event raised when authentication token needs to be refreshed
        /// </summary>
        event EventHandler<TokenRefreshEventArgs> TokenRefreshRequired;

        /// <summary>
        /// Event raised when network connectivity changes
        /// </summary>
        event EventHandler<NetworkStatusEventArgs> NetworkStatusChanged;

        /// <summary>
        /// Sets the authentication token for requests
        /// </summary>
        /// <param name="token">Bearer token</param>
        Task SetAuthenticationTokenAsync(string token);

        /// <summary>
        /// Clears the authentication token
        /// </summary>
        Task ClearAuthenticationTokenAsync();

        /// <summary>
        /// Performs a GET request and returns the deserialized response
        /// </summary>
        /// <typeparam name="T">Type to deserialize response to</typeparam>
        /// <param name="endpoint">API endpoint (relative to base URL)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Deserialized response data</returns>
        Task<T> GetAsync<T>(string endpoint, CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a GET request and returns the raw response content
        /// </summary>
        /// <param name="endpoint">API endpoint (relative to base URL)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Raw response content as string</returns>
        Task<string> GetRawAsync(string endpoint, CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a POST request with data and returns the deserialized response
        /// </summary>
        /// <typeparam name="TRequest">Type of request data</typeparam>
        /// <typeparam name="TResponse">Type of response data</typeparam>
        /// <param name="endpoint">API endpoint (relative to base URL)</param>
        /// <param name="data">Request data to serialize and send</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Deserialized response data</returns>
        Task<TResponse> PostAsync<TRequest, TResponse>(string endpoint, TRequest data, CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a POST request with data (fire-and-forget)
        /// </summary>
        /// <typeparam name="TRequest">Type of request data</typeparam>
        /// <param name="endpoint">API endpoint (relative to base URL)</param>
        /// <param name="data">Request data to serialize and send</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task PostAsync<TRequest>(string endpoint, TRequest data, CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a PUT request with data and returns the deserialized response
        /// </summary>
        /// <typeparam name="TRequest">Type of request data</typeparam>
        /// <typeparam name="TResponse">Type of response data</typeparam>
        /// <param name="endpoint">API endpoint (relative to base URL)</param>
        /// <param name="data">Request data to serialize and send</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Deserialized response data</returns>
        Task<TResponse> PutAsync<TRequest, TResponse>(string endpoint, TRequest data, CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a PUT request with data (fire-and-forget)
        /// </summary>
        /// <typeparam name="TRequest">Type of request data</typeparam>
        /// <param name="endpoint">API endpoint (relative to base URL)</param>
        /// <param name="data">Request data to serialize and send</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task PutAsync<TRequest>(string endpoint, TRequest data, CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a DELETE request
        /// </summary>
        /// <param name="endpoint">API endpoint (relative to base URL)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>HTTP response message</returns>
        Task<HttpResponseMessage> DeleteAsync(string endpoint, CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a PATCH request with data and returns the deserialized response
        /// </summary>
        /// <typeparam name="TRequest">Type of request data</typeparam>
        /// <typeparam name="TResponse">Type of response data</typeparam>
        /// <param name="endpoint">API endpoint (relative to base URL)</param>
        /// <param name="data">Request data to serialize and send</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Deserialized response data</returns>
        Task<TResponse> PatchAsync<TRequest, TResponse>(string endpoint, TRequest data, CancellationToken cancellationToken = default);

        /// <summary>
        /// Tests connectivity to the API
        /// </summary>
        /// <param name="healthCheckEndpoint">Health check endpoint (optional, defaults to "health")</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if API is reachable, false otherwise</returns>
        Task<bool> TestConnectivityAsync(string healthCheckEndpoint = "health", CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current network status
        /// </summary>
        /// <returns>Network status information</returns>
        Task<NetworkStatus> GetNetworkStatusAsync();

        /// <summary>
        /// Adds a custom header to all requests
        /// </summary>
        /// <param name="name">Header name</param>
        /// <param name="value">Header value</param>
        void AddDefaultHeader(string name, string value);

        /// <summary>
        /// Removes a custom header from all requests
        /// </summary>
        /// <param name="name">Header name</param>
        void RemoveDefaultHeader(string name);

        /// <summary>
        /// Gets request statistics for monitoring and debugging
        /// </summary>
        /// <returns>Request statistics</returns>
        HttpClientStatistics GetStatistics();
    }

    /// <summary>
    /// Event arguments for token refresh events
    /// </summary>
    public class TokenRefreshEventArgs : EventArgs
    {
        public string ExpiredToken { get; set; } = string.Empty;
        public DateTime ExpirationTime { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Event arguments for network status events
    /// </summary>
    public class NetworkStatusEventArgs : EventArgs
    {
        public bool IsConnected { get; set; }
        public string ConnectionType { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Network status information
    /// </summary>
    public class NetworkStatus
    {
        public bool IsConnected { get; set; }
        public bool IsApiReachable { get; set; }
        public string ConnectionType { get; set; } = string.Empty;
        public TimeSpan? Latency { get; set; }
        public DateTime LastChecked { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// HTTP client statistics for monitoring
    /// </summary>
    public class HttpClientStatistics
    {
        public int TotalRequests { get; set; }
        public int SuccessfulRequests { get; set; }
        public int FailedRequests { get; set; }
        public int RetriedRequests { get; set; }
        public TimeSpan AverageResponseTime { get; set; }
        public DateTime LastRequestTime { get; set; }
        public Dictionary<string, int> ErrorCounts { get; set; } = new Dictionary<string, int>();
        public Dictionary<int, int> StatusCodeCounts { get; set; } = new Dictionary<int, int>();
    }
}