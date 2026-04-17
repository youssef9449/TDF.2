using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
// TDFShared references
using TDFShared.Constants;
using TDFShared.DTOs.Auth;
using TDFShared.DTOs.Messages;
using TDFShared.DTOs.Requests;
using TDFShared.DTOs.Users;
using TDFShared.DTOs.Common;
using TDFShared.Exceptions;
using TDFShared.Models.User;
using TDFShared.Models;
using TDFShared.Services;

namespace TDFMAUI.Services
{
    public class ApiService : IApiService, IAuthApiService, IUserApiService, IRequestApiService, IMessageService, ILookupApiService, IDisposable
    {
        private readonly TDFShared.Services.IHttpClientService _httpClientService;
        private readonly SecureStorageService _secureStorage;
        private readonly ILogger<ApiService> _logger;
        private readonly IConnectivityService _connectivityService;

        private readonly IAuthApiService _authApi;
        private readonly IUserApiService _userApi;
        private readonly IRequestApiService _requestApi;
        private readonly IMessageService _messageApi;
        private readonly ILookupApiService _lookupApi;

        private bool _isNetworkAvailable;
        private bool _initialized;

        public ApiService(
            TDFShared.Services.IHttpClientService httpClientService,
            SecureStorageService secureStorage,
            ILogger<ApiService> logger,
            IConnectivityService connectivityService,
            IAuthApiService authApi,
            IUserApiService userApi,
            IRequestApiService requestApi,
            IMessageService messageApi,
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

            _logger?.LogInformation("ApiService: Initialized");
            _isNetworkAvailable = _connectivityService.IsConnected();
            _connectivityService.ConnectivityChanged += OnNetworkStatusChanged;
        }

        private void OnNetworkStatusChanged(object? sender, TDFConnectivityChangedEventArgs e)
        {
            _isNetworkAvailable = e.IsConnected;
        }

        public void Dispose()
        {
            _connectivityService.ConnectivityChanged -= OnNetworkStatusChanged;
        }

        private async Task InitializeAuthenticationAsync()
        {
            var tokenInfo = await _secureStorage.GetTokenAsync();
            if (!string.IsNullOrEmpty(tokenInfo.Token) && tokenInfo.Expiration > DateTime.UtcNow)
            {
                await _httpClientService.SetAuthenticationTokenAsync(tokenInfo.Token);
            }
            else
            {
                await _httpClientService.ClearAuthenticationTokenAsync();
            }
            _initialized = true;
        }

        public async Task InitializeAsync()
        {
            if (!_initialized)
            {
                await InitializeAuthenticationAsync();
            }
        }

        public async Task<string> GetRawResponseAsync(string endpoint) => await _httpClientService.GetRawAsync(endpoint);
        public async Task<T> GetAsync<T>(string endpoint, bool queueIfUnavailable = false) => await _httpClientService.GetAsync<T>(endpoint);

        public async Task<bool> TestConnectivityAsync() => await _httpClientService.TestConnectivityAsync();

        // IAuthApiService
        public async Task<ApiResponse<TokenResponse>> LoginAsync(LoginRequestDto loginRequest) => await _authApi.LoginAsync(loginRequest);
        public async Task<ApiResponse<bool>> LogoutAsync() => await _authApi.LogoutAsync();
        public async Task<ApiResponse<RegisterResponseDto>> RegisterAsync(RegisterRequestDto registerRequest) => await _authApi.RegisterAsync(registerRequest);
        public async Task<ApiResponse<TokenResponse>> RefreshTokenAsync(string token, string refreshToken) => await _authApi.RefreshTokenAsync(token, refreshToken);
        public async Task<ApiResponse<bool>> IsAuthenticatedAsync() => await _authApi.IsAuthenticatedAsync();
        public async Task<bool> RegisterPushTokenAsync(PushTokenRegistrationDto registration) => await _authApi.RegisterPushTokenAsync(registration);
        public async Task<bool> UnregisterPushTokenAsync(string token) => await _authApi.UnregisterPushTokenAsync(token);

        // IUserApiService
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
        public async Task<UserPresenceInfo> GetUserStatusAsync(int userId) => await _userApi.GetUserStatusAsync(userId);
        public async Task<PaginatedResult<UserPresenceInfo>> GetOnlineUsersPresenceAsync(int page = 1, int pageSize = 100) => await _userApi.GetOnlineUsersPresenceAsync(page, pageSize);
        public async Task UpdateUserConnectionStatusAsync(int userId, bool isConnected) => await _userApi.UpdateUserConnectionStatusAsync(userId, isConnected);
        public async Task<ApiResponse<UserDto>> GetCurrentUserAsync() => await _userApi.GetCurrentUserAsync();
        public async Task<ApiResponse<int>> GetCurrentUserIdAsync() => await _userApi.GetCurrentUserIdAsync();

        // IRequestApiService
        public async Task<ApiResponse<RequestResponseDto>> GetRequestByIdAsync(int requestId, bool queueIfUnavailable = true) => await _requestApi.GetRequestByIdAsync(requestId, queueIfUnavailable);
        public async Task<ApiResponse<RequestResponseDto>> CreateRequestAsync(RequestCreateDto requestDto) => await _requestApi.CreateRequestAsync(requestDto);
        public async Task<ApiResponse<RequestResponseDto>> UpdateRequestAsync(int requestId, RequestUpdateDto requestDto) => await _requestApi.UpdateRequestAsync(requestId, requestDto);
        public async Task<ApiResponse<bool>> DeleteRequestAsync(int requestId) => await _requestApi.DeleteRequestAsync(requestId);
        public async Task<ApiResponse<bool>> ManagerApproveRequestAsync(int requestId, ManagerApprovalDto approvalDto) => await _requestApi.ManagerApproveRequestAsync(requestId, approvalDto);
        public async Task<ApiResponse<bool>> HRApproveRequestAsync(int requestId, HRApprovalDto approvalDto) => await _requestApi.HRApproveRequestAsync(requestId, approvalDto);
        public async Task<ApiResponse<bool>> ManagerRejectRequestAsync(int requestId, ManagerRejectDto rejectDto) => await _requestApi.ManagerRejectRequestAsync(requestId, rejectDto);
        public async Task<ApiResponse<bool>> HRRejectRequestAsync(int requestId, HRRejectDto rejectDto) => await _requestApi.HRRejectRequestAsync(requestId, rejectDto);
        public async Task<ApiResponse<PaginatedResult<RequestResponseDto>>> GetRequestsAsync(RequestPaginationDto pagination, int? userId = null, string? department = null, bool queueIfUnavailable = true) => await _requestApi.GetRequestsAsync(pagination, userId, department, queueIfUnavailable);
        public async Task<ApiResponse<PaginatedResult<RequestResponseDto>>> GetRequestsForApprovalAsync(RequestPaginationDto pagination, bool queueIfUnavailable = true) => await _requestApi.GetRequestsForApprovalAsync(pagination, queueIfUnavailable);
        public async Task<ApiResponse<Dictionary<string, int>>> GetLeaveBalancesAsync(int userId, bool queueIfUnavailable = true) => await _requestApi.GetLeaveBalancesAsync(userId, queueIfUnavailable);

        // IMessageService
        public async Task<PaginatedResult<MessageDto>> GetUserMessagesAsync(int userId, MessagePaginationDto pagination) => await _messageApi.GetUserMessagesAsync(userId, pagination);
        public async Task<PaginatedResult<MessageDto>> GetAllMessagesAsync(MessagePaginationDto pagination) => await _messageApi.GetAllMessagesAsync(pagination);
        public async Task<MessageDto> CreateMessageAsync(MessageCreateDto createDto) => await _messageApi.CreateMessageAsync(createDto);
        public async Task<ChatMessageDto> CreateChatMessageAsync(ChatMessageCreateDto createDto) => await _messageApi.CreateChatMessageAsync(createDto);
        public async Task<bool> MarkMessageAsReadAsync(int messageId) => await _messageApi.MarkMessageAsReadAsync(messageId);
        public async Task<bool> MarkMessagesAsReadAsync(List<int> messageIds) => await _messageApi.MarkMessagesAsReadAsync(messageIds);
        public async Task<List<ChatMessageDto>> GetRecentChatMessagesAsync(int count = 50) => await _messageApi.GetRecentChatMessagesAsync(count);
        public async Task<PaginatedResult<MessageDto>> GetPrivateMessagesAsync(int userId, MessagePaginationDto pagination) => await _messageApi.GetPrivateMessagesAsync(userId, pagination);
        public async Task<int> GetUnreadMessagesCountAsync(int userId) => await _messageApi.GetUnreadMessagesCountAsync(userId);

        // ILookupApiService
        public async Task<ApiResponse<List<LookupItem>>> GetDepartmentsAsync(bool queueIfUnavailable = true) => await _lookupApi.GetDepartmentsAsync(queueIfUnavailable);
        public async Task<List<LookupItem>> GetLeaveTypesAsync(bool queueIfUnavailable = true) => await _lookupApi.GetLeaveTypesAsync(queueIfUnavailable);
        public async Task<ApiResponse<List<LookupItem>>> GetRequestTypesAsync(bool queueIfUnavailable = true) => await _lookupApi.GetRequestTypesAsync(queueIfUnavailable);
        public async Task<ApiResponse<List<LookupItem>>> GetStatusCodesAsync(bool queueIfUnavailable = true) => await _lookupApi.GetStatusCodesAsync(queueIfUnavailable);
    }
}
