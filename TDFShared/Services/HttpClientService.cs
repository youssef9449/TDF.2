using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.Logging;
using TDFShared.Http;
using TDFShared.Models;
using TDFShared.Exceptions;
using TDFShared.Validation;

namespace TDFShared.Services
{
    /// <summary>
    /// Shared HTTP client service that orchestrates HTTP verbs and response
    /// processing on top of the delegating-handler pipeline. Authentication
    /// (<see cref="AuthenticationHeaderHandler"/>), retry policy
    /// (<see cref="PollyRetryingHandler"/>), and telemetry
    /// (<see cref="HttpTelemetryHandler"/>) live as individual handlers so this
    /// service only owns serialization and status-code translation.
    /// </summary>
    public class HttpClientService : IHttpClientService, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<HttpClientService> _logger;
        private readonly IConnectivityService _connectivityService;
        private readonly IAuthTokenStore _tokenStore;
        private readonly IHttpTelemetry _telemetry;
        private readonly Dictionary<string, string> _defaultHeaders = new();
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly ConnectivityInfo _connectivityInfo = new();
        private bool _disposed;
        private int _timeoutSeconds = 30;

        // Advisory values; actual retry behaviour is owned by PollyRetryingHandler.
        private const int MaxRetryAttempts = 3;
        private static readonly TimeSpan InitialRetryBackoff = TimeSpan.FromSeconds(1);

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
        /// Gets the maximum number of retry attempts performed by the retry handler.
        /// </summary>
        public int MaxRetries => MaxRetryAttempts;

        /// <summary>
        /// Gets the base backoff delay used by the retry handler.
        /// </summary>
        public TimeSpan InitialRetryDelay => InitialRetryBackoff;

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
        /// <param name="tokenStore">Bearer-token store shared with <see cref="AuthenticationHeaderHandler"/>.</param>
        /// <param name="telemetry">Aggregate telemetry sink shared with <see cref="HttpTelemetryHandler"/>.</param>
        public HttpClientService(
            HttpClient httpClient,
            ILogger<HttpClientService> logger,
            IConnectivityService connectivityService,
            IAuthTokenStore tokenStore,
            IHttpTelemetry telemetry)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _connectivityService = connectivityService ?? throw new ArgumentNullException(nameof(connectivityService));
            _tokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));
            _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));

            _jsonOptions = TDFShared.Helpers.JsonSerializationHelper.DefaultOptions;

            _logger.LogInformation(
                "HttpClientService initialized with BaseUrl: {BaseUrl}, Timeout: {Timeout}s, MaxRetries: {MaxRetries}",
                BaseUrl, TimeoutSeconds, MaxRetries);
        }

        /// <summary>
        /// Sets the authentication token for subsequent requests
        /// </summary>
        /// <param name="token">The authentication token</param>
        public Task SetAuthenticationTokenAsync(string token)
        {
            _logger.LogInformation(
                "HttpClientService: Setting Authorization header with token of length {Length}",
                token?.Length ?? 0);
            _tokenStore.SetToken(token);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Clears the authentication token from subsequent requests
        /// </summary>
        public Task ClearAuthenticationTokenAsync()
        {
            _tokenStore.Clear();
            _logger.LogDebug("Authentication token cleared");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Sends a GET request and deserializes the response
        /// </summary>
        public async Task<T> GetAsync<T>(string endpoint, CancellationToken cancellationToken = default)
        {
            var url = BuildUrl(endpoint);
            _logger.LogDebug("Sending GET request to {Url}", url);

            var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            return await ProcessResponseAsync<T>(response, endpoint, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends a GET request and returns the raw response string
        /// </summary>
        public async Task<string> GetRawAsync(string endpoint, CancellationToken cancellationToken = default)
        {
            var url = BuildUrl(endpoint);
            _logger.LogDebug("Sending GET request to {Url}", url);

            var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            await EnsureSuccessStatusCodeAsync(response, endpoint, cancellationToken).ConfigureAwait(false);
            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends a POST request with data and deserializes the response
        /// </summary>
        public async Task<TResponse> PostAsync<TRequest, TResponse>(string endpoint, TRequest data, CancellationToken cancellationToken = default)
        {
            var url = BuildUrl(endpoint);
            _logger.LogDebug("Sending POST request to {Url}", url);

            using var content = SerializeToJson(data);
            var response = await _httpClient.PostAsync(url, content, cancellationToken).ConfigureAwait(false);
            return await ProcessResponseAsync<TResponse>(response, endpoint, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends a POST request with data
        /// </summary>
        public async Task PostAsync<TRequest>(string endpoint, TRequest data, CancellationToken cancellationToken = default)
        {
            var url = BuildUrl(endpoint);
            _logger.LogDebug("Sending POST request to {Url}", url);

            using var content = SerializeToJson(data);
            var response = await _httpClient.PostAsync(url, content, cancellationToken).ConfigureAwait(false);
            await EnsureSuccessStatusCodeAsync(response, endpoint, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends a PUT request with data and deserializes the response
        /// </summary>
        public async Task<TResponse> PutAsync<TRequest, TResponse>(string endpoint, TRequest data, CancellationToken cancellationToken = default)
        {
            var url = BuildUrl(endpoint);
            _logger.LogDebug("Sending PUT request to {Url}", url);

            using var content = SerializeToJson(data);
            var response = await _httpClient.PutAsync(url, content, cancellationToken).ConfigureAwait(false);
            return await ProcessResponseAsync<TResponse>(response, endpoint, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends a PUT request with data
        /// </summary>
        public async Task PutAsync<TRequest>(string endpoint, TRequest data, CancellationToken cancellationToken = default)
        {
            var url = BuildUrl(endpoint);
            _logger.LogDebug("Sending PUT request to {Url}", url);

            using var content = SerializeToJson(data);
            var response = await _httpClient.PutAsync(url, content, cancellationToken).ConfigureAwait(false);
            await EnsureSuccessStatusCodeAsync(response, endpoint, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends a DELETE request
        /// </summary>
        public async Task<HttpResponseMessage> DeleteAsync(string endpoint, CancellationToken cancellationToken = default)
        {
            var url = BuildUrl(endpoint);
            _logger.LogDebug("Sending DELETE request to {Url}", url);

            var response = await _httpClient.DeleteAsync(url, cancellationToken).ConfigureAwait(false);
            await EnsureSuccessStatusCodeAsync(response, endpoint, cancellationToken).ConfigureAwait(false);
            return response;
        }

        /// <summary>
        /// Sends a PATCH request with data and deserializes the response
        /// </summary>
        public async Task<TResponse> PatchAsync<TRequest, TResponse>(string endpoint, TRequest data, CancellationToken cancellationToken = default)
        {
            var url = BuildUrl(endpoint);
            _logger.LogDebug("Sending PATCH request to {Url}", url);

            using var content = SerializeToJson(data);
            using var request = new HttpRequestMessage(new HttpMethod("PATCH"), url) { Content = content };
            var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return await ProcessResponseAsync<TResponse>(response, endpoint, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Tests connectivity to the API
        /// </summary>
        public async Task<bool> TestConnectivityAsync(string healthCheckEndpoint = "health", CancellationToken cancellationToken = default)
        {
            try
            {
                var url = BuildUrl(healthCheckEndpoint);
                _logger.LogDebug("Testing connectivity to {Url}", url);

                var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
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
                IsConnected = await TestConnectivityAsync().ConfigureAwait(false),
                LastUpdated = DateTime.UtcNow,
                ConnectionType = "Unknown",
                NetworkAccess = "Unknown",
                ConnectionProfiles = Array.Empty<string>()
            };

            try
            {
                var response = await _httpClient.GetAsync("health", HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
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
        public HttpClientStatistics GetStatistics() => _telemetry.GetSnapshot();

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
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing)
            {
                _httpClient.Dispose();
            }
            _disposed = true;
        }

        private StringContent SerializeToJson<T>(T data) =>
            new(JsonSerializer.Serialize(data, _jsonOptions), Encoding.UTF8, "application/json");

        private void ValidateEndpoint(string endpoint)
        {
            if (string.IsNullOrEmpty(endpoint))
                throw new ArgumentNullException(nameof(endpoint));

            if (string.IsNullOrEmpty(BaseUrl))
                throw new InvalidOperationException("BaseUrl must be set before making requests.");
        }

        private async Task<T> ProcessResponseAsync<T>(HttpResponseMessage response, string endpoint, CancellationToken cancellationToken)
        {
            await EnsureSuccessStatusCodeAsync(response, endpoint, cancellationToken).ConfigureAwait(false);

            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrEmpty(content))
                return default!;

            try
            {
                return JsonSerializer.Deserialize<T>(content, _jsonOptions)
                    ?? throw new JsonException("Deserialized result is null");
            }
            catch (JsonException)
            {
                _logger.LogError("Error deserializing response from {Endpoint}", endpoint);
                throw new HttpRequestException($"Error deserializing response from {endpoint}");
            }
        }

        private async Task EnsureSuccessStatusCodeAsync(HttpResponseMessage response, string endpoint, CancellationToken cancellationToken = default)
        {
            if (response.IsSuccessStatusCode)
                return;

            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
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

        private string BuildUrl(string endpoint)
        {
            ValidateEndpoint(endpoint);

            var baseUrl = BaseUrl.TrimEnd('/');
            var endpointPath = endpoint.TrimStart('/');
            return $"{baseUrl}/{endpointPath}";
        }

        private string GetFriendlyErrorMessage(HttpStatusCode statusCode, string errorContent)
        {
            try
            {
                if (!string.IsNullOrEmpty(errorContent))
                {
                    var errorResponse = JsonSerializer.Deserialize<ApiResponseBase>(errorContent, _jsonOptions);
                    if (errorResponse != null)
                    {
                        if (!string.IsNullOrEmpty(errorResponse.ErrorMessage))
                            return errorResponse.ErrorMessage;

                        if (errorResponse.ValidationErrors != null && errorResponse.ValidationErrors.Count > 0)
                            return string.Join("; ", errorResponse.ValidationErrors.Select(e => e.Value.TrimEnd('.')));

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

        private static string GetDefaultErrorMessage(HttpStatusCode statusCode) => statusCode switch
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

        /// <summary>
        /// Raises the token refreshed event
        /// </summary>
        protected virtual void OnTokenRefreshed(string accessToken, string refreshToken, DateTime expiresAt)
        {
            TokenRefreshCompleted?.Invoke(this, new TokenRefreshEventArgs(accessToken, refreshToken, expiresAt));
        }

        /// <summary>
        /// Raises the token refresh failed event
        /// </summary>
        protected virtual void OnTokenRefreshFailed(string error)
        {
            TokenRefreshFailed?.Invoke(this, new TokenRefreshEventArgs(error, string.Empty, DateTime.UtcNow));
        }

        /// <summary>
        /// Sends an HTTP request and returns the response
        /// </summary>
        public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
        {
            ValidateEndpoint(request.RequestUri?.ToString() ?? string.Empty);
            return await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends an HTTP request and deserializes the response
        /// </summary>
        public async Task<T?> SendAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken = default)
        {
            ValidateEndpoint(request.RequestUri?.ToString() ?? string.Empty);
            var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
            return await ProcessResponseAsync<T>(response, request.RequestUri?.ToString() ?? string.Empty, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Sends an HTTP request and returns the response wrapped in an ApiResponseBase
        /// </summary>
        public async Task<ApiResponseBase<T>?> SendWithResponseAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken = default)
        {
            ValidateEndpoint(request.RequestUri?.ToString() ?? string.Empty);
            var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
            return await ProcessResponseAsync<ApiResponseBase<T>>(response, request.RequestUri?.ToString() ?? string.Empty, cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
