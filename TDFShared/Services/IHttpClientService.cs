using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TDFShared.Models;

namespace TDFShared.Services
{
    /// <summary>
    /// Interface for HTTP client service with enhanced features
    /// </summary>
    public interface IHttpClientService : IDisposable
    {
        /// <summary>
        /// Gets the underlying HttpClient instance
        /// </summary>
        HttpClient HttpClient { get; }

        /// <summary>
        /// Gets the current connectivity information
        /// </summary>
        ConnectivityInfo ConnectivityInfo { get; }

        /// <summary>
        /// Gets the base URL for API requests
        /// </summary>
        string BaseUrl { get; set; }

        /// <summary>
        /// Gets or sets the timeout in seconds for HTTP requests
        /// </summary>
        int TimeoutSeconds { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of retry attempts for failed requests
        /// </summary>
        int MaxRetries { get; }

        /// <summary>
        /// Gets or sets the initial delay before the first retry attempt
        /// </summary>
        TimeSpan InitialRetryDelay { get; }

        /// <summary>
        /// Event raised when a token refresh is required
        /// </summary>
        event EventHandler<TokenRefreshEventArgs>? TokenRefreshNeeded;

        /// <summary>
        /// Event raised when a token is refreshed
        /// </summary>
        event EventHandler<TokenRefreshEventArgs>? TokenRefreshCompleted;

        /// <summary>
        /// Event raised when a token refresh fails
        /// </summary>
        event EventHandler<TokenRefreshEventArgs>? TokenRefreshFailed;

        /// <summary>
        /// Sets the authentication token for requests
        /// </summary>
        /// <param name="token">The authentication token</param>
        Task SetAuthenticationTokenAsync(string token);

        /// <summary>
        /// Clears the authentication token
        /// </summary>
        Task ClearAuthenticationTokenAsync();

        /// <summary>
        /// Performs a GET request
        /// </summary>
        /// <typeparam name="T">The type of response</typeparam>
        /// <param name="endpoint">The endpoint to request</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The response data</returns>
        Task<T> GetAsync<T>(string endpoint, CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a GET request and returns the raw response
        /// </summary>
        /// <param name="endpoint">The endpoint to request</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The raw response</returns>
        Task<string> GetRawAsync(string endpoint, CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a POST request
        /// </summary>
        /// <typeparam name="TRequest">The type of request data</typeparam>
        /// <typeparam name="TResponse">The type of response data</typeparam>
        /// <param name="endpoint">The endpoint to request</param>
        /// <param name="data">The request data</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The response data</returns>
        Task<TResponse> PostAsync<TRequest, TResponse>(string endpoint, TRequest data, CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a POST request without response data
        /// </summary>
        /// <typeparam name="TRequest">The type of request data</typeparam>
        /// <param name="endpoint">The endpoint to request</param>
        /// <param name="data">The request data</param>
        /// <param name="cancellationToken">The cancellation token</param>
        Task PostAsync<TRequest>(string endpoint, TRequest data, CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a PUT request
        /// </summary>
        /// <typeparam name="TRequest">The type of request data</typeparam>
        /// <typeparam name="TResponse">The type of response data</typeparam>
        /// <param name="endpoint">The endpoint to request</param>
        /// <param name="data">The request data</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The response data</returns>
        Task<TResponse> PutAsync<TRequest, TResponse>(string endpoint, TRequest data, CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a PUT request without response data
        /// </summary>
        /// <typeparam name="TRequest">The type of request data</typeparam>
        /// <param name="endpoint">The endpoint to request</param>
        /// <param name="data">The request data</param>
        /// <param name="cancellationToken">The cancellation token</param>
        Task PutAsync<TRequest>(string endpoint, TRequest data, CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a DELETE request
        /// </summary>
        /// <param name="endpoint">The endpoint to request</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The response message</returns>
        Task<HttpResponseMessage> DeleteAsync(string endpoint, CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a PATCH request
        /// </summary>
        /// <typeparam name="TRequest">The type of request data</typeparam>
        /// <typeparam name="TResponse">The type of response data</typeparam>
        /// <param name="endpoint">The endpoint to request</param>
        /// <param name="data">The request data</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The response data</returns>
        Task<TResponse> PatchAsync<TRequest, TResponse>(string endpoint, TRequest data, CancellationToken cancellationToken = default);

        /// <summary>
        /// Tests the connectivity to the API
        /// </summary>
        /// <param name="healthCheckEndpoint">The health check endpoint to use</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>True if the API is reachable, false otherwise</returns>
        Task<bool> TestConnectivityAsync(string healthCheckEndpoint = "health", CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current connectivity information
        /// </summary>
        Task<ConnectivityInfo> GetConnectivityInfoAsync();

        /// <summary>
        /// Adds a default header to all requests
        /// </summary>
        /// <param name="name">The header name</param>
        /// <param name="value">The header value</param>
        void AddDefaultHeader(string name, string value);

        /// <summary>
        /// Removes a default header from all requests
        /// </summary>
        /// <param name="name">The header name</param>
        void RemoveDefaultHeader(string name);

        /// <summary>
        /// Sends an HTTP request and returns the response
        /// </summary>
        /// <param name="request">The HTTP request to send</param>
        /// <param name="cancellationToken">A token to cancel the operation</param>
        /// <returns>The HTTP response message</returns>
        Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends an HTTP request with retry policy, logging, and response deserialization
        /// </summary>
        Task<T?> SendAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends an HTTP request with retry policy, logging, and response deserialization
        /// </summary>
        Task<ApiResponseBase<T>?> SendWithResponseAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken = default);
    }
}