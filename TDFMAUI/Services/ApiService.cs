using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Networking;
using Microsoft.Maui.Storage;
// TDFShared references
using TDFShared.Constants;
using TDFShared.DTOs.Auth;
using TDFShared.DTOs.Messages;
using TDFShared.DTOs.Requests;
using TDFShared.DTOs.Users;
using TDFShared.DTOs.Common;
using TDFShared.Enums;
using TDFShared.Exceptions; // Updated
using TDFShared.Models.User;
using TDFShared.Models.Message;
using System.Net.NetworkInformation;
using TDFMAUI.Config; // Added
using TDFMAUI.Helpers; // Added for DeviceHelper

namespace TDFMAUI.Services
{
    /// <summary>
    /// Service for communicating with the API.
    ///
    /// Features:
    /// - Automatic token management for authentication
    /// - Retry logic with exponential backoff for transient errors
    /// - Integrated network status monitoring
    /// - Request queueing when network is unavailable
    /// - Performance tracking with DebugService
    /// - Comprehensive error handling with user-friendly messages
    /// </summary>
    public class ApiService : IApiService, IDisposable
    {
        private readonly TDFShared.Services.IHttpClientService _httpClientService;
        private readonly SecureStorageService _secureStorage;
        private readonly JsonSerializerOptions _jsonOptions;
        private NetworkMonitorService? _networkMonitor;
        private bool _isNetworkAvailable = true;
        private readonly Dictionary<string, Queue<PendingRequest>> _pendingRequests = new Dictionary<string, Queue<PendingRequest>>();
        private readonly ILogger<ApiService>? _logger;
        private bool _initialized;

        // Class to represent a pending API request
        private class PendingRequest
        {
            public string Endpoint { get; set; }
            public object? Data { get; set; }
            public string RequestType { get; set; } // GET, POST, PUT, DELETE
            public DateTime QueuedTime { get; set; }
            public TaskCompletionSource<object> CompletionSource { get; set; }

            public PendingRequest(string endpoint, object? data, string requestType)
            {
                Endpoint = endpoint;
                Data = data;
                RequestType = requestType;
                QueuedTime = DateTime.Now;
                CompletionSource = new TaskCompletionSource<object>();
            }
        }

        public ApiService(
            TDFShared.Services.IHttpClientService httpClientService,
            SecureStorageService secureStorage,
            ILogger<ApiService>? logger,
            NetworkMonitorService? networkMonitor)
        {
            _httpClientService = httpClientService;
            _secureStorage = secureStorage;
            _logger = logger;
            _networkMonitor = networkMonitor;

            _jsonOptions = TDFShared.Helpers.JsonSerializationHelper.DefaultOptions;

            _logger?.LogInformation("ApiService: Initialized");
            if (_networkMonitor != null)
            {
                _isNetworkAvailable = _networkMonitor.IsConnected;
                _networkMonitor.NetworkStatusChanged += OnNetworkStatusChanged;
                _logger?.LogInformation("ApiService: Network monitor registered. Initial status: {Status}", (_isNetworkAvailable ? "Connected" : "Disconnected"));
            }
            _initialized = false;
        }

        private void OnNetworkStatusChanged(object? sender, NetworkStatusChangedEventArgs e)
        {
            _isNetworkAvailable = e.IsConnected;
            _logger?.LogInformation("ApiService: Network status changed: {Status}", (_isNetworkAvailable ? "Connected" : "Disconnected"));

            // If network was restored, we could trigger deferred requests here
            if (_isNetworkAvailable && _pendingRequests.Count > 0)
            {
                // Process pending requests
                Task.Run(async () => await ProcessPendingRequestsAsync());
            }
        }

        // Method to process pending requests when network is restored
        private async Task ProcessPendingRequestsAsync()
        {
            if (!_isNetworkAvailable || _pendingRequests.Count == 0)
                return;

            _logger?.LogInformation("ApiService: Processing {PendingRequestCount} pending requests", _pendingRequests.Sum(p => p.Value.Count));

            foreach (var endpointQueue in _pendingRequests.ToList())
            {
                string endpoint = endpointQueue.Key;
                var requests = endpointQueue.Value;

                while (requests.Count > 0 && _isNetworkAvailable)
                {
                    var request = requests.Peek();

                    try
                    {
                        // Skip requests older than 1 hour
                        if (DateTime.Now - request.QueuedTime > TimeSpan.FromHours(1))
                        {
                            _logger?.LogWarning("ApiService: Skipping expired request to {Endpoint}", request.Endpoint);
                            requests.Dequeue();
                            request.CompletionSource.SetException(new TimeoutException("Request expired"));
                            continue;
                        }

                        _logger?.LogInformation("ApiService: Executing queued {RequestType} request to {Endpoint}", request.RequestType, request.Endpoint);

                        object? result = null;

                        switch (request.RequestType)
                        {
                            case "GET":
                                result = await GetAsync<object>(request.Endpoint);
                                break;
                            case "POST":
                                result = await PostAsync<object, object>(request.Endpoint, request.Data ?? new object());
                                break;
                            case "PUT":
                                await PutAsync<object, object>(request.Endpoint, request.Data ?? new object());
                                break;
                            case "DELETE":
                                var response = await DeleteAsync(request.Endpoint);
                                // For DELETE requests, we can set a bool result indicating success
                                result = response.IsSuccessStatusCode;
                                break;
                        }

                        // Complete the task
                        requests.Dequeue();
                        request.CompletionSource.SetResult(result!);
                    }
                    catch (Exception ex)
                    {
                        // If this is a network error, stop processing
                        if (!_isNetworkAvailable || ex is HttpRequestException)
                        {
                            _logger?.LogWarning("ApiService: Network error while processing pending requests, will retry later");
                            break;
                        }

                        // For other errors, complete the task with an exception and move on
                        requests.Dequeue();
                        request.CompletionSource.SetException(ex);
                        _logger?.LogError(ex, "ApiService: Error processing queued request");
                    }
                }

                // If queue is empty, remove it
                if (requests.Count == 0)
                {
                    _pendingRequests.Remove(endpoint);
                }
            }

            _logger?.LogInformation("ApiService: Finished processing pending requests. {PendingRequestCount} remaining", _pendingRequests.Sum(p => p.Value.Count));
        }

        // Method to queue a request for later execution
        private Task<T> QueueRequestAsync<T>(string endpoint, object? data, string requestType)
        {
            _logger?.LogInformation("ApiService: Queuing {RequestType} request to {Endpoint} for later execution", requestType, endpoint);

            var request = new PendingRequest(endpoint, data, requestType);

            if (!_pendingRequests.ContainsKey(endpoint))
            {
                _pendingRequests[endpoint] = new Queue<PendingRequest>();
            }

            _pendingRequests[endpoint].Enqueue(request);

            // Return a task that will complete when the request is processed
            return request.CompletionSource.Task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    throw t.Exception.GetBaseException();
                }

                try
                {
                return (T)t.Result;
                }
                catch (InvalidCastException castEx)
                {
                    _logger?.LogError(castEx, "ApiService: Type mismatch processing queued request result for {Endpoint}: {ErrorMessage}", endpoint, castEx.Message);
                    throw new InvalidOperationException($"Result type mismatch for queued request {endpoint}", castEx);
                }
            });
        }

        // Method to check network before making requests
        private bool CheckNetworkBeforeRequest(string endpoint, bool queueIfUnavailable = false, object? data = null, string? requestType = null)
        {
            if (!_isNetworkAvailable)
            {
                _logger?.LogWarning("ApiService: Network unavailable, cannot make request to {Endpoint}", endpoint);

                if (queueIfUnavailable && requestType != null)
                {
                    // Queue the request for later
                    QueueRequestAsync<object>(endpoint, data, requestType);
                    _logger?.LogInformation("ApiService: Request to {Endpoint} queued for later execution", endpoint);
                }

                return false;
            }
            return true;
        }

        // Method to handle cleanup when ApiService is disposed
        public void Dispose()
        {
            if (_networkMonitor != null)
                _networkMonitor.NetworkStatusChanged -= OnNetworkStatusChanged;
        }

        private async Task InitializeAuthenticationAsync()
        {
            var tokenInfo = await _secureStorage.GetTokenAsync();
            if (!string.IsNullOrEmpty(tokenInfo.Token) && tokenInfo.Expiration > DateTime.UtcNow)
            {
                _logger?.LogInformation("ApiService: InitializeAuthenticationAsync - Found valid token in secure storage. Token: {Token}, Expires: {Expiration}", tokenInfo.Token.Length > 10 ? tokenInfo.Token.Substring(0, 10) + "..." : tokenInfo.Token, tokenInfo.Expiration);
                await _httpClientService.SetAuthenticationTokenAsync(tokenInfo.Token);
                _logger?.LogInformation("ApiService: Initialization - Authorization header SET with stored token.");
            }
            else
            {
                if (string.IsNullOrEmpty(tokenInfo.Token))
                {
                    _logger?.LogWarning("ApiService: InitializeAuthenticationAsync - No token found in secure storage.");
                }
                else
                {
                    _logger?.LogWarning("ApiService: InitializeAuthenticationAsync - Token found but expired. Expires: {Expiration}, Current UTC: {UtcNow}", tokenInfo.Expiration, DateTime.UtcNow);
                }
                await _httpClientService.ClearAuthenticationTokenAsync();
                _logger?.LogInformation("ApiService: Initialization - Authorization header CLEARED.");
            }
            _initialized = true;
        }

        // Add a new method to handle async initialization
        public async Task InitializeAsync()
        {
            if (!_initialized)
            {
                await InitializeAuthenticationAsync();
                _initialized = true;
            }
        }

        #region HTTP Methods
        // Helper method to determine if an exception is retryable
        private bool IsRetryableException(Exception ex)
        {
            return ex is HttpRequestException ||
                   ex is WebException ||
                   ex is TimeoutException ||
                   (ex is ApiException apiEx && IsRetryableStatusCode(apiEx.StatusCode));
        }

        // Helper method to determine if a response has a retryable status code
        private bool IsRetryableStatusCode(HttpStatusCode statusCode)
        {
            return statusCode == HttpStatusCode.RequestTimeout ||
                   statusCode == HttpStatusCode.ServiceUnavailable ||
                   statusCode == HttpStatusCode.GatewayTimeout ||
                   (int)statusCode >= 500; // General server errors
        }

        public async Task<string> GetRawResponseAsync(string endpoint)
        {
            _logger?.LogDebug("Executing Raw GET request to {Endpoint}", endpoint);
            try
            {
                // Pass endpoint as-is
                var response = await _httpClientService.GetRawAsync(endpoint);
                return response;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "ApiService: Error in GetRawResponseAsync for {Endpoint}: {Message}", endpoint, ex.Message);
                throw new ApiException(ex.Message, ex);
            }
        }

        public async Task<T> GetAsync<T>(string endpoint, bool queueIfUnavailable = false)
        {
            if (!CheckNetworkBeforeRequest(endpoint, queueIfUnavailable, null, "GET"))
            {
                return queueIfUnavailable
                    ? await QueueRequestAsync<T>(endpoint, null, "GET")
                    : default!;
            }

            try
            {
                // Pass endpoint as-is
                var response = await _httpClientService.GetAsync<T>(endpoint);
                return response;
            }
            catch (HttpRequestException httpEx)
            {
                _logger?.LogError(httpEx, "ApiService: HTTP error in GetAsync for {Endpoint}", endpoint);
                _isNetworkAvailable = false;
                if (queueIfUnavailable) return await QueueRequestAsync<T>(endpoint, null, "GET");
                throw new ApiException($"Connection error: {httpEx.Message}", httpEx);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "ApiService: Error in GetAsync for {Endpoint}: {Message}", endpoint, ex.Message);
                throw new ApiException(ex.Message, ex);
            }
        }

        public async Task<TResponse> PostAsync<TRequest, TResponse>(string endpoint, TRequest data, bool queueIfUnavailable = true)
        {
            if (!CheckNetworkBeforeRequest(endpoint, queueIfUnavailable, data, "POST"))
            {
                return await QueueRequestAsync<TResponse>(endpoint, data, "POST");
            }

            try
            {
                _logger?.LogDebug("PostAsync: Sending POST request to {Endpoint}", endpoint);
                var response = await _httpClientService.PostAsync<TRequest, TResponse>(endpoint, data);
                _logger?.LogDebug("PostAsync: Successfully received response from {Endpoint}", endpoint);
                return response;
            }
            catch (HttpRequestException ex)
            {
                _logger?.LogError(ex, "PostAsync: HTTP request error for {Endpoint}: {Message}", endpoint, ex.Message);
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "PostAsync: Unexpected error for {Endpoint}: {Message}", endpoint, ex.Message);
                throw;
            }
        }

        public async Task<TResponse> PutAsync<TRequest, TResponse>(string endpoint, TRequest data, bool queueIfUnavailable = true)
        {
            if (!CheckNetworkBeforeRequest(endpoint, queueIfUnavailable, data, "PUT"))
            {
                 if (queueIfUnavailable)
                    return await QueueRequestAsync<TResponse>(endpoint, data, "PUT");
                else
                    throw new InvalidOperationException("Network unavailable");
            }

            try
            {
                // Pass endpoint as-is
                var response = await _httpClientService.PutAsync<TRequest, TResponse>(endpoint, data);
                return response;
            }
            catch (HttpRequestException httpEx)
            {
                _logger?.LogError(httpEx, "ApiService: HTTP error in PutAsync for {Endpoint}", endpoint);
                _isNetworkAvailable = false;
                if (queueIfUnavailable) return await QueueRequestAsync<TResponse>(endpoint, data, "PUT");
                throw new ApiException($"Connection error: {httpEx.Message}", httpEx);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "ApiService: Error in PutAsync for {Endpoint}: {Message}", endpoint, ex.Message);
                throw new ApiException(ex.Message, ex);
            }
        }

        public async Task<HttpResponseMessage> DeleteAsync(string endpoint, bool queueIfUnavailable = true)
        {
            if (!CheckNetworkBeforeRequest(endpoint, queueIfUnavailable))
            {
                if (queueIfUnavailable)
                {
                    // Queue returns Task<object>, but Delete returns HttpResponseMessage, so we await and convert
                    var result = await QueueRequestAsync<object>(endpoint, null, "DELETE");
                    // Return a fake success response since the request is queued
                    return new HttpResponseMessage(HttpStatusCode.Accepted)
                    {
                        ReasonPhrase = "Request queued for later execution"
                    };
                }
                else
                {
                    throw new NetworkUnavailableException("Network unavailable");
                }
            }

            try
            {
                // Pass endpoint as-is
                var response = await _httpClientService.DeleteAsync(endpoint);
                return response;
            }
            catch (HttpRequestException httpEx)
            {
                _logger?.LogError(httpEx, "ApiService: HTTP error in DeleteAsync for {Endpoint}", endpoint);
                _isNetworkAvailable = false;
                if (queueIfUnavailable)
                {
                    // Queue returns Task<object>, but Delete returns HttpResponseMessage, so we await and convert
                    var result = await QueueRequestAsync<object>(endpoint, null, "DELETE");
                    // Return a fake success response since the request is queued
                    return new HttpResponseMessage(HttpStatusCode.Accepted)
                    {
                        ReasonPhrase = "Request queued for later execution"
                    };
                }
                throw new ApiException($"Connection error: {httpEx.Message}", httpEx);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "ApiService: Error in DeleteAsync for {Endpoint}: {Message}", endpoint, ex.Message);
                throw new ApiException(ex.Message, ex);
            }
        }
        #endregion

        #region Authentication
        public async Task<ApiResponse<LoginResponseDto>> LoginAsync(LoginRequestDto loginRequest)
        {
            if (!_initialized) await InitializeAuthenticationAsync();
            string endpoint = ApiRoutes.Auth.Login;
            if (!CheckNetworkBeforeRequest(endpoint, queueIfUnavailable: false))
                throw new NetworkUnavailableException();
            try
            {
                var response = await PostAsync<LoginRequestDto, ApiResponse<LoginResponseDto>>(endpoint, loginRequest);
                if (response?.Success == true && response.Data != null)
                {
                    DateTime expiration = DateTime.UtcNow.AddHours(24); // fallback
                    await _secureStorage.SaveTokenAsync(response.Data.Token, expiration);
                    await _httpClientService.SetAuthenticationTokenAsync(response.Data.Token);
                }
                return response ?? new ApiResponse<LoginResponseDto> { Success = false, Message = "Login failed" };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "ApiService: Login failed: {Message}", ex.Message);
                return new ApiResponse<LoginResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<bool>> LogoutAsync()
        {
            if (!_initialized) await InitializeAuthenticationAsync();
            try
            {
                await _httpClientService.ClearAuthenticationTokenAsync();
                await _secureStorage.ClearTokenAsync();
                return new ApiResponse<bool> { Success = true, Data = true };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "ApiService: Logout failed: {Message}", ex.Message);
                return new ApiResponse<bool> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<RegisterResponseDto>> RegisterAsync(RegisterRequestDto registerRequest)
        {
            if (!_initialized) await InitializeAuthenticationAsync();
            string endpoint = ApiRoutes.Auth.Register;
            try
            {
                _logger?.LogInformation("ApiService: Attempting registration for user {Username}", registerRequest.Username);
                var response = await PostAsync<RegisterRequestDto, ApiResponse<RegisterResponseDto>>(endpoint, registerRequest, queueIfUnavailable: false);
                return response ?? new ApiResponse<RegisterResponseDto> { Success = false, Message = "Registration failed" };
            }
            catch (ApiException ex)
            {
                _logger?.LogError(ex, "ApiService: API error during registration for {Username}: {Message}", registerRequest.Username, ex.Message);
                
                // Try to extract more specific error message from response content
                string errorMessage = ex.Message;
                if (!string.IsNullOrEmpty(ex.ResponseContent))
                {
                    errorMessage += $" Details: {ex.ResponseContent}";
                }
                
                // If we have response content, try to extract a more specific message
                if (!string.IsNullOrEmpty(ex.ResponseContent))
                {
                    try
                    {
                        var errorResponse = System.Text.Json.JsonSerializer.Deserialize<ApiResponseBase>(
                            ex.ResponseContent,
                            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                        );
                        
                        if (errorResponse != null && !string.IsNullOrEmpty(errorResponse.Message))
                        {
                            errorMessage = errorResponse.Message;
                        }
                    }
                    catch
                    {
                        // If we can't deserialize, just use the original message
                        _logger?.LogDebug("Could not deserialize error response content: {Content}", ex.ResponseContent);
                    }
                }
                
                return new ApiResponse<RegisterResponseDto> 
                { 
                    Success = false, 
                    Message = errorMessage,
                    StatusCode = (int)ex.StatusCode
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "ApiService: Error during registration for {Username}: {Message}", registerRequest.Username, ex.Message);
                return new ApiResponse<RegisterResponseDto> { Success = false, Message = GetFriendlyErrorMessage(ex) };
            }
        }

        #region User Operations (Now returns DTOs)
         public async Task<UserDto> GetUserByIdAsync(int userId)
        {
             if (!_initialized) await InitializeAuthenticationAsync();
             string endpoint = string.Format(ApiRoutes.Users.GetById, userId);
              if (!CheckNetworkBeforeRequest(endpoint, queueIfUnavailable: true))
                 return await QueueRequestAsync<UserDto>(endpoint, null, "GET");

             var response = await GetAsync<ApiResponse<UserDto>>(endpoint);
             if(response == null || !response.Success)
             {
                 HttpStatusCode statusCode = HttpStatusCode.NotFound;
                 if (response != null)
                 {
                     statusCode = (HttpStatusCode)response.StatusCode;
                 }
                 throw new ApiException(statusCode, response?.ErrorMessage ?? "User not found");
             }
             return response.Data!;
        }

        public async Task<UserDto> GetUserAsync(int userId)
        {
            // This is an alias for GetUserByIdAsync for backward compatibility
            return await GetUserByIdAsync(userId);
        }

        public async Task<ApiResponse<UserProfileDto>> GetUserProfileAsync(int userId)
        {
            if (!_initialized) await InitializeAuthenticationAsync();
            string endpoint = string.Format(ApiRoutes.Users.GetById, userId);
            try
            {
                var response = await GetAsync<ApiResponse<UserProfileDto>>(endpoint);
                return response ?? new ApiResponse<UserProfileDto> { Success = false, Message = "Failed to get user profile" };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "ApiService: Failed to get user profile: {Message}", ex.Message);
                return new ApiResponse<UserProfileDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<PaginatedResult<UserDto>> GetAllUsersAsync(int page = 1, int pageSize = 10)
        {
             if (!_initialized) await InitializeAuthenticationAsync();
             string endpoint = $"{ApiRoutes.Users.Base}?pageNumber={page}&pageSize={pageSize}";
             if (!CheckNetworkBeforeRequest(endpoint, queueIfUnavailable: true))
                 return await QueueRequestAsync<PaginatedResult<UserDto>>(endpoint, null, "GET");

             var response = await GetAsync<ApiResponse<PaginatedResult<UserDto>>>(endpoint);
              if(response == null || !response.Success)
             {
                 HttpStatusCode statusCode = HttpStatusCode.BadRequest;
                 if (response != null)
                 {
                     statusCode = (HttpStatusCode)response.StatusCode;
                 }
                 throw new ApiException(statusCode, response?.ErrorMessage ?? "Failed to get users");
             }
            return response.Data ?? new PaginatedResult<UserDto>() { Items = new List<UserDto>() };
        }

        public async Task<List<UserDto>> GetUsersByDepartmentAsync(string department)
        {
            if (!_initialized) await InitializeAuthenticationAsync();
            if (string.IsNullOrWhiteSpace(department)) return new List<UserDto>();
            string endpoint = string.Format(ApiRoutes.Users.GetByDepartment, Uri.EscapeDataString(department));
            if (!CheckNetworkBeforeRequest(endpoint, queueIfUnavailable: true))
                return await QueueRequestAsync<List<UserDto>>(endpoint, null, "GET");

            try
            {
                var response = await GetAsync<ApiResponse<IEnumerable<UserDto>>>(endpoint);
                if (response == null || !response.Success)
                {
                    HttpStatusCode statusCode = HttpStatusCode.NotFound;
                    if (response != null)
                    {
                        statusCode = (HttpStatusCode)response.StatusCode;
                    }
                    throw new ApiException(statusCode, response?.ErrorMessage ?? "Failed to get users by department");
                }
                return response.Data?.ToList() ?? new List<UserDto>();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "ApiService: Failed to get users by department '{Department}': {ErrorMessage}", department, GetFriendlyErrorMessage(ex));
                throw;
            }
        }

        public async Task<UserDto> CreateUserAsync(CreateUserRequest userRequest)
        {
            if (!_initialized) await InitializeAuthenticationAsync();
            string endpoint = ApiRoutes.Users.Base;

            if (!CheckNetworkBeforeRequest(endpoint, queueIfUnavailable: false))
                throw new NetworkUnavailableException("Create user requires network.");

            try
            {
                _logger?.LogInformation("ApiService: Creating new user");
                var response = await PostAsync<CreateUserRequest, ApiResponse<UserDto>>(endpoint, userRequest);

                if (response == null || !response.Success)
                {
                    HttpStatusCode statusCode = HttpStatusCode.BadRequest;
                    if (response != null)
                    {
                        statusCode = (HttpStatusCode)response.StatusCode;
                    }
                    throw new ApiException(statusCode, response?.ErrorMessage ?? "Failed to create user");
                }

                return response.Data!;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "ApiService: Failed to create user: {ErrorMessage}", GetFriendlyErrorMessage(ex));
                throw;
            }
        }

        public async Task<UserDto> UpdateUserAsync(int userId, UpdateUserRequest userDto)
        {
             if (!_initialized) await InitializeAuthenticationAsync();
             string endpoint = string.Format(ApiRoutes.Users.GetById, userId);
             if (!CheckNetworkBeforeRequest(endpoint, queueIfUnavailable: true))
                 throw new NetworkUnavailableException("Update user requires network.");

            try
            {
                 var response = await PutAsync<UpdateUserRequest, ApiResponse<UserDto>>(endpoint, userDto);
                  if (response == null || !response.Success)
                  {
                      HttpStatusCode statusCode = HttpStatusCode.BadRequest;
                      if (response != null)
                      {
                          statusCode = (HttpStatusCode)response.StatusCode;
                      }
                      throw new ApiException(statusCode, response?.ErrorMessage ?? "Failed to update user");
                  }
                 return response.Data!;
            }
            catch (Exception ex)
            {
                 _logger?.LogError(ex, "ApiService: Failed to update user {UserId}", userId);
                 throw new ApiException($"Update failed: {GetFriendlyErrorMessage(ex)}", ex);
            }
        }

        public async Task DeleteUserAsync(int userId)
        {
             if (!_initialized) await InitializeAuthenticationAsync();
             string endpoint = string.Format(ApiRoutes.Users.GetById, userId);
              if (!CheckNetworkBeforeRequest(endpoint, queueIfUnavailable: true))
                 throw new NetworkUnavailableException("Delete user requires network.");

            try
            {
                var response = await DeleteAsync(endpoint);
                // Ensure successful response
                if (!response.IsSuccessStatusCode)
                {
                    _logger?.LogWarning("ApiService: Delete user {UserId} returned status code {StatusCode}",
                        userId, response.StatusCode);
                    throw new ApiException(response.StatusCode, $"Failed to delete user: {response.ReasonPhrase}");
                }
            }
            catch (Exception ex)
            {
                 _logger?.LogError(ex, "ApiService: Failed to delete user {UserId}", userId);
                 throw new ApiException($"Delete failed: {GetFriendlyErrorMessage(ex)}", ex);
            }
        }

        public async Task<bool> ChangePasswordAsync(ChangePasswordDto changePasswordDto)
        {
             if (!_initialized) await InitializeAuthenticationAsync();
             string endpoint = ApiRoutes.Users.ChangePassword;
             if (!CheckNetworkBeforeRequest(endpoint, queueIfUnavailable: false))
                 throw new NetworkUnavailableException();

            try
            {
                var response = await PostAsync<ChangePasswordDto, ApiResponse<bool>>(endpoint, changePasswordDto);
                 if (response == null || !response.Success)
                 {
                     HttpStatusCode statusCode = HttpStatusCode.BadRequest;
                     if (response != null)
                     {
                         statusCode = (HttpStatusCode)response.StatusCode;
                     }
                     throw new ApiException(statusCode, response?.ErrorMessage ?? "Failed to change password.");
                 }
                return true;
            }
            catch (ApiException ex)
            {
                _logger?.LogError(ex, "API error changing password: {Message}", ex.Message);
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Unexpected error changing password.");
                throw new ApiException("An unexpected error occurred while changing the password.", ex);
            }
        }

        public async Task<bool> UploadProfilePictureAsync(Stream imageStream, string fileName, string contentType)
        {
            if (!CheckNetworkBeforeRequest("users/upload-profile-picture", queueIfUnavailable: false))
            {
                throw new NetworkUnavailableException("Network unavailable. Cannot upload profile picture.");
            }

            try
            {
                _logger?.LogInformation("ApiService: Uploading profile picture {FileName} ({ContentType})", fileName, contentType);

                using var content = new MultipartFormDataContent();
                using var streamContent = new StreamContent(imageStream);
                streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);

                // The backend expects a parameter named 'file'
                content.Add(streamContent, "file", fileName);

                var response = await PostAsync<MultipartFormDataContent, ApiResponse<bool>>("users/upload-profile-picture", content);

                if (response?.Success != true)
                {
                    _logger?.LogWarning("ApiService: Failed to upload profile picture. Message: {Message}", response?.Message);
                    throw new ApiException(response?.Message ?? "Failed to upload profile picture.");
                }

                 _logger?.LogInformation("ApiService: Profile picture uploaded successfully.");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "ApiService: Error uploading profile picture: {ErrorMessage}", GetFriendlyErrorMessage(ex));
                throw;
            }
        }

        public async Task<bool> UpdateUserProfileAsync(UpdateMyProfileRequest profileData)
        {
             if (!CheckNetworkBeforeRequest(ApiRoutes.Users.UpdateMyProfile, queueIfUnavailable: true, profileData, "PUT"))
            {
                _logger?.LogInformation("ApiService: UpdateUserProfileAsync request queued due to network unavailability.");
                return false;
            }

            try
            {
                _logger?.LogInformation("ApiService: Updating user profile.");
                var response = await PutAsync<UpdateMyProfileRequest, ApiResponse<bool>>(ApiRoutes.Users.UpdateMyProfile, profileData);

                if (response?.Success != true)
                {
                    _logger?.LogWarning("ApiService: Failed to update user profile. Message: {Message}", response?.Message);
                    throw new ApiException(response?.Message ?? "Failed to update profile.");
                }

                _logger?.LogInformation("ApiService: User profile updated successfully.");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "ApiService: Error updating user profile: {ErrorMessage}", GetFriendlyErrorMessage(ex));
                throw;
            }
        }
        #endregion

        #region Message Operations (Return DTOs)
        public async Task<PaginatedResult<ChatMessageDto>> GetUserMessagesAsync(int userId, MessagePaginationDto pagination)
        {
            if (!_initialized) await InitializeAuthenticationAsync();
            string endpoint = $"{ApiRoutes.Messages.Base}?userId={userId}&{BuildPaginationQueryString(pagination)}";
            if (!CheckNetworkBeforeRequest(endpoint, queueIfUnavailable: true))
                return await QueueRequestAsync<PaginatedResult<ChatMessageDto>>(endpoint, pagination, "GET");

            var response = await GetAsync<ApiResponse<PaginatedResult<ChatMessageDto>>>(endpoint);
            if (response == null || !response.Success)
                throw new ApiException(response != null ? (HttpStatusCode)response.StatusCode : HttpStatusCode.BadRequest, response?.ErrorMessage ?? "Failed to get user messages");
               return response.Data ?? new PaginatedResult<ChatMessageDto>() { Items = new List<ChatMessageDto>() };
        }

        public async Task<PaginatedResult<ChatMessageDto>> GetAllMessagesAsync(MessagePaginationDto pagination)
        {
            if (!_initialized) await InitializeAuthenticationAsync();
            string endpoint = $"{ApiRoutes.Messages.Base}{BuildPaginationQueryString(pagination)}";
            if (!CheckNetworkBeforeRequest(endpoint, queueIfUnavailable: true))
                 return await QueueRequestAsync<PaginatedResult<ChatMessageDto>>(endpoint, pagination, "GET");

            var response = await GetAsync<ApiResponse<PaginatedResult<ChatMessageDto>>>(endpoint);
             if(response == null || !response.Success)
             {
                 HttpStatusCode statusCode = HttpStatusCode.BadRequest;
                 if (response != null)
                 {
                     statusCode = (HttpStatusCode)response.StatusCode;
                 }
                 throw new ApiException(statusCode, response?.ErrorMessage ?? "Failed to get all messages");
             }
            return response.Data ?? new PaginatedResult<ChatMessageDto>() { Items = new List<ChatMessageDto>() };
        }

        public async Task<ChatMessageDto> CreateMessageAsync(MessageCreateDto createDto)
        {
            if (!_initialized) await InitializeAuthenticationAsync();
            string endpoint = ApiRoutes.Messages.Base;
             if (!CheckNetworkBeforeRequest(endpoint, queueIfUnavailable: true))
                 throw new NetworkUnavailableException("Create message requires network.");

             var response = await PostAsync<MessageCreateDto, ApiResponse<ChatMessageDto>>(endpoint, createDto);
             if(response == null || !response.Success)
                throw new ApiException(response != null ? (HttpStatusCode)response.StatusCode : HttpStatusCode.BadRequest, response?.ErrorMessage ?? "Failed to create message");
             return response.Data!;
        }

        public async Task<ChatMessageDto> CreatePrivateMessageAsync(MessageCreateDto createDto)
        {
            if (!_initialized) await InitializeAuthenticationAsync();
            string endpoint = ApiRoutes.Messages.Private;

            if (!CheckNetworkBeforeRequest(endpoint, queueIfUnavailable: true))
                throw new NetworkUnavailableException("Create private message requires network.");

            var response = await PostAsync<MessageCreateDto, ApiResponse<ChatMessageDto>>(endpoint, createDto);
            if (response == null || !response.Success)
                throw new ApiException(response != null ? (HttpStatusCode)response.StatusCode : HttpStatusCode.BadRequest, response?.ErrorMessage ?? "Failed to create private message");
            return response.Data!;
        }

        public async Task<bool> MarkMessageAsReadAsync(int messageId)
        {
             if (!_initialized) await InitializeAuthenticationAsync();
             string endpoint = string.Format(ApiRoutes.Messages.MarkRead, messageId);
              if (!CheckNetworkBeforeRequest(endpoint, queueIfUnavailable: true))
                 throw new NetworkUnavailableException("Mark message read requires network.");

             var response = await PostAsync<object, ApiResponse<bool>>(endpoint, new object());
            return response?.Success ?? false;
        }

        public async Task<bool> MarkMessagesAsReadAsync(List<int> messageIds)
        {
            if (!_initialized) await InitializeAuthenticationAsync();
            string endpoint = ApiRoutes.Messages.MarkBulkRead;

            if (!CheckNetworkBeforeRequest(endpoint, queueIfUnavailable: true))
                throw new NetworkUnavailableException("Mark messages as read requires network.");

            var response = await PostAsync<List<int>, ApiResponse<bool>>(endpoint, messageIds);
            return response?.Success ?? false;
        }

        public async Task<List<ChatMessageDto>> GetRecentChatMessagesAsync(int count = 50)
        {
             if (!_initialized) await InitializeAuthenticationAsync();
             string endpoint = $"{ApiRoutes.Messages.RecentChat}?count={count}";
             if (!CheckNetworkBeforeRequest(endpoint, queueIfUnavailable: true))
                 return await QueueRequestAsync<List<ChatMessageDto>>(endpoint, null, "GET");

             var response = await GetAsync<ApiResponse<List<ChatMessageDto>>>(endpoint);
              if(response == null || !response.Success)
              {
                 HttpStatusCode statusCode = HttpStatusCode.BadRequest;
                 if (response != null)
                 {
                     statusCode = (HttpStatusCode)response.StatusCode;
                 }
                 throw new ApiException(statusCode, response?.ErrorMessage ?? "Failed to get recent messages");
              }
             return response.Data ?? new List<ChatMessageDto>();
        }

        public async Task<PaginatedResult<ChatMessageDto>> GetPrivateMessagesAsync(int userId, MessagePaginationDto pagination)
        {
            if (!_initialized) await InitializeAuthenticationAsync();
            string endpoint = $"{ApiRoutes.Messages.Private}?userId={userId}&{BuildPaginationQueryString(pagination)}";

            if (!CheckNetworkBeforeRequest(endpoint, queueIfUnavailable: true))
                return await QueueRequestAsync<PaginatedResult<ChatMessageDto>>(endpoint, pagination, "GET");

            var response = await GetAsync<ApiResponse<PaginatedResult<ChatMessageDto>>>(endpoint);
            if (response == null || !response.Success)
                throw new ApiException(response != null ? (HttpStatusCode)response.StatusCode : HttpStatusCode.BadRequest, response?.ErrorMessage ?? "Failed to get private messages");

            return response.Data ?? new PaginatedResult<ChatMessageDto>() { Items = new List<ChatMessageDto>() };
        }

        public async Task<bool> MarkNotificationAsSeenAsync(int notificationId)
        {
             if (!_initialized) await InitializeAuthenticationAsync();
             string endpoint = $"notifications/{notificationId}/mark-seen";
             if (!CheckNetworkBeforeRequest(endpoint, queueIfUnavailable: true))
                 throw new NetworkUnavailableException("Mark notification seen requires network.");

            var response = await PostAsync<object, ApiResponse<bool>>(endpoint, null);
            return response?.Success ?? false;
        }

        public async Task<bool> BroadcastNotificationAsync(string message, string? department = null)
        {
             if (!_initialized) await InitializeAuthenticationAsync();
             string endpoint = "notifications/broadcast";
             if (!CheckNetworkBeforeRequest(endpoint, queueIfUnavailable: true))
                 throw new NetworkUnavailableException("Broadcast notification requires network.");

             var payload = new { Message = message, Department = department };
             var response = await PostAsync<object, ApiResponse<bool>>(endpoint, payload);
             return response?.Success ?? false;
        }

        /// <summary>
        /// Tests API connectivity and logs the result
        /// </summary>
        /// <returns>True if API is reachable, false otherwise</returns>
        public async Task<bool> ValidateApiConnectionAsync()
        {
            try
            {
                var result = await GetAsync<object>(ApiRoutes.Health.Ping);
                return result != null;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> TestConnectivityAsync()
        {
            try
            {
                await GetAsync<object>(ApiRoutes.Health.Ping);
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "API Connectivity test failed using {Endpoint}", ApiRoutes.Health.Ping);
                return false;
            }
        }

        /// <summary>
        /// Gets a user-friendly error message from an API exception
        /// </summary>
        public static string GetFriendlyErrorMessage(Exception ex)
        {
            if (ex is UnauthorizedAccessException)
                return "Authentication failed. Please log in again.";
            if (ex is HttpRequestException || ex is WebException || ex.Message.Contains("Network error"))
                return "Network error. Please check your connection and try again.";
            if (ex is TimeoutException || ex is TaskCanceledException)
                return "The request timed out. Please try again.";
            if (ex is ApiException apiEx)
                return $"API Error ({apiEx.StatusCode}): {apiEx.Message}";
            if (ex is JsonException)
                return "Received invalid data from the server.";
            if (ex is InvalidOperationException && ex.Message.Contains("Network unavailable"))
                 return "Network unavailable. Please check your connection.";

            return "An unexpected error occurred. Please try again later.";
        }
        #endregion

        #region Helper Methods (New/Updated)

        private string BuildPaginationQueryString(RequestPaginationDto pagination)
        {
            var queryParams = new List<string>();
            queryParams.Add($"page={pagination.Page}");
            queryParams.Add($"pageSize={pagination.PageSize}");
            if (!string.IsNullOrEmpty(pagination.SortBy))
            {
                queryParams.Add($"sortBy={Uri.EscapeDataString(pagination.SortBy)}");
            }
            queryParams.Add($"ascending={pagination.Ascending}");
            if (pagination.FilterStatus.HasValue)
            {
                queryParams.Add($"filterStatus={Uri.EscapeDataString(pagination.FilterStatus.Value.ToString())}");
            }
             if (pagination.FilterType.HasValue)
            {
                queryParams.Add($"filterType={Uri.EscapeDataString(pagination.FilterType.Value.ToString())}");
            }
            if (pagination.FromDate.HasValue)
            {
                queryParams.Add($"fromDate={pagination.FromDate.Value:o}");
            }
            if (pagination.ToDate.HasValue)
            {
                queryParams.Add($"toDate={pagination.ToDate.Value:o}");
            }
            if (pagination.UserId.HasValue)
            {
                queryParams.Add($"userId={pagination.UserId.Value}");
            }
            if (!string.IsNullOrEmpty(pagination.Department))
            {
                queryParams.Add($"department={Uri.EscapeDataString(pagination.Department)}");
            }

            return queryParams.Any() ? "?" + string.Join("&", queryParams) : string.Empty;
        }

        private string BuildPaginationQueryString(MessagePaginationDto pagination, int? userId = null)
        {
            var queryParams = new List<string>();
            if (pagination.PageNumber > 0) queryParams.Add($"pageNumber={pagination.PageNumber}");
            if (pagination.PageSize > 0) queryParams.Add($"pageSize={pagination.PageSize}");
            if (!pagination.SortDescending) queryParams.Add($"sortDesc=false");
            if (userId.HasValue) queryParams.Add($"userId={userId.Value}");
            if (pagination.UnreadOnly) queryParams.Add("unreadOnly=true");
            if (pagination.FromUserId.HasValue) queryParams.Add($"fromUserId={pagination.FromUserId.Value}");

            return queryParams.Any() ? $"?{string.Join("&", queryParams)}" : "";
        }

        #endregion

        // AUTH
        public async Task<ApiResponse<UserDto>> GetCurrentUserAsync()
        {
            if (!_initialized) await InitializeAuthenticationAsync();
            string endpoint = ApiRoutes.Users.GetCurrent;
            try
            {
                var response = await GetAsync<ApiResponse<UserDto>>(endpoint);
                return response ?? new ApiResponse<UserDto> { Success = false, Message = "Failed to get current user" };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "ApiService: Failed to get current user: {Message}", ex.Message);
                return new ApiResponse<UserDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<int>> GetCurrentUserIdAsync()
        {
            var userResponse = await GetCurrentUserAsync();
            if (userResponse.Success && userResponse.Data != null)
                return new ApiResponse<int> { Success = true, Data = userResponse.Data.UserID };
            return new ApiResponse<int> { Success = false, Message = userResponse.Message };
        }

        public async Task<ApiResponse<bool>> IsAuthenticatedAsync()
        {
            try
            {
                var tokenInfo = await _secureStorage.GetTokenAsync();
                bool isValid = !string.IsNullOrEmpty(tokenInfo.Token) && tokenInfo.Expiration > DateTime.UtcNow;
                return new ApiResponse<bool> { Success = true, Data = isValid };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error checking authentication status: {Message}", ex.Message);
                return new ApiResponse<bool> { Success = false, Message = ex.Message };
            }
        }

        // REQUESTS
        public async Task<ApiResponse<RequestResponseDto>> GetRequestByIdAsync(int requestId, bool queueIfUnavailable = true)
        {
            try
            {
                string endpoint = string.Format(ApiRoutes.Requests.GetById, requestId);
                var response = await GetAsync<ApiResponse<RequestResponseDto>>(endpoint, queueIfUnavailable);
                return response ?? new ApiResponse<RequestResponseDto> { Success = false, Message = "Failed to get request" };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error getting request {RequestId}: {Message}", requestId, ex.Message);
                return new ApiResponse<RequestResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<RequestResponseDto>> CreateRequestAsync(RequestCreateDto requestDto)
        {
            try
            {
                _logger?.LogInformation("Creating request with data: {@RequestDto}", requestDto);
                var response = await PostAsync<RequestCreateDto, ApiResponse<RequestResponseDto>>(ApiRoutes.Requests.Base, requestDto);
                
                if (response == null)
                {
                    _logger?.LogWarning("CreateRequestAsync: Received null response from server");
                    return new ApiResponse<RequestResponseDto> { Success = false, Message = "Failed to create request: No response from server" };
                }

                if (!response.Success)
                {
                    _logger?.LogWarning("CreateRequestAsync: Server returned unsuccessful response: {Message}", response.Message);
                    return response;
                }

                if (response.Data == null)
                {
                    _logger?.LogWarning("CreateRequestAsync: Server returned success but no data");
                    return new ApiResponse<RequestResponseDto> { Success = false, Message = "Failed to create request: No data received" };
                }

                _logger?.LogInformation("Successfully created request with ID: {RequestId}", response.Data.RequestID);
                return response;
            }
            catch (HttpRequestException ex)
            {
                _logger?.LogError(ex, "Network error creating request: {Message}", ex.Message);
                return new ApiResponse<RequestResponseDto> { Success = false, Message = "Network error: " + ex.Message };
            }
            catch (JsonException ex)
            {
                _logger?.LogError(ex, "JSON parsing error creating request: {Message}", ex.Message);
                return new ApiResponse<RequestResponseDto> { Success = false, Message = "Error processing server response" };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Unexpected error creating request: {Message}", ex.Message);
                return new ApiResponse<RequestResponseDto> { Success = false, Message = "An unexpected error occurred" };
            }
        }

        public async Task<ApiResponse<RequestResponseDto>> UpdateRequestAsync(int requestId, RequestUpdateDto requestDto)
        {
            try
            {
                string endpoint = string.Format(ApiRoutes.Requests.Update, requestId);
                var response = await PutAsync<RequestUpdateDto, ApiResponse<RequestResponseDto>>(endpoint, requestDto);
                return response ?? new ApiResponse<RequestResponseDto> { Success = false, Message = "Failed to update request" };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error updating request {RequestId}: {Message}", requestId, ex.Message);
                return new ApiResponse<RequestResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<bool>> DeleteRequestAsync(int requestId)
        {
            try
            {
                string endpoint = string.Format(ApiRoutes.Requests.Delete, requestId);
                var response = await DeleteAsync(endpoint);
                return new ApiResponse<bool> { Success = response.IsSuccessStatusCode, Data = response.IsSuccessStatusCode };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error deleting request {RequestId}: {Message}", requestId, ex.Message);
                return new ApiResponse<bool> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<bool>> ManagerApproveRequestAsync(int requestId, ManagerApprovalDto approvalDto)
        {
            try
            {
                string endpoint = $"{TDFShared.Constants.ApiRoutes.Base}/requests/{requestId}/manager/approve";
                var response = await PostAsync<ManagerApprovalDto, ApiResponse<bool>>(endpoint, approvalDto);
                return response ?? new ApiResponse<bool> { Success = false, Message = "Failed to approve request" };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error approving request {RequestId} as manager: {Message}", requestId, ex.Message);
                return new ApiResponse<bool> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<bool>> HRApproveRequestAsync(int requestId, HRApprovalDto approvalDto)
        {
            try
            {
                string endpoint = $"{TDFShared.Constants.ApiRoutes.Base}/requests/{requestId}/hr/approve";
                var response = await PostAsync<HRApprovalDto, ApiResponse<bool>>(endpoint, approvalDto);
                return response ?? new ApiResponse<bool> { Success = false, Message = "Failed to approve request" };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error approving request {RequestId} as HR: {Message}", requestId, ex.Message);
                return new ApiResponse<bool> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<bool>> ManagerRejectRequestAsync(int requestId, ManagerRejectDto rejectDto)
        {
            try
            {
                string endpoint = $"{TDFShared.Constants.ApiRoutes.Base}/requests/{requestId}/manager/reject";
                var response = await PostAsync<ManagerRejectDto, ApiResponse<bool>>(endpoint, rejectDto);
                return response ?? new ApiResponse<bool> { Success = false, Message = "Failed to reject request" };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error rejecting request {RequestId} as manager: {Message}", requestId, ex.Message);
                return new ApiResponse<bool> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<bool>> HRRejectRequestAsync(int requestId, HRRejectDto rejectDto)
        {
            try
            {
                string endpoint = $"{TDFShared.Constants.ApiRoutes.Base}/requests/{requestId}/hr/reject";
                var response = await PostAsync<HRRejectDto, ApiResponse<bool>>(endpoint, rejectDto);
                return response ?? new ApiResponse<bool> { Success = false, Message = "Failed to reject request" };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error rejecting request {RequestId} as HR: {Message}", requestId, ex.Message);
                return new ApiResponse<bool> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<PaginatedResult<RequestResponseDto>>> GetRequestsAsync(RequestPaginationDto pagination, int? userId = null, string department = null, bool queueIfUnavailable = true)
        {
            try
            {
                string endpoint = ApiRoutes.Requests.Base;
                var queryParams = new List<string>();
                if (userId.HasValue)
                    queryParams.Add($"userId={userId.Value}");
                if (!string.IsNullOrEmpty(department))
                    queryParams.Add($"department={Uri.EscapeDataString(department)}");
                if (pagination != null)
                {
                    queryParams.Add($"page={pagination.Page}");
                    queryParams.Add($"pageSize={pagination.PageSize}");
                    if (!string.IsNullOrEmpty(pagination.SortBy))
                        queryParams.Add($"sortBy={Uri.EscapeDataString(pagination.SortBy)}");
                    queryParams.Add($"ascending={!pagination.Ascending}");
                    if (pagination.FilterStatus.HasValue)
                        queryParams.Add($"status={pagination.FilterStatus.Value}");
                    if (pagination.CountOnly)
                        queryParams.Add($"countOnly=true");
                }
                if (queryParams.Count > 0)
                    endpoint += "?" + string.Join("&", queryParams);
                var response = await GetAsync<ApiResponse<PaginatedResult<RequestResponseDto>>>(endpoint, queueIfUnavailable);
                return response ?? new ApiResponse<PaginatedResult<RequestResponseDto>> { Success = false, Message = "Failed to get requests" };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error getting requests: {Message}", ex.Message);
                return new ApiResponse<PaginatedResult<RequestResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        // LEAVE BALANCES
        public async Task<ApiResponse<Dictionary<string, int>>> GetLeaveBalancesAsync(int userId, bool queueIfUnavailable = true)
        {
            try
            {
                string endpoint = string.Format(ApiRoutes.Requests.GetUserBalances, userId);
                var response = await GetAsync<ApiResponse<Dictionary<string, int>>>(endpoint, queueIfUnavailable);
                return response ?? new ApiResponse<Dictionary<string, int>> { Success = false, Message = "Failed to get leave balances" };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error getting leave balances: {Message}", ex.Message);
                return new ApiResponse<Dictionary<string, int>> { Success = false, Message = ex.Message };
            }
        }        // DEPARTMENTS
        public async Task<ApiResponse<List<LookupItem>>> GetDepartmentsAsync(bool queueIfUnavailable = true)
        {
            try
            {
                string endpoint = ApiRoutes.Lookups.GetDepartments;
                _logger?.LogDebug("Getting departments from endpoint: {Endpoint}", endpoint);
                
                // Since the endpoint returns ApiResponse<List<LookupItem>>, we need to avoid double wrapping
                var response = await GetAsync<ApiResponse<List<LookupItem>>>(endpoint, queueIfUnavailable);
                
                if (response == null)
                {
                    _logger?.LogWarning("GetDepartmentsAsync: Received null response from endpoint");
                    return new ApiResponse<List<LookupItem>> { Success = false, Message = "Failed to get departments: null response" };
                }

                _logger?.LogDebug("GetDepartmentsAsync: Received response - Success: {Success}, Items: {Count}",
                    response.Success, response.Data?.Count ?? 0);

                // Enhanced logging of the response
                if (response.Data != null)
                {
                    _logger?.LogDebug("GetDepartmentsAsync: First department: {FirstDepartment}",
                        response.Data.FirstOrDefault()?.Name ?? "none");
                }

                // Check if we got a valid data list
                if (response.Success && response.Data == null)
                {
                    response.Data = new List<LookupItem>();
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error getting departments: {Message}", ex.Message);
                return new ApiResponse<List<LookupItem>> { Success = false, Message = ex.Message };
            }
        }

        // LEAVE TYPES (exception: returns Task<List<LookupItem>>)
        public async Task<List<LookupItem>> GetLeaveTypesAsync(bool queueIfUnavailable = true)
        {
            try
            {
                string endpoint = ApiRoutes.Lookups.GetLeaveTypes;
                var response = await GetAsync<ApiResponse<List<LookupItem>>>(endpoint, queueIfUnavailable);
                return response?.Data ?? new List<LookupItem>();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error getting leave types: {Message}", ex.Message);
                return new List<LookupItem>();
            }
        }
    }
    #endregion
}