using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using TDFShared.DTOs.Common;
using TDFShared.Exceptions;

namespace TDFShared.Services
{
    /// <summary>
    /// Shared HTTP client service with retry logic, error handling, and authentication
    /// Uses Polly for resilience patterns and provides comprehensive error handling
    /// </summary>
    public class HttpClientService : IHttpClientService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<HttpClientService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly HttpClientStatistics _statistics;
        private readonly object _statisticsLock = new object();
        private readonly Dictionary<string, string> _defaultHeaders;
        private readonly SemaphoreSlim _semaphore;

        // Configuration properties
        public string BaseUrl { get; set; } = string.Empty;
        public int TimeoutSeconds { get; set; } = 30;
        public int MaxRetries { get; set; } = 3;
        public TimeSpan InitialRetryDelay { get; set; } = TimeSpan.FromSeconds(1);

        // Events
        public event EventHandler<TokenRefreshEventArgs>? TokenRefreshRequired;
        public event EventHandler<NetworkStatusEventArgs>? NetworkStatusChanged;

        // Retry policy using Polly
        private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;

        public HttpClientService(HttpClient httpClient, ILogger<HttpClientService> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _statistics = new HttpClientStatistics();
            _defaultHeaders = new Dictionary<string, string>();
            _semaphore = new SemaphoreSlim(10, 10); // Limit concurrent requests

            // Use centralized JSON serialization options for consistency
            _jsonOptions = TDFShared.Helpers.JsonSerializationHelper.CompactOptions;

            // Configure retry policy with exponential backoff
            _retryPolicy = Policy
                .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode && IsRetryableStatusCode(r.StatusCode))
                .Or<HttpRequestException>()
                .Or<TaskCanceledException>()
                .WaitAndRetryAsync(
                    retryCount: MaxRetries,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) + InitialRetryDelay,
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        _logger.LogWarning("Retry attempt {RetryCount} for {Endpoint} in {Delay}ms",
                            retryCount, context.TryGetValue("endpoint", out var endpointValue) ? endpointValue.ToString() : "unknown", timespan.TotalMilliseconds);

                        lock (_statisticsLock)
                        {
                            _statistics.RetriedRequests++;
                        }
                    });

            _logger.LogInformation("HttpClientService initialized with BaseUrl: {BaseUrl}, Timeout: {Timeout}s, MaxRetries: {MaxRetries}",
                BaseUrl, TimeoutSeconds, MaxRetries);
        }

        public async Task SetAuthenticationTokenAsync(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                await ClearAuthenticationTokenAsync();
                return;
            }

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            _logger.LogDebug("Authentication token set");
        }

        public async Task ClearAuthenticationTokenAsync()
        {
            _httpClient.DefaultRequestHeaders.Authorization = null;
            _logger.LogDebug("Authentication token cleared");
            await Task.CompletedTask;
        }

        public async Task<T> GetAsync<T>(string endpoint, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                var response = await _httpClient.GetAsync(BuildUrl(endpoint), cancellationToken);
                return await ProcessResponseAsync<T>(response, endpoint);
            }, endpoint);
        }

        public async Task<string> GetRawAsync(string endpoint, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                var response = await _httpClient.GetAsync(BuildUrl(endpoint), cancellationToken);
                await EnsureSuccessStatusCodeAsync(response, endpoint);
                return await response.Content.ReadAsStringAsync();
            }, endpoint);
        }

        public async Task<TResponse> PostAsync<TRequest, TResponse>(string endpoint, TRequest data, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                var json = JsonSerializer.Serialize(data, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(BuildUrl(endpoint), content, cancellationToken);
                return await ProcessResponseAsync<TResponse>(response, endpoint);
            }, endpoint);
        }

        public async Task PostAsync<TRequest>(string endpoint, TRequest data, CancellationToken cancellationToken = default)
        {
            await ExecuteWithRetryAsync(async () =>
            {
                var json = JsonSerializer.Serialize(data, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(BuildUrl(endpoint), content, cancellationToken);
                await EnsureSuccessStatusCodeAsync(response, endpoint);
                return response;
            }, endpoint);
        }

        public async Task<TResponse> PutAsync<TRequest, TResponse>(string endpoint, TRequest data, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                var json = JsonSerializer.Serialize(data, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PutAsync(BuildUrl(endpoint), content, cancellationToken);
                return await ProcessResponseAsync<TResponse>(response, endpoint);
            }, endpoint);
        }

        public async Task PutAsync<TRequest>(string endpoint, TRequest data, CancellationToken cancellationToken = default)
        {
            await ExecuteWithRetryAsync(async () =>
            {
                var json = JsonSerializer.Serialize(data, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PutAsync(BuildUrl(endpoint), content, cancellationToken);
                await EnsureSuccessStatusCodeAsync(response, endpoint);
                return response;
            }, endpoint);
        }

        public async Task<HttpResponseMessage> DeleteAsync(string endpoint, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                var response = await _httpClient.DeleteAsync(BuildUrl(endpoint), cancellationToken);
                await EnsureSuccessStatusCodeAsync(response, endpoint);
                return response;
            }, endpoint);
        }

        public async Task<TResponse> PatchAsync<TRequest, TResponse>(string endpoint, TRequest data, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                var json = JsonSerializer.Serialize(data, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(new HttpMethod("PATCH"), BuildUrl(endpoint)) { Content = content };
                var response = await _httpClient.SendAsync(request, cancellationToken);
                return await ProcessResponseAsync<TResponse>(response, endpoint);
            }, endpoint);
        }

        public async Task<bool> TestConnectivityAsync(string healthCheckEndpoint = "health", CancellationToken cancellationToken = default)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                var response = await _httpClient.GetAsync(BuildUrl(healthCheckEndpoint), cancellationToken);
                stopwatch.Stop();

                var isReachable = response.IsSuccessStatusCode;
                _logger.LogInformation("API connectivity test: {Status} (Response time: {ResponseTime}ms)",
                    isReachable ? "Success" : "Failed", stopwatch.ElapsedMilliseconds);

                return isReachable;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "API connectivity test failed");
                return false;
            }
        }

        public async Task<NetworkStatus> GetNetworkStatusAsync()
        {
            var status = new NetworkStatus();

            try
            {
                var stopwatch = Stopwatch.StartNew();
                status.IsApiReachable = await TestConnectivityAsync();
                stopwatch.Stop();
                status.Latency = stopwatch.Elapsed;
                status.IsConnected = true; // Basic implementation - can be enhanced with platform-specific checks
                status.ConnectionType = "Unknown"; // Platform-specific implementation needed
            }
            catch
            {
                status.IsConnected = false;
                status.IsApiReachable = false;
            }

            return status;
        }

        public void AddDefaultHeader(string name, string value)
        {
            _defaultHeaders[name] = value;
            _httpClient.DefaultRequestHeaders.Remove(name);
            _httpClient.DefaultRequestHeaders.Add(name, value);
            _logger.LogDebug("Added default header: {HeaderName}", name);
        }

        public void RemoveDefaultHeader(string name)
        {
            _defaultHeaders.Remove(name);
            _httpClient.DefaultRequestHeaders.Remove(name);
            _logger.LogDebug("Removed default header: {HeaderName}", name);
        }

        public HttpClientStatistics GetStatistics()
        {
            lock (_statisticsLock)
            {
                return new HttpClientStatistics
                {
                    TotalRequests = _statistics.TotalRequests,
                    SuccessfulRequests = _statistics.SuccessfulRequests,
                    FailedRequests = _statistics.FailedRequests,
                    RetriedRequests = _statistics.RetriedRequests,
                    AverageResponseTime = _statistics.AverageResponseTime,
                    LastRequestTime = _statistics.LastRequestTime,
                    ErrorCounts = new Dictionary<string, int>(_statistics.ErrorCounts),
                    StatusCodeCounts = new Dictionary<int, int>(_statistics.StatusCodeCounts)
                };
            }
        }

        public void Dispose()
        {
            _semaphore?.Dispose();
            _httpClient?.Dispose();
            _logger.LogDebug("HttpClientService disposed");
        }

        // Private helper methods
        private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, string endpoint)
        {
            await _semaphore.WaitAsync();
            try
            {
                var stopwatch = Stopwatch.StartNew();
                var context = new Context(endpoint);
                context["endpoint"] = endpoint;

                // Create a wrapper that returns HttpResponseMessage for the retry policy
                var httpResponseOperation = async (Context ctx) =>
                {
                    try
                    {
                        var result = await operation();

                        // If T is HttpResponseMessage, return it directly
                        if (result is HttpResponseMessage httpResponse)
                        {
                            return httpResponse;
                        }

                        // For other types, create a successful response
                        var response = new HttpResponseMessage(HttpStatusCode.OK);
                        return response;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Request to {Endpoint} failed: {Message}", endpoint, ex.Message);
                        throw;
                    }
                };

                // Execute the operation with retry policy
                var httpResult = await _retryPolicy.ExecuteAsync(httpResponseOperation, context);

                // If we're expecting HttpResponseMessage, return the actual result
                if (typeof(T) == typeof(HttpResponseMessage))
                {
                    stopwatch.Stop();
                    UpdateStatistics(true, stopwatch.Elapsed, null);
                    return (T)(object)httpResult;
                }

                // For other types, execute the original operation again to get the actual result
                var actualResult = await operation();

                stopwatch.Stop();
                UpdateStatistics(true, stopwatch.Elapsed, null);
                return actualResult;
            }
            catch (Exception ex)
            {
                UpdateStatistics(false, TimeSpan.Zero, ex);
                throw;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task<T> ProcessResponseAsync<T>(HttpResponseMessage response, string endpoint)
        {
            await EnsureSuccessStatusCodeAsync(response, endpoint);

            if (typeof(T) == typeof(string))
            {
                var content = await response.Content.ReadAsStringAsync();
                return (T)(object)content;
            }

            try
            {
                var contentStream = await response.Content.ReadAsStreamAsync();
                if (contentStream.Length == 0)
                {
                    _logger.LogWarning("Empty response body for {Endpoint}", endpoint);
                    return default(T);
                }

                // Try to deserialize as ApiResponse<T> first
                contentStream.Position = 0;
                var apiResponse = await JsonSerializer.DeserializeAsync<ApiResponse<T>>(contentStream, _jsonOptions);

                if (apiResponse != null)
                {
                    if (apiResponse.Success)
                    {
                        return apiResponse.Data;
                    }
                    else
                    {
                        throw new ApiException((HttpStatusCode)apiResponse.StatusCode,
                            apiResponse.Message, apiResponse.ErrorMessage);
                    }
                }

                // Fallback to direct deserialization
                contentStream.Position = 0;
                return await JsonSerializer.DeserializeAsync<T>(contentStream, _jsonOptions);
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "JSON deserialization error for {Endpoint}", endpoint);
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogError("Raw response content: {Content}", content);
                throw new ApiException($"Failed to deserialize response from {endpoint}", jsonEx);
            }
        }

        private async Task EnsureSuccessStatusCodeAsync(HttpResponseMessage response, string endpoint)
        {
            if (response.IsSuccessStatusCode)
                return;

            var errorContent = await response.Content.ReadAsStringAsync();
            var errorMessage = GetFriendlyErrorMessage(response.StatusCode, errorContent);

            // Check for authentication errors
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                _logger.LogWarning("Authentication error for {Endpoint}", endpoint);
                TokenRefreshRequired?.Invoke(this, new TokenRefreshEventArgs
                {
                    Reason = "Authentication token expired or invalid",
                    ExpirationTime = DateTime.UtcNow
                });
            }

            lock (_statisticsLock)
            {
                var statusCode = (int)response.StatusCode;
                _statistics.StatusCodeCounts[statusCode] =
                    _statistics.StatusCodeCounts.TryGetValue(statusCode, out var count) ? count + 1 : 1;
            }

            throw new ApiException(response.StatusCode, errorMessage, errorContent);
        }

        private string BuildUrl(string endpoint)
        {
            if (string.IsNullOrEmpty(BaseUrl))
                return endpoint;

            var baseUrl = BaseUrl.TrimEnd('/');
            var cleanEndpoint = endpoint.TrimStart('/');
            return $"{baseUrl}/{cleanEndpoint}";
        }

        private static bool IsRetryableStatusCode(HttpStatusCode statusCode)
        {
            return statusCode == HttpStatusCode.RequestTimeout ||
                   statusCode == (HttpStatusCode)429 || // TooManyRequests
                   statusCode == HttpStatusCode.InternalServerError ||
                   statusCode == HttpStatusCode.BadGateway ||
                   statusCode == HttpStatusCode.ServiceUnavailable ||
                   statusCode == HttpStatusCode.GatewayTimeout;
        }

        private string GetFriendlyErrorMessage(HttpStatusCode statusCode, string errorContent)
        {
            return statusCode switch
            {
                HttpStatusCode.Unauthorized => "Authentication required. Please log in again.",
                HttpStatusCode.Forbidden => "You don't have permission to access this resource.",
                HttpStatusCode.NotFound => "The requested resource was not found.",
                HttpStatusCode.RequestTimeout => "The request timed out. Please try again.",
                (HttpStatusCode)429 => "Too many requests. Please wait before trying again.", // TooManyRequests
                HttpStatusCode.InternalServerError => "A server error occurred. Please try again later.",
                HttpStatusCode.BadGateway => "Service temporarily unavailable. Please try again later.",
                HttpStatusCode.ServiceUnavailable => "Service temporarily unavailable. Please try again later.",
                HttpStatusCode.GatewayTimeout => "The request timed out. Please try again.",
                _ => $"Request failed with status {(int)statusCode}: {statusCode}"
            };
        }

        private void UpdateStatistics(bool success, TimeSpan responseTime, Exception exception)
        {
            lock (_statisticsLock)
            {
                _statistics.TotalRequests++;
                _statistics.LastRequestTime = DateTime.UtcNow;

                if (success)
                {
                    _statistics.SuccessfulRequests++;
                }
                else
                {
                    _statistics.FailedRequests++;

                    if (exception != null)
                    {
                        var errorType = exception.GetType().Name;
                        _statistics.ErrorCounts[errorType] =
                            _statistics.ErrorCounts.TryGetValue(errorType, out var count) ? count + 1 : 1;
                    }
                }

                // Update average response time
                if (responseTime > TimeSpan.Zero)
                {
                    var totalTime = _statistics.AverageResponseTime.TotalMilliseconds * (_statistics.TotalRequests - 1) + responseTime.TotalMilliseconds;
                    _statistics.AverageResponseTime = TimeSpan.FromMilliseconds(totalTime / _statistics.TotalRequests);
                }
            }
        }
    }
}
