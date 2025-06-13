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
            _logger?.LogInformation("HttpClientService: Setting Authorization header with token of length {Length}", token?.Length ?? 0);
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            await Task.CompletedTask;
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

                T? result = default; // Variable to store the actual result of the operation

                // Create a wrapper that executes the operation and returns an HttpResponseMessage for the retry policy
                var httpResponseOperation = async (Context ctx) =>
                {
                    try
                    {
                        result = await operation(); // Execute the actual operation and store its result

                        // If T is HttpResponseMessage, return it directly for policy evaluation
                        if (result is HttpResponseMessage httpResponse)
                        {
                            // If the response is successful or it's a client error (400-level), don't retry
                            if (httpResponse.IsSuccessStatusCode || (int)httpResponse.StatusCode < 500)
                            {
                                return httpResponse;
                            }
                            // Only retry for server errors (500-level)
                            if (!IsRetryableStatusCode(httpResponse.StatusCode))
                            {
                                return httpResponse;
                            }
                        }

                        // For other types, create a successful HttpResponseMessage for the policy to consider it successful
                        // The actual 'result' (of type T) is already stored.
                        return new HttpResponseMessage(HttpStatusCode.OK);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Request to {Endpoint} failed: {Message}", endpoint, ex.Message);
                        throw; // Re-throw to allow Polly to handle retries based on the exception
                    }
                };

                // Execute the operation with retry policy
                // The policy will execute httpResponseOperation, which in turn executes the original 'operation'
                var httpResult = await _retryPolicy.ExecuteAsync(httpResponseOperation, context);

                // After the policy has completed (either successfully or after retries),
                // return the 'result' that was captured during the *last successful* execution of 'operation'.
                // If the policy failed and re-threw an exception, this part won't be reached.
                stopwatch.Stop();
                UpdateStatistics(true, stopwatch.Elapsed, null);
                return result!; // Use null-forgiving operator as 'result' will be set if no exception was thrown
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

        /// <summary>
        /// Process the HTTP response and deserialize the content to the requested type.
        /// </summary>
        /// <typeparam name="T">The type to deserialize to</typeparam>
        /// <param name="response">The HTTP response message</param>
        /// <param name="endpoint">The API endpoint that was called</param>
        /// <returns>The deserialized response object</returns>
        /// <exception cref="ApiException">Thrown when deserialization fails</exception>
        private async Task<T> ProcessResponseAsync<T>(HttpResponseMessage response, string endpoint)
        {
            await EnsureSuccessStatusCodeAsync(response, endpoint);

            if (typeof(T) == typeof(string))
            {
                var content = await response.Content.ReadAsStringAsync();
                return (T)(object)content;
            }

            string rawContent = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrEmpty(rawContent))
            {
                _logger.LogWarning("Empty response body for {Endpoint}", endpoint);
                if (typeof(T).IsValueType)
                {
                    return default!;
                }
                throw new ApiException($"Empty response from {endpoint}");
            }

            try
            {
                // Special handling for ApiResponse<List<LookupItem>>
                if (typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(ApiResponse<>) &&
                    typeof(T).GetGenericArguments()[0] == typeof(List<LookupItem>))
                {
                    _logger.LogDebug("Processing ApiResponse<List<LookupItem>> response from {Endpoint}", endpoint);
                    try
                    {
                        using var doc = JsonDocument.Parse(rawContent);
                        var lookupItems = new List<LookupItem>();

                        // Parse the data array if it exists
                        if (doc.RootElement.TryGetProperty("data", out JsonElement dataElement) &&
                            dataElement.ValueKind == JsonValueKind.Array)
                        {
                            foreach (JsonElement element in dataElement.EnumerateArray())
                            {
                                var lookupItem = new LookupItem();

                                // Name is required, use empty string if not found
                                lookupItem.Name = element.TryGetProperty("name", out JsonElement nameElement)
                                    ? nameElement.GetString() ?? string.Empty
                                    : string.Empty;

                                // ID defaults to name if not present
                                lookupItem.Id = element.TryGetProperty("id", out JsonElement idElement)
                                    ? idElement.GetString() ?? lookupItem.Name
                                    : lookupItem.Name;

                                // Description is optional
                                if (element.TryGetProperty("description", out JsonElement descElement))
                                {
                                    lookupItem.Description = descElement.GetString();
                                }

                                // Value is optional, defaults to Name
                                lookupItem.Value = element.TryGetProperty("value", out JsonElement valueElement)
                                    ? valueElement.GetString() ?? lookupItem.Name
                                    : lookupItem.Name;

                                // SortOrder is optional
                                if (element.TryGetProperty("sortOrder", out JsonElement sortOrderElement))
                                {
                                    if (sortOrderElement.ValueKind == JsonValueKind.Number)
                                    {
                                        lookupItem.SortOrder = sortOrderElement.GetInt32();
                                    }
                                    else if (sortOrderElement.ValueKind == JsonValueKind.String &&
                                            int.TryParse(sortOrderElement.GetString(), out var parsedOrder))
                                    {
                                        lookupItem.SortOrder = parsedOrder;
                                    }
                                }

                                lookupItems.Add(lookupItem);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Response from {Endpoint} has no data array or data is not an array", endpoint);
                        }

                        // Get success status (default false)
                        var success = doc.RootElement.TryGetProperty("success", out JsonElement successElement) &&
                                    successElement.ValueKind == JsonValueKind.True;

                        // Get message (default to empty)
                        var message = doc.RootElement.TryGetProperty("message", out JsonElement messageElement)
                            ? messageElement.GetString() ?? string.Empty
                            : string.Empty;

                        // Get status code (default to 200 OK)
                        var statusCode = doc.RootElement.TryGetProperty("statusCode", out JsonElement statusElement) &&
                                       statusElement.ValueKind == JsonValueKind.Number
                            ? statusElement.GetInt32()
                            : (int)HttpStatusCode.OK;

                        // Get error message if present
                        var errorMessage = doc.RootElement.TryGetProperty("errorMessage", out JsonElement errorElement)
                            ? errorElement.GetString()
                            : null;

                        var apiResponse = new ApiResponse<List<LookupItem>>
                        {
                            Success = success,
                            Message = message,
                            StatusCode = statusCode,
                            ErrorMessage = errorMessage,
                            Data = lookupItems
                        };

                        if (!success)
                        {
                            _logger.LogWarning("Unsuccessful response from {Endpoint}. Message: {Message}, Error: {Error}", 
                                endpoint, message, errorMessage ?? "none");
                        }
                        else
                        {
                            _logger.LogDebug("Successfully processed {Count} lookup items from {Endpoint}", 
                                lookupItems.Count, endpoint);
                        }

                        return (T)(object)apiResponse;
                    }
                    catch (JsonException jsonEx)
                    {
                        _logger.LogError(jsonEx, "JSON parsing error processing response from {Endpoint}. Content: {Content}", 
                            endpoint, rawContent);
                        throw new ApiException($"Failed to parse JSON response from {endpoint}: {jsonEx.Message}", jsonEx);
                    }
                }

                // Standard deserialization for other types
                try
                {
                    // Added paginated result handling
                    if (typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    {
                        var paginatedResultType = typeof(PaginatedResult<>).MakeGenericType(typeof(T).GetGenericArguments()[0]);
                        dynamic paginatedResult = JsonSerializer.Deserialize(rawContent, paginatedResultType, _jsonOptions)!;
                        return (T)paginatedResult.Items;
                    }
                    var result = JsonSerializer.Deserialize<T>(rawContent, _jsonOptions);
                    if (result == null && !typeof(T).IsValueType)
                    {
                        throw new ApiException($"Deserialization returned null for non-nullable type {typeof(T).Name}");
                    }
                    return result!;
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogError(jsonEx, "JSON deserialization error for type {Type} from {Endpoint}. Content: {Content}", 
                        typeof(T).Name, endpoint, rawContent);
                    throw new ApiException($"Failed to deserialize {typeof(T).Name} from {endpoint}: {jsonEx.Message}", jsonEx);
                }
            }
            catch (Exception ex) when (ex is not ApiException)
            {
                _logger.LogError(ex, "Error processing response from {Endpoint}. Content: {Content}", endpoint, rawContent);
                throw new ApiException($"Failed to process response from {endpoint}: {ex.Message}", ex);
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
            // Only retry for server errors (500-level) and specific network-related status codes
            return statusCode == HttpStatusCode.RequestTimeout ||
                   statusCode == (HttpStatusCode)429 || // TooManyRequests
                   statusCode == HttpStatusCode.InternalServerError ||
                   statusCode == HttpStatusCode.BadGateway ||
                   statusCode == HttpStatusCode.ServiceUnavailable ||
                   statusCode == HttpStatusCode.GatewayTimeout;
        }

        private string GetFriendlyErrorMessage(HttpStatusCode statusCode, string errorContent)
        {
            // Try to extract error message from API response content
            if (!string.IsNullOrEmpty(errorContent))
            {
                try
                {
                    // Try to deserialize as ApiResponse
                    var apiResponse = JsonSerializer.Deserialize<ApiResponseBase>(
                        errorContent, 
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );
                    
                    if (apiResponse != null && !string.IsNullOrEmpty(apiResponse.Message))
                    {
                        return apiResponse.Message;
                    }
                }
                catch (JsonException)
                {
                    // If we can't deserialize as ApiResponse, just continue to default messages
                    _logger.LogDebug("Could not deserialize error content as ApiResponse: {Content}", errorContent);
                }
            }

            // Default friendly messages based on status code
            return statusCode switch
            {
                HttpStatusCode.Unauthorized => "Authentication required. Please log in again.",
                HttpStatusCode.Forbidden => "You don't have permission to access this resource.",
                HttpStatusCode.NotFound => "The requested resource was not found.",
                HttpStatusCode.RequestTimeout => "The request timed out. Please try again.",
                HttpStatusCode.BadRequest => "The request was invalid. Please check your input and try again.",
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