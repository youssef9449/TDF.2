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
using TDFShared.Models; // Added for ApiResponseBase
using System.Net.NetworkInformation;
using TDFMAUI.Config; // Added
using TDFMAUI.Helpers; // Added for DeviceHelper
using TDFShared.Services;
using ApiDTOs = TDFShared.DTOs.Common;

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
    public class ApiService : IApiService, IAuthApiService, IUserApiService, IRequestApiService, IMessageApiService, ILookupApiService, IDisposable
    {
        private readonly TDFShared.Services.IHttpClientService _httpClientService;
        private readonly SecureStorageService _secureStorage;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly ILogger<ApiService> _logger;
        private readonly IConnectivityService _connectivityService;

        private readonly IAuthApiService _authApi;
        private readonly IUserApiService _userApi;
        private readonly IRequestApiService _requestApi;
        private readonly IMessageApiService _messageApi;
        private readonly ILookupApiService _lookupApi;

        private bool _isNetworkAvailable;
        private bool _isApiReachable;
        private DateTime _lastApiCheck = DateTime.MinValue;
        private readonly TimeSpan _apiCheckInterval = TimeSpan.FromSeconds(30);
        private readonly SemaphoreSlim _reconnectLock = new SemaphoreSlim(1, 1);
        private bool _isReconnecting;
        private readonly object _stateLock = new object();
        private readonly Dictionary<string, Queue<PendingRequest>> _pendingRequests = new Dictionary<string, Queue<PendingRequest>>();
        private bool _initialized;

        /// <summary>
        /// Event raised when the API becomes reachable
        /// </summary>
        public event EventHandler? ApiReachable;

        /// <summary>
        /// Event raised when the API becomes unreachable
        /// </summary>
        public event EventHandler? ApiUnreachable;

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
            ILogger<ApiService> logger,
            IConnectivityService connectivityService,
            IAuthApiService authApi,
            IUserApiService userApi,
            IRequestApiService requestApi,
            IMessageApiService messageApi,
            ILookupApiService lookupApi)
        {
            _httpClientService = httpClientService;
            _secureStorage = secureStorage;
            _logger = logger;
            _connectivityService = connectivityService;

            _authApi = authApi;
            _userApi = userApi;
            _requestApi = requestApi;
            _messageApi = messageApi;
            _lookupApi = lookupApi;

            _jsonOptions = TDFShared.Helpers.JsonSerializationHelper.DefaultOptions;

            _logger?.LogInformation("ApiService: Initialized");
            _isNetworkAvailable = _connectivityService.IsConnected();
            _connectivityService.ConnectivityChanged += OnNetworkStatusChanged;
            _initialized = false;
        }

        private async Task ReconnectAsync()
        {
            try
            {
                _isApiReachable = await TestConnectivityAsync();
                _logger?.LogInformation("ApiService: Reconnection attempt {Status}", (_isApiReachable ? "successful" : "failed"));
            }
            catch (Exception ex)
            {
                _isApiReachable = false;
                _logger?.LogError(ex, "ApiService: Error during reconnection attempt");
            }
        }

        private void OnNetworkStatusChanged(object? sender, TDFConnectivityChangedEventArgs e)
        {
            lock (_stateLock)
            {
                _isNetworkAvailable = e.IsConnected;
                _logger.LogInformation("Network status changed: {IsConnected}", e.IsConnected);

                if (e.IsConnected)
                {
                    _ = TestApiConnectivityAsync();
                }
                else
                {
                    _isApiReachable = false;
                    ApiUnreachable?.Invoke(this, EventArgs.Empty);
                }
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
            _connectivityService.ConnectivityChanged -= OnNetworkStatusChanged;
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
        public async Task<bool> RegisterPushTokenAsync(PushTokenRegistrationDto registration) => await _authApi.RegisterPushTokenAsync(registration);
        public async Task<bool> UnregisterPushTokenAsync(string token) => await _authApi.UnregisterPushTokenAsync(token);
        public async Task<ApiResponse<TokenResponse>> LoginAsync(LoginRequestDto loginRequest) => await _authApi.LoginAsync(loginRequest);
        public async Task<ApiResponse<bool>> LogoutAsync() => await _authApi.LogoutAsync();
        public async Task<ApiResponse<RegisterResponseDto>> RegisterAsync(RegisterRequestDto registerRequest) => await _authApi.RegisterAsync(registerRequest);

        #region User Operations (Now returns DTOs)
        public async Task<UserDto> GetUserByIdAsync(int userId) => await _userApi.GetUserByIdAsync(userId);
        public async Task<UserDto> GetUserAsync(int userId) => await _userApi.GetUserAsync(userId);
        public async Task<ApiResponse<UserProfileDto>> GetUserProfileAsync(int userId) => await _userApi.GetUserProfileAsync(userId);
        public async Task<PaginatedResult<UserDto>> GetAllUsersAsync(int page = 1, int pageSize = 10) => await _userApi.GetAllUsersAsync(page, pageSize);
        public async Task<List<UserDto>> GetUsersByDepartmentAsync(string department) => await _userApi.GetUsersByDepartmentAsync(department);
        public async Task<UserDto> CreateUserAsync(CreateUserRequest userRequest) => await _userApi.CreateUserAsync(userRequest);
        public async Task<UserDto> UpdateUserAsync(int userId, UpdateUserRequest userDto) => await _userApi.UpdateUserAsync(userId, userDto);
        public async Task DeleteUserAsync(int userId) => await _userApi.DeleteUserAsync(userId);
        public async Task<bool> ChangePasswordAsync(ChangePasswordDto changePasswordDto) => await _userApi.ChangePasswordAsync(changePasswordDto);
        public async Task<bool> UploadProfilePictureAsync(Stream imageStream, string fileName, string contentType) => await _userApi.UploadProfilePictureAsync(imageStream, fileName, contentType);
        public async Task<bool> UpdateUserProfileAsync(UpdateMyProfileRequest profileData) => await _userApi.UpdateUserProfileAsync(profileData);
        #endregion

        #region Message Operations (Return DTOs)
        public async Task<PaginatedResult<ChatMessageDto>> GetUserMessagesAsync(int userId, MessagePaginationDto pagination) => await _messageApi.GetUserMessagesAsync(userId, pagination);
        public async Task<PaginatedResult<ChatMessageDto>> GetAllMessagesAsync(MessagePaginationDto pagination) => await _messageApi.GetAllMessagesAsync(pagination);
        public async Task<ChatMessageDto> CreateMessageAsync(MessageCreateDto createDto) => await _messageApi.CreateMessageAsync(createDto);
        public async Task<ChatMessageDto> CreatePrivateMessageAsync(MessageCreateDto createDto) => await _messageApi.CreatePrivateMessageAsync(createDto);
        public async Task<bool> MarkMessageAsReadAsync(int messageId) => await _messageApi.MarkMessageAsReadAsync(messageId);
        public async Task<bool> MarkMessagesAsReadAsync(List<int> messageIds) => await _messageApi.MarkMessagesAsReadAsync(messageIds);
        public async Task<List<ChatMessageDto>> GetRecentChatMessagesAsync(int count = 50) => await _messageApi.GetRecentChatMessagesAsync(count);
        public async Task<PaginatedResult<ChatMessageDto>> GetPrivateMessagesAsync(int userId, MessagePaginationDto pagination) => await _messageApi.GetPrivateMessagesAsync(userId, pagination);
        public async Task<int> GetUnreadMessagesCountAsync(int userId) => await _messageApi.GetUnreadMessagesCountAsync(userId);

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
                return await _httpClientService.TestConnectivityAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "ApiService: Error testing connectivity");
                return false;
            }
        }

        private async Task TestApiConnectivityAsync()
        {
            try
            {
                _isApiReachable = await _httpClientService.TestConnectivityAsync();
                if (_isApiReachable)
                {
                    ApiReachable?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    ApiUnreachable?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "ApiService: Error testing API connectivity");
                _isApiReachable = false;
                ApiUnreachable?.Invoke(this, EventArgs.Empty);
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
        public async Task<UserPresenceInfo> GetUserStatusAsync(int userId) => await _userApi.GetUserStatusAsync(userId);
        public async Task<PaginatedResult<UserPresenceInfo>> GetOnlineUsersPresenceAsync(int page = 1, int pageSize = 100) => await _userApi.GetOnlineUsersPresenceAsync(page, pageSize);
        public async Task UpdateUserConnectionStatusAsync(int userId, bool isConnected) => await _userApi.UpdateUserConnectionStatusAsync(userId, isConnected);
        public async Task<ApiResponse<UserDto>> GetCurrentUserAsync() => await _userApi.GetCurrentUserAsync();
        public async Task<ApiResponse<int>> GetCurrentUserIdAsync() => await _userApi.GetCurrentUserIdAsync();
        public async Task<ApiResponse<bool>> IsAuthenticatedAsync() => await _authApi.IsAuthenticatedAsync();

        // REQUESTS
        public async Task<ApiResponse<RequestResponseDto>> GetRequestByIdAsync(int requestId, bool queueIfUnavailable = true) => await _requestApi.GetRequestByIdAsync(requestId, queueIfUnavailable);
        public async Task<ApiResponse<RequestResponseDto>> CreateRequestAsync(RequestCreateDto requestDto) => await _requestApi.CreateRequestAsync(requestDto);
        public async Task<ApiResponse<RequestResponseDto>> UpdateRequestAsync(int requestId, RequestUpdateDto requestDto) => await _requestApi.UpdateRequestAsync(requestId, requestDto);
        public async Task<ApiResponse<bool>> DeleteRequestAsync(int requestId) => await _requestApi.DeleteRequestAsync(requestId);
        public async Task<ApiResponse<bool>> ManagerApproveRequestAsync(int requestId, ManagerApprovalDto approvalDto) => await _requestApi.ManagerApproveRequestAsync(requestId, approvalDto);
        public async Task<ApiResponse<bool>> HRApproveRequestAsync(int requestId, HRApprovalDto approvalDto) => await _requestApi.HRApproveRequestAsync(requestId, approvalDto);
        public async Task<ApiResponse<bool>> ManagerRejectRequestAsync(int requestId, ManagerRejectDto rejectDto) => await _requestApi.ManagerRejectRequestAsync(requestId, rejectDto);
        public async Task<ApiResponse<bool>> HRRejectRequestAsync(int requestId, HRRejectDto rejectDto) => await _requestApi.HRRejectRequestAsync(requestId, rejectDto);
        public async Task<ApiResponse<PaginatedResult<RequestResponseDto>>> GetRequestsAsync(RequestPaginationDto pagination, int? userId = null, string department = null, bool queueIfUnavailable = true) => await _requestApi.GetRequestsAsync(pagination, userId, department, queueIfUnavailable);
        public async Task<ApiResponse<PaginatedResult<RequestResponseDto>>> GetRequestsForApprovalAsync(RequestPaginationDto pagination, bool queueIfUnavailable = true) => await _requestApi.GetRequestsForApprovalAsync(pagination, queueIfUnavailable);

        // LEAVE BALANCES
        public async Task<ApiResponse<Dictionary<string, int>>> GetLeaveBalancesAsync(int userId, bool queueIfUnavailable = true) => await _requestApi.GetLeaveBalancesAsync(userId, queueIfUnavailable);

        // DEPARTMENTS
        public async Task<ApiResponse<List<LookupItem>>> GetDepartmentsAsync(bool queueIfUnavailable = true) => await _lookupApi.GetDepartmentsAsync(queueIfUnavailable);

        // LEAVE TYPES
        public async Task<List<LookupItem>> GetLeaveTypesAsync(bool queueIfUnavailable = true) => await _lookupApi.GetLeaveTypesAsync(queueIfUnavailable);

        private async Task<T> ProcessResponseAsync<T>(HttpResponseMessage response, string endpoint)
        {
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                var errorResponse = JsonSerializer.Deserialize<ApiResponseBase<T>>(content, _jsonOptions);
                if (errorResponse?.ValidationErrors != null)
                {
                    var validationErrors = errorResponse.ValidationErrors.ToDictionary(
                        kvp => kvp.Key,
                        kvp => new[] { kvp.Value }
                    );
                    throw new ApiException(response.StatusCode, errorResponse.ErrorMessage ?? "Unknown error", validationErrors, content);
                }
                throw new ApiException(response.StatusCode, errorResponse?.ErrorMessage ?? $"Error calling {endpoint}: {response.StatusCode}", content);
            }

            var result = JsonSerializer.Deserialize<T>(content, _jsonOptions);
            if (result == null)
            {
                throw new ApiException(response.StatusCode, $"Failed to deserialize response from {endpoint}", content);
            }
            return result;
        }
    }
    #endregion
}