using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TDFMAUI.Config;
using TDFShared.DTOs.Common; // For ApiResponse
using TDFShared.Exceptions;
using Microsoft.Extensions.Options;

namespace TDFMAUI.Services
{
    public class HttpClientService : IHttpClientService
    {
        private readonly HttpClient _httpClient;
        private readonly SecureStorageService _secureStorageService;
        private readonly ILogger<HttpClientService> _logger;
        private readonly ApiSettings _apiSettings;
        private readonly JsonSerializerOptions _jsonOptions;

        // Constants for retry logic (example)
        private const int MaxRetries = 3;
        private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(1);

        public HttpClientService(HttpClient httpClient, 
                                 SecureStorageService secureStorageService, 
                                 IOptions<ApiSettings> apiSettings, // Inject IOptions<ApiSettings>
                                 ILogger<HttpClientService> logger)
        {
             // Log constructor entry
            logger?.LogInformation("HttpClientService constructor started.");

            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _secureStorageService = secureStorageService ?? throw new ArgumentNullException(nameof(secureStorageService));
            _apiSettings = apiSettings?.Value ?? throw new ArgumentNullException(nameof(apiSettings)); // Get settings from IOptions
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Configure HttpClient
            _httpClient.BaseAddress = new Uri(_apiSettings.BaseUrl);
            _httpClient.Timeout = TimeSpan.FromSeconds(_apiSettings.Timeout);
            
            _logger.LogInformation($"HttpClientService Initialized. BaseUrl: {_apiSettings.BaseUrl}, Timeout: {_apiSettings.Timeout}s");

            // Log constructor exit
            logger?.LogInformation("HttpClientService constructor finished.");

            // Configure Headers and Json Options directly on the injected client
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        public void SetAuthorizationHeader(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = null;
                _logger.LogInformation("HttpClientService: SetAuthorizationHeader - Token was null/empty, header CLEARED.");
            }
            else
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                _logger.LogInformation("HttpClientService: SetAuthorizationHeader - Bearer token SET. Token starts with: {TokenStart}", token.Length > 10 ? token.Substring(0, 10) + "..." : token);
            }
        }

        public void ClearAuthorizationHeader()
        {
            _httpClient.DefaultRequestHeaders.Authorization = null;
            _logger.LogInformation("HttpClientService: ClearAuthorizationHeader - Authorization header CLEARED.");
        }

        public async Task<T> GetAsync<T>(string endpoint)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                _logger.LogDebug("Executing GET request to {Endpoint}", endpoint);
                var response = await _httpClient.GetAsync(endpoint);
                await EnsureSuccessStatusCodeAsync(response);
                try
                {
                    // Handle potential empty response for non-DTO types
                    var contentStream = await response.Content.ReadAsStreamAsync();
                    if (contentStream.Length == 0)
                    {
                        _logger.LogWarning("Empty response body for GET {Endpoint}", endpoint);
                        return default(T);
                    }
                    contentStream.Position = 0; // Reset stream position
                    return await JsonSerializer.DeserializeAsync<T>(contentStream, _jsonOptions);
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogError(jsonEx, "JSON Deserialization Error on GET {Endpoint}", endpoint);
                    var content = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Raw Response Content: {Content}", content);
                    throw new ApiException($"Failed to deserialize response from {endpoint}", jsonEx);
                }
            });
        }

        public async Task<TResponse> PostAsync<TRequest, TResponse>(string endpoint, TRequest data)
        {
             return await ExecuteWithRetryAsync(async () =>
            {
                _logger.LogDebug("Executing POST request to {Endpoint}", endpoint);
                var response = await _httpClient.PostAsJsonAsync(endpoint, data, _jsonOptions);
                await EnsureSuccessStatusCodeAsync(response);
                try
                {
                    var contentStream = await response.Content.ReadAsStreamAsync();
                    if (contentStream.Length == 0) return default(TResponse);
                    contentStream.Position = 0;
                    return await JsonSerializer.DeserializeAsync<TResponse>(contentStream, _jsonOptions);
                }
                catch (JsonException jsonEx)
                {
                     _logger.LogError(jsonEx, "JSON Deserialization Error on POST {Endpoint}", endpoint);
                    var content = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Raw Response Content: {Content}", content);
                    throw new ApiException($"Failed to deserialize response from {endpoint}", jsonEx);
                }
            });
        }
        
        public async Task PostAsync<TRequest>(string endpoint, TRequest data)
        {
            await ExecuteWithRetryAsync(async () =>
            {
                _logger.LogDebug("Executing fire-and-forget POST request to {Endpoint}", endpoint);
                var response = await _httpClient.PostAsJsonAsync(endpoint, data, _jsonOptions);
                await EnsureSuccessStatusCodeAsync(response);
            });
        }

        public async Task<TResponse> PutAsync<TRequest, TResponse>(string endpoint, TRequest data)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                 _logger.LogDebug("Executing PUT request to {Endpoint}", endpoint);
                var response = await _httpClient.PutAsJsonAsync(endpoint, data, _jsonOptions);
                await EnsureSuccessStatusCodeAsync(response);
                try
                {
                     var contentStream = await response.Content.ReadAsStreamAsync();
                    if (contentStream.Length == 0) return default(TResponse);
                    contentStream.Position = 0;
                    return await JsonSerializer.DeserializeAsync<TResponse>(contentStream, _jsonOptions);
                }
                 catch (JsonException jsonEx)
                {
                     _logger.LogError(jsonEx, "JSON Deserialization Error on PUT {Endpoint}", endpoint);
                     var content = await response.Content.ReadAsStringAsync();
                     _logger.LogError("Raw Response Content: {Content}", content);
                     throw new ApiException($"Failed to deserialize response from {endpoint}", jsonEx);
                }
            });
        }
        
        public async Task PutAsync<TRequest>(string endpoint, TRequest data)
        {
             await ExecuteWithRetryAsync(async () =>
            {
                 _logger.LogDebug("Executing fire-and-forget PUT request to {Endpoint}", endpoint);
                var response = await _httpClient.PutAsJsonAsync(endpoint, data, _jsonOptions);
                await EnsureSuccessStatusCodeAsync(response);
            });
        }

        public async Task<HttpResponseMessage> DeleteAsync(string endpoint)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                _logger.LogDebug("Executing DELETE request to {Endpoint}", endpoint);
                var response = await _httpClient.DeleteAsync(endpoint);
                await EnsureSuccessStatusCodeAsync(response);
                return response; // Return the response
            });
        }

        public async Task<string> GetRawAsync(string endpoint)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                _logger.LogDebug("Executing Raw GET request to {Endpoint}", endpoint);
                var response = await _httpClient.GetAsync(endpoint);
                await EnsureSuccessStatusCodeAsync(response);
                // Read content directly as string
                return await response.Content.ReadAsStringAsync();
            });
        }

        public async Task EnsureSuccessStatusCodeAsync(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            string errorContent = "No error content available.";
            try {
                 errorContent = await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex) {
                 _logger.LogError(ex, "Failed to read error response content.");
            }

            _logger.LogWarning("API request failed with status {StatusCode}. Endpoint: {RequestUri}. Content: {ErrorContent}", 
                response.StatusCode, response.RequestMessage?.RequestUri, errorContent);
                
            // Attempt to deserialize standard error response
            ApiResponse<object> apiError = null;
            if (response.Content.Headers.ContentType?.MediaType == "application/json")
            {
                try
                {
                     apiError = await response.Content.ReadFromJsonAsync<ApiResponse<object>>(_jsonOptions);
                     _logger.LogDebug("Deserialized API error response: {@ApiError}", apiError);
                }
                catch (Exception deserEx)
                {
                    _logger.LogWarning(deserEx, "Could not deserialize standard API error response.");
                }
            }

            string errorMessage = apiError?.ErrorMessage ?? apiError?.Message ?? response.ReasonPhrase ?? "API request failed.";
            
            // Include validation errors if present
            if (apiError?.Errors != null && apiError.Errors.Any())
            {
                errorMessage += " " + string.Join(" ", apiError.Errors.SelectMany(kv => kv.Value));
            }
            
            // Check if this is related to the missing RevokedTokens table
            if (errorContent.Contains("Invalid object name 'RevokedTokens'"))
            {
                _logger.LogWarning("Detected RevokedTokens database issue. This requires server-side fixes.");
                errorMessage = "Authentication system requires database updates. Please contact administrators.";
            }
            
            throw new ApiException(response.StatusCode, errorMessage, errorContent);
        }

        private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> action)
        {
            int retries = 0;
            while (true)
            {
                try
                {
                    return await action();
                }
                catch (Exception ex) when (IsRetryableException(ex) && retries < MaxRetries)
                {
                    retries++;
                    TimeSpan delay = TimeSpan.FromSeconds(Math.Pow(2, retries - 1)) + InitialDelay;
                    _logger.LogWarning(ex, "Retryable exception caught. Retrying in {DelaySeconds}s... (Attempt {RetryCount}/{MaxRetries})", delay.TotalSeconds, retries, MaxRetries);
                    await Task.Delay(delay);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Non-retryable exception or max retries reached.");
                    throw; // Rethrow original exception if not retryable or retries exhausted
                }
            }
        }
        
         private async Task ExecuteWithRetryAsync(Func<Task> action)
        {
            int retries = 0;
            while (true)
            {
                try
                {
                    await action();
                    return; // Success
                }
                catch (Exception ex) when (IsRetryableException(ex) && retries < MaxRetries)
                {
                    retries++;
                    TimeSpan delay = TimeSpan.FromSeconds(Math.Pow(2, retries - 1)) + InitialDelay;
                    _logger.LogWarning(ex, "Retryable exception caught. Retrying in {DelaySeconds}s... (Attempt {RetryCount}/{MaxRetries})", delay.TotalSeconds, retries, MaxRetries);
                    await Task.Delay(delay);
                }
                 catch (Exception ex)
                {
                     _logger.LogError(ex, "Non-retryable exception or max retries reached.");
                     throw;
                }
            }
        }

        private bool IsRetryableException(Exception ex)
        {
            // Handle network-related exceptions (should be retried)
            if (ex is HttpRequestException || 
                ex is TaskCanceledException || 
                ex is TimeoutException)
            {
                return true;
            }

            // Check if it's an API exception with a retryable status code
            if (ex is ApiException apiEx)
            {
                _logger.LogDebug("Checking if API exception with status {StatusCode} is retryable", apiEx.StatusCode);
                
                // Check for specific database errors that need server-side fixes
                if (apiEx.ResponseContent?.Contains("Invalid object name 'RevokedTokens'") == true)
                {
                    _logger.LogWarning("RevokedTokens database issue detected - no retry");
                    return false; // Don't retry database structure issues
                }
                
                // Retry on server errors (5xx) and some specific 4xx errors
                return IsRetryableStatusCode(apiEx.StatusCode);
            }

            _logger.LogDebug("Non-retryable exception encountered: {ExceptionType}", ex.GetType().Name);
            return false;
        }

        private bool IsRetryableStatusCode(HttpStatusCode statusCode)
        {
            // Retry server errors (5xx) and some client errors like request timeout, service unavailable
            return statusCode == HttpStatusCode.RequestTimeout ||
                   statusCode == HttpStatusCode.TooManyRequests ||
                   statusCode == HttpStatusCode.GatewayTimeout ||
                   statusCode == HttpStatusCode.ServiceUnavailable ||
                   (int)statusCode >= 500;
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
} 