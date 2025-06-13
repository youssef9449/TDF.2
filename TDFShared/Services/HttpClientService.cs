using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using Polly.CircuitBreaker;
using TDFShared.Models;
using TDFShared.Exceptions;
using TDFShared.Validation;
using TDFShared.Validation.Results;

namespace TDFShared.Services
{
    /// <summary>
    /// Extension methods for Polly Context
    /// </summary>
    public static class PollyContextExtensions
    {
        /// <summary>
        /// Adds a logger to the context
        /// </summary>
        /// <param name="context">The context</param>
        /// <param name="logger">The logger</param>
        /// <returns>The context with the logger</returns>
        public static Context WithLogger(this Context context, ILogger logger)
        {
            context["logger"] = logger;
            return context;
        }

        /// <summary>
        /// Adds an endpoint to the context
        /// </summary>
        /// <param name="context">The context</param>
        /// <param name="endpoint">The endpoint</param>
        /// <returns>The context with the endpoint</returns>
        public static Context WithEndpoint(this Context context, string endpoint)
        {
            context["endpoint"] = endpoint;
            return context;
        }
    }

    /// <summary>
    /// Shared HTTP client service with retry logic, error handling, and authentication
    /// Uses Polly for resilience patterns and provides comprehensive error handling
    /// </summary>
    public class HttpClientService : IHttpClientService, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<HttpClientService> _logger;
        private readonly IConnectivityService _connectivityService;
        private readonly IAuthService _authService;
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private readonly Dictionary<string, string> _defaultHeaders = new();
        private readonly Dictionary<string, string> _defaultQueryParams = new();
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly IAsyncPolicy<HttpResponseMessage> _combinedPolicy;
        private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;
        private readonly HttpClientStatistics _statistics;
        private readonly object _statisticsLock = new object();
        private bool _disposed;
        private readonly ConnectivityInfo _connectivityInfo;
        private int _timeoutSeconds = 30;
        private readonly int _maxRetries = 3;
        private readonly TimeSpan _initialRetryDelay = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Gets the HttpClient instance
        /// </summary>
        public HttpClient HttpClient => _httpClient;

        /// <summary>
        /// Gets the current connectivity information
        /// </summary>
        public ConnectivityInfo ConnectivityInfo => _connectivityInfo;

        /// <summary>
        /// Gets or sets the base URL for all HTTP requests
        /// </summary>
        public string BaseUrl { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the timeout in seconds for HTTP requests
        /// </summary>
        public int TimeoutSeconds 
        { 
            get => _timeoutSeconds;
            set 
            {
                _timeoutSeconds = value;
                _httpClient.Timeout = TimeSpan.FromSeconds(value);
            }
        }
        
        /// <summary>
        /// Gets or sets the maximum number of retry attempts for failed requests
        /// </summary>
        public int MaxRetries => _maxRetries;

        /// <summary>
        /// Gets or sets the initial delay before the first retry attempt
        /// </summary>
        public TimeSpan InitialRetryDelay => _initialRetryDelay;

        /// <summary>
        /// Event raised when a token refresh is required
        /// </summary>
        public event EventHandler<TokenRefreshEventArgs>? TokenRefreshNeeded;

        /// <summary>
        /// Event raised when a token is refreshed
        /// </summary>
        public event EventHandler<TokenRefreshEventArgs>? TokenRefreshCompleted;

        /// <summary>
        /// Event raised when a token refresh fails
        /// </summary>
        public event EventHandler<TokenRefreshEventArgs>? TokenRefreshFailed;

        /// <summary>
        /// Initializes a new instance of the HttpClientService class
        /// </summary>
        /// <param name="httpClient">The HttpClient instance to use</param>
        /// <param name="logger">The logger instance</param>
        /// <param name="connectivityService">The connectivity service</param>
        /// <param name="authService">The authentication service</param>
        public HttpClientService(
            HttpClient httpClient, 
            ILogger<HttpClientService> logger,
            IConnectivityService connectivityService,
            IAuthService authService)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _connectivityService = connectivityService ?? throw new ArgumentNullException(nameof(connectivityService));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));

            _statistics = new HttpClientStatistics();
            _defaultHeaders = new Dictionary<string, string>();
            _semaphore = new SemaphoreSlim(1, 1);

            // Use centralized JSON serialization options for consistency
            _jsonOptions = TDFShared.Helpers.JsonSerializationHelper.DefaultOptions;

            // Configure retry policy with exponential backoff
            _retryPolicy = Policy<HttpResponseMessage>
                .HandleResult(r => !r.IsSuccessStatusCode && IsRetryableStatusCode(r.StatusCode))
                .Or<HttpRequestException>()
                .Or<TaskCanceledException>()
                .Or<IOException>() // Handle IO exceptions that can occur during response reading
                .WaitAndRetryAsync(
                    retryCount: _maxRetries,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) + _initialRetryDelay,
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        _logger.LogWarning("Retry attempt {RetryCount} for {Endpoint} in {Delay}ms",
                            retryCount, context.TryGetValue("endpoint", out var endpointValue) ? endpointValue.ToString() : "unknown", timespan.TotalMilliseconds);

                        lock (_statisticsLock)
                        {
                            _statistics.RetriedRequests++;
                        }
                    });

            // Use retry policy directly
            _combinedPolicy = _retryPolicy;

            _logger.LogInformation("HttpClientService initialized with BaseUrl: {BaseUrl}, Timeout: {Timeout}s, MaxRetries: {MaxRetries}",
                BaseUrl, TimeoutSeconds, MaxRetries);

            _connectivityInfo = new ConnectivityInfo();
        }

        /// <summary>
        /// Sets the authentication token for subsequent requests
        /// </summary>
        /// <param name="token">The authentication token</param>
        public async Task SetAuthenticationTokenAsync(string token)
        {
            _logger?.LogInformation("HttpClientService: Setting Authorization header with token of length {Length}", token?.Length ?? 0);
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            await Task.CompletedTask;
        }

        /// <summary>
        /// Clears the authentication token from subsequent requests
        /// </summary>
        public async Task ClearAuthenticationTokenAsync()
        {
            _httpClient.DefaultRequestHeaders.Authorization = null;
            _logger.LogDebug("Authentication token cleared");
            await Task.CompletedTask;
        }

        /// <summary>
        /// Sends a GET request and deserializes the response
        /// </summary>
        /// <typeparam name="T">The type to deserialize the response to</typeparam>
        /// <param name="endpoint">The endpoint to send the request to</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The deserialized response</returns>
        public async Task<T> GetAsync<T>(string endpoint, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                var url = BuildUrl(endpoint);
                _logger.LogDebug("Sending GET request to {Url}", url);

                var response = await _httpClient.GetAsync(url, cancellationToken);
                return await ProcessResponseAsync<T>(response, endpoint, cancellationToken);
            }, endpoint);
        }

        /// <summary>
        /// Sends a GET request and returns the raw response string
        /// </summary>
        /// <param name="endpoint">The endpoint to send the request to</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The raw response string</returns>
        public async Task<string> GetRawAsync(string endpoint, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                var url = BuildUrl(endpoint);
                _logger.LogDebug("Sending GET request to {Url}", url);

                var response = await _httpClient.GetAsync(url, cancellationToken);
                await EnsureSuccessStatusCodeAsync(response, endpoint, cancellationToken);
                return await response.Content.ReadAsStringAsync(cancellationToken);
            }, endpoint);
        }

        /// <summary>
        /// Sends a POST request with data and deserializes the response
        /// </summary>
        /// <typeparam name="TRequest">The type of data to send</typeparam>
        /// <typeparam name="TResponse">The type to deserialize the response to</typeparam>
        /// <param name="endpoint">The endpoint to send the request to</param>
        /// <param name="data">The data to send</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The deserialized response</returns>
        public async Task<TResponse> PostAsync<TRequest, TResponse>(string endpoint, TRequest data, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                var url = BuildUrl(endpoint);
                _logger.LogDebug("Sending POST request to {Url}", url);

                var content = new StringContent(
                    JsonSerializer.Serialize(data, _jsonOptions),
                    Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PostAsync(url, content, cancellationToken);
                return await ProcessResponseAsync<TResponse>(response, endpoint, cancellationToken);
            }, endpoint);
        }

        /// <summary>
        /// Sends a POST request with data
        /// </summary>
        /// <typeparam name="TRequest">The type of data to send</typeparam>
        /// <param name="endpoint">The endpoint to send the request to</param>
        /// <param name="data">The data to send</param>
        /// <param name="cancellationToken">The cancellation token</param>
        public async Task PostAsync<TRequest>(string endpoint, TRequest data, CancellationToken cancellationToken = default)
        {
            await ExecuteWithRetryAsync(async () =>
            {
                var url = BuildUrl(endpoint);
                _logger.LogDebug("Sending POST request to {Url}", url);

                var content = new StringContent(
                    JsonSerializer.Serialize(data, _jsonOptions),
                    Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PostAsync(url, content, cancellationToken);
                await EnsureSuccessStatusCodeAsync(response, endpoint, cancellationToken);
                return true;
            }, endpoint);
        }

        /// <summary>
        /// Sends a PUT request with data and deserializes the response
        /// </summary>
        /// <typeparam name="TRequest">The type of data to send</typeparam>
        /// <typeparam name="TResponse">The type to deserialize the response to</typeparam>
        /// <param name="endpoint">The endpoint to send the request to</param>
        /// <param name="data">The data to send</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The deserialized response</returns>
        public async Task<TResponse> PutAsync<TRequest, TResponse>(string endpoint, TRequest data, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                var url = BuildUrl(endpoint);
                _logger.LogDebug("Sending PUT request to {Url}", url);

                var content = new StringContent(
                    JsonSerializer.Serialize(data, _jsonOptions),
                    Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PutAsync(url, content, cancellationToken);
                return await ProcessResponseAsync<TResponse>(response, endpoint, cancellationToken);
            }, endpoint);
        }

        /// <summary>
        /// Sends a PUT request with data
        /// </summary>
        /// <typeparam name="TRequest">The type of data to send</typeparam>
        /// <param name="endpoint">The endpoint to send the request to</param>
        /// <param name="data">The data to send</param>
        /// <param name="cancellationToken">The cancellation token</param>
        public async Task PutAsync<TRequest>(string endpoint, TRequest data, CancellationToken cancellationToken = default)
        {
            await ExecuteWithRetryAsync(async () =>
            {
                var url = BuildUrl(endpoint);
                _logger.LogDebug("Sending PUT request to {Url}", url);

                var content = new StringContent(
                    JsonSerializer.Serialize(data, _jsonOptions),
                    Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PutAsync(url, content, cancellationToken);
                await EnsureSuccessStatusCodeAsync(response, endpoint, cancellationToken);
                return true;
            }, endpoint);
        }

        /// <summary>
        /// Sends a DELETE request
        /// </summary>
        /// <param name="endpoint">The endpoint to send the request to</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The HTTP response message</returns>
        public async Task<HttpResponseMessage> DeleteAsync(string endpoint, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                var url = BuildUrl(endpoint);
                _logger.LogDebug("Sending DELETE request to {Url}", url);

                var response = await _httpClient.DeleteAsync(url, cancellationToken);
                await EnsureSuccessStatusCodeAsync(response, endpoint, cancellationToken);
                return response;
            }, endpoint);
        }

        /// <summary>
        /// Sends a PATCH request with data and deserializes the response
        /// </summary>
        /// <typeparam name="TRequest">The type of data to send</typeparam>
        /// <typeparam name="TResponse">The type to deserialize the response to</typeparam>
        /// <param name="endpoint">The endpoint to send the request to</param>
        /// <param name="data">The data to send</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The deserialized response</returns>
        public async Task<TResponse> PatchAsync<TRequest, TResponse>(string endpoint, TRequest data, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                var url = BuildUrl(endpoint);
                _logger.LogDebug("Sending PATCH request to {Url}", url);

                var content = new StringContent(
                    JsonSerializer.Serialize(data, _jsonOptions),
                    Encoding.UTF8,
                    "application/json");

                var request = new HttpRequestMessage(new HttpMethod("PATCH"), url)
                {
                    Content = content
                };

                var response = await _httpClient.SendAsync(request, cancellationToken);
                return await ProcessResponseAsync<TResponse>(response, endpoint, cancellationToken);
            }, endpoint);
        }

        /// <summary>
        /// Tests connectivity to the API
        /// </summary>
        /// <param name="healthCheckEndpoint">The health check endpoint to use</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>True if the API is accessible, false otherwise</returns>
        public async Task<bool> TestConnectivityAsync(string healthCheckEndpoint = "health", CancellationToken cancellationToken = default)
        {
            try
            {
                var url = BuildUrl(healthCheckEndpoint);
                _logger.LogDebug("Testing connectivity to {Url}", url);

                var response = await _httpClient.GetAsync(url, cancellationToken);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing connectivity to {Endpoint}", healthCheckEndpoint);
                return false;
            }
        }

        /// <summary>
        /// Gets the current connectivity information
        /// </summary>
        public async Task<ConnectivityInfo> GetConnectivityInfoAsync()
        {
            var info = new ConnectivityInfo
            {
                IsConnected = await TestConnectivityAsync(),
                LastUpdated = DateTime.UtcNow,
                ConnectionType = "Unknown",
                NetworkAccess = "Unknown",
                ConnectionProfiles = Array.Empty<string>()
            };

            try
            {
                var response = await _httpClient.GetAsync("health", HttpCompletionOption.ResponseHeadersRead);
                info.IsConnected = response.IsSuccessStatusCode;
                if (response.Headers.Date.HasValue)
                {
                    info.SignalStrength = (int)response.Headers.Date.Value.Subtract(DateTime.UtcNow).TotalMilliseconds;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get API connectivity info");
                info.IsConnected = false;
                info.SignalStrength = null;
            }

            return info;
        }

        /// <summary>
        /// Adds a default header to all requests
        /// </summary>
        /// <param name="name">The header name</param>
        /// <param name="value">The header value</param>
        public void AddDefaultHeader(string name, string value)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));

            _defaultHeaders[name] = value;
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(name, value);
        }

        /// <summary>
        /// Removes a default header from all requests
        /// </summary>
        /// <param name="name">The header name</param>
        public void RemoveDefaultHeader(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));

            _defaultHeaders.Remove(name);
            _httpClient.DefaultRequestHeaders.Remove(name);
        }

        /// <summary>
        /// Gets the current HTTP client statistics
        /// </summary>
        /// <returns>The current statistics</returns>
        public HttpClientStatistics GetStatistics()
        {
            return _statistics.Clone();
        }

        /// <summary>
        /// Disposes the HTTP client service
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the HTTP client service
        /// </summary>
        /// <param name="disposing">Whether the service is being disposed</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _httpClient.Dispose();
                    _semaphore.Dispose();
                }

                _disposed = true;
            }
        }

        /// <summary>
        /// Validates the endpoint
        /// </summary>
        /// <param name="endpoint">The endpoint to validate</param>
        private void ValidateEndpoint(string endpoint)
        {
            if (string.IsNullOrEmpty(endpoint))
                throw new ArgumentNullException(nameof(endpoint));

            if (string.IsNullOrEmpty(BaseUrl))
                throw new InvalidOperationException("BaseUrl must be set before making requests.");
        }

        /// <summary>
        /// Validates the request data
        /// </summary>
        /// <typeparam name="T">The type of data to validate</typeparam>
        /// <param name="endpoint">The endpoint to validate</param>
        /// <param name="data">The data to validate</param>
        private void ValidateRequest<T>(string endpoint, T? data) where T : class
        {
            ValidateEndpoint(endpoint);

            if (data == null)
                return;

            if (data is IValidatable validatable)
            {
                var validationResult = validatable.Validate();
                if (!validationResult.IsValid)
                {
                    var errorMessage = validationResult.Errors.Count > 0
                        ? string.Join("; ", validationResult.Errors)
                        : "Request validation failed.";
                    throw new TDFShared.Exceptions.ValidationException(errorMessage);
                }
            }

            if (!IsValidJson(data))
                throw new TDFShared.Exceptions.ValidationException("Request data is not valid JSON.");
        }

        /// <summary>
        /// Checks if the data is valid JSON
        /// </summary>
        /// <param name="data">The data to check</param>
        /// <returns>True if the data is valid JSON, false otherwise</returns>
        private bool IsValidJson(object data)
        {
            try
            {
                JsonSerializer.Serialize(data, _jsonOptions);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error serializing request data to JSON");
                return false;
            }
        }

        /// <summary>
        /// Executes an operation with retry logic
        /// </summary>
        /// <typeparam name="T">The type of result</typeparam>
        /// <param name="operation">The operation to execute</param>
        /// <param name="endpoint">The endpoint being accessed</param>
        /// <returns>The operation result</returns>
        private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, string endpoint)
        {
            var retryCount = 0;
            var delay = InitialRetryDelay;

            while (true)
            {
                try
                {
                    return await operation();
                }
                catch (Exception ex) when (IsRetryableException(ex) && retryCount < MaxRetries)
                {
                    retryCount++;
                    _logger.LogWarning(ex, "Retry {RetryCount} of {MaxRetries} for {Endpoint}", retryCount, MaxRetries, endpoint);
                    await Task.Delay(delay);
                    delay *= 2; // Exponential backoff
                }
            }
        }

        /// <summary>
        /// Processes an HTTP response
        /// </summary>
        /// <typeparam name="T">The type of response to deserialize</typeparam>
        /// <param name="response">The HTTP response</param>
        /// <param name="endpoint">The endpoint that was called</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The deserialized response</returns>
        private async Task<T> ProcessResponseAsync<T>(HttpResponseMessage response, string endpoint, CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                await EnsureSuccessStatusCodeAsync(response, endpoint, cancellationToken);

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                if (string.IsNullOrEmpty(content))
                    return default!;

                try
                {
                    var result = JsonSerializer.Deserialize<T>(content, _jsonOptions) ?? throw new JsonException("Deserialized result is null");
                    stopwatch.Stop();
                    UpdateStatistics(true, stopwatch.Elapsed, false);
                    return result;
                }
                catch (JsonException)
                {
                    _logger.LogError("Error deserializing response from {Endpoint}", endpoint);
                    throw new HttpRequestException($"Error deserializing response from {endpoint}");
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                UpdateStatistics(false, stopwatch.Elapsed, false);
                throw;
            }
        }

        /// <summary>
        /// Ensures the HTTP response has a success status code
        /// </summary>
        /// <param name="response">The HTTP response</param>
        /// <param name="endpoint">The endpoint that was accessed</param>
        /// <param name="cancellationToken">The cancellation token</param>
        private async Task EnsureSuccessStatusCodeAsync(HttpResponseMessage response, string endpoint, CancellationToken cancellationToken = default)
        {
            if (response.IsSuccessStatusCode)
                return;

            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var errorMessage = GetFriendlyErrorMessage(response.StatusCode, errorContent);

            switch (response.StatusCode)
            {
                case HttpStatusCode.Unauthorized:
                    _logger.LogWarning("Unauthorized access to {Endpoint}", endpoint);
                    TokenRefreshNeeded?.Invoke(this, new TokenRefreshEventArgs());
                    throw new UnauthorizedAccessException("You don't have permission to perform this action.");

                case HttpStatusCode.Forbidden:
                    _logger.LogWarning("Forbidden access to {Endpoint}", endpoint);
                    throw new UnauthorizedAccessException("You don't have permission to perform this action.");

                case HttpStatusCode.NotFound:
                    _logger.LogWarning("Resource not found at {Endpoint}", endpoint);
                    throw new InvalidOperationException($"The requested resource at {endpoint} was not found.");

                case HttpStatusCode.BadRequest:
                    _logger.LogWarning("Bad request to {Endpoint}: {Error}", endpoint, errorMessage);
                    throw new TDFShared.Exceptions.ValidationException(errorMessage);

                case HttpStatusCode.InternalServerError:
                    _logger.LogError("Server error at {Endpoint}: {Error}", endpoint, errorMessage);
                    throw new InvalidOperationException("An unexpected error occurred. Please try again later.");

                case HttpStatusCode.RequestTimeout:
                    _logger.LogWarning("Request timeout at {Endpoint}", endpoint);
                    throw new TimeoutException("The request timed out. Please try again.");

                case HttpStatusCode.ServiceUnavailable:
                    _logger.LogWarning("Service unavailable at {Endpoint}", endpoint);
                    throw new InvalidOperationException("The service is currently unavailable. Please try again later.");

                default:
                    _logger.LogError("HTTP error {StatusCode} at {Endpoint}: {Error}", response.StatusCode, endpoint, errorMessage);
                    throw new InvalidOperationException($"An error occurred while accessing {endpoint}. Please try again later.");
            }
        }

        /// <summary>
        /// Builds the full URL for a request
        /// </summary>
        /// <param name="endpoint">The endpoint to build the URL for</param>
        /// <returns>The full URL</returns>
        private string BuildUrl(string endpoint)
        {
            if (string.IsNullOrEmpty(endpoint))
                throw new ArgumentNullException(nameof(endpoint));

            if (string.IsNullOrEmpty(BaseUrl))
                throw new InvalidOperationException("BaseUrl must be set before making requests.");

            var baseUrl = BaseUrl.TrimEnd('/');
            var endpointPath = endpoint.TrimStart('/');

            return $"{baseUrl}/{endpointPath}";
        }

        /// <summary>
        /// Checks if a status code is retryable
        /// </summary>
        /// <param name="statusCode">The status code to check</param>
        /// <returns>True if the status code is retryable, false otherwise</returns>
        private static bool IsRetryableStatusCode(HttpStatusCode statusCode)
        {
            return statusCode switch
            {
                HttpStatusCode.RequestTimeout or
                HttpStatusCode.TooManyRequests or
                HttpStatusCode.InternalServerError or
                HttpStatusCode.BadGateway or
                HttpStatusCode.ServiceUnavailable or
                HttpStatusCode.GatewayTimeout => true,
                _ => false
            };
        }

        /// <summary>
        /// Gets a friendly error message for a status code
        /// </summary>
        /// <param name="statusCode">The status code</param>
        /// <param name="errorContent">The error content</param>
        /// <returns>A friendly error message</returns>
        private string GetFriendlyErrorMessage(HttpStatusCode statusCode, string errorContent)
        {
            try
            {
                if (!string.IsNullOrEmpty(errorContent))
                {
                    var errorResponse = JsonSerializer.Deserialize<ApiResponseBase>(errorContent, _jsonOptions);
                    if (errorResponse != null)
                    {
                        // First try to get the error message
                        if (!string.IsNullOrEmpty(errorResponse.ErrorMessage))
                            return errorResponse.ErrorMessage;

                        // Then try to get validation errors
                        if (errorResponse.ValidationErrors != null && errorResponse.ValidationErrors.Count > 0)
                            return string.Join("; ", errorResponse.ValidationErrors.Select(e => e.Value.TrimEnd('.')));

                        // Finally try to get the message
                        if (!string.IsNullOrEmpty(errorResponse.Message))
                            return errorResponse.Message;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing error response");
            }

            return GetDefaultErrorMessage(statusCode);
        }

        /// <summary>
        /// Gets the default error message for a status code
        /// </summary>
        /// <param name="statusCode">The status code</param>
        /// <returns>The default error message</returns>
        private static string GetDefaultErrorMessage(HttpStatusCode statusCode)
        {
            return statusCode switch
            {
                HttpStatusCode.BadRequest => "The request was invalid.",
                HttpStatusCode.Unauthorized => "You are not authorized to access this resource.",
                HttpStatusCode.Forbidden => "You do not have permission to access this resource.",
                HttpStatusCode.NotFound => "The requested resource was not found.",
                HttpStatusCode.RequestTimeout => "The request timed out.",
                HttpStatusCode.InternalServerError => "An internal server error occurred.",
                HttpStatusCode.BadGateway => "A bad gateway error occurred.",
                HttpStatusCode.ServiceUnavailable => "The service is currently unavailable.",
                HttpStatusCode.GatewayTimeout => "The gateway timed out.",
                _ => $"An error occurred with status code {statusCode}."
            };
        }

        /// <summary>
        /// Updates the statistics with the result of a request
        /// </summary>
        /// <param name="success">Whether the request was successful</param>
        /// <param name="responseTime">The response time</param>
        /// <param name="isRetry">Whether this was a retry</param>
        private void UpdateStatistics(bool success, TimeSpan responseTime, bool isRetry = false)
        {
            lock (_statisticsLock)
            {
                _statistics.TotalRequests++;
                _statistics.TotalResponseTime += (long)responseTime.TotalMilliseconds;

                if (success)
                {
                    _statistics.SuccessfulRequests++;
                }
                else
                {
                    _statistics.FailedRequests++;
                }

                if (isRetry)
                {
                    _statistics.RetriedRequests++;
                }

                if (responseTime.TotalMilliseconds > _statistics.MaxResponseTime)
                {
                    _statistics.MaxResponseTime = (long)responseTime.TotalMilliseconds;
                }

                if (responseTime.TotalMilliseconds < _statistics.MinResponseTime || _statistics.MinResponseTime == long.MaxValue)
                {
                    _statistics.MinResponseTime = (long)responseTime.TotalMilliseconds;
                }
            }
        }

        /// <summary>
        /// Checks if an exception is a network error
        /// </summary>
        /// <param name="ex">The exception to check</param>
        /// <returns>True if the exception is a network error, false otherwise</returns>
        private bool IsNetworkError(Exception ex)
        {
            return ex switch
            {
                HttpRequestException or
                SocketException or
                IOException or
                TaskCanceledException => true,
                _ => false
            };
        }

        /// <summary>
        /// Checks if an exception is retryable
        /// </summary>
        /// <param name="ex">The exception to check</param>
        /// <returns>True if the exception is retryable, false otherwise</returns>
        private bool IsRetryableException(Exception ex)
        {
            return ex switch
            {
                HttpRequestException or
                SocketException or
                IOException or
                TaskCanceledException => true,
                _ => false
            };
        }

        /// <summary>
        /// Raises the token refreshed event
        /// </summary>
        /// <param name="accessToken">The access token</param>
        /// <param name="refreshToken">The refresh token</param>
        /// <param name="expiresAt">The token expiration time</param>
        protected virtual void OnTokenRefreshed(string accessToken, string refreshToken, DateTime expiresAt)
        {
            TokenRefreshCompleted?.Invoke(this, new TokenRefreshEventArgs(accessToken, refreshToken, expiresAt));
        }

        /// <summary>
        /// Raises the token refresh failed event
        /// </summary>
        /// <param name="error">The error message</param>
        protected virtual void OnTokenRefreshFailed(string error)
        {
            TokenRefreshFailed?.Invoke(this, new TokenRefreshEventArgs(error, string.Empty, DateTime.UtcNow));
        }

        /// <summary>
        /// Sends an HTTP request and returns the response
        /// </summary>
        /// <param name="request">The HTTP request to send</param>
        /// <param name="cancellationToken">A token to cancel the operation</param>
        /// <returns>The HTTP response message</returns>
        public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
        {
            ValidateEndpoint(request.RequestUri?.ToString() ?? string.Empty);
            return await ExecuteWithRetryAsync(async () => await _httpClient.SendAsync(request, cancellationToken), request.RequestUri?.ToString() ?? string.Empty);
        }

        /// <summary>
        /// Sends an HTTP request and deserializes the response to the specified type
        /// </summary>
        /// <typeparam name="T">The type to deserialize the response to</typeparam>
        /// <param name="request">The HTTP request to send</param>
        /// <param name="cancellationToken">A token to cancel the operation</param>
        /// <returns>The deserialized response</returns>
        public async Task<T?> SendAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken = default)
        {
            ValidateEndpoint(request.RequestUri?.ToString() ?? string.Empty);
            var response = await SendAsync(request, cancellationToken);
            return await ProcessResponseAsync<T>(response, request.RequestUri?.ToString() ?? string.Empty, cancellationToken);
        }

        /// <summary>
        /// Sends an HTTP request and returns the response wrapped in an ApiResponseBase
        /// </summary>
        /// <typeparam name="T">The type of data in the response</typeparam>
        /// <param name="request">The HTTP request to send</param>
        /// <param name="cancellationToken">A token to cancel the operation</param>
        /// <returns>The response wrapped in an ApiResponseBase</returns>
        public async Task<ApiResponseBase<T>?> SendWithResponseAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken = default)
        {
            ValidateEndpoint(request.RequestUri?.ToString() ?? string.Empty);
            var response = await SendAsync(request, cancellationToken);
            var result = await ProcessResponseAsync<ApiResponseBase<T>>(response, request.RequestUri?.ToString() ?? string.Empty, cancellationToken);
            return result;
        }
    }
}