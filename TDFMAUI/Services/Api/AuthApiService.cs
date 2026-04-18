using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using TDFMAUI.Config;
using TDFShared.Constants;
using TDFShared.DTOs.Auth;
using TDFShared.DTOs.Common;
using TDFShared.DTOs.Users;
using TDFShared.Exceptions;
using TDFShared.Services;

namespace TDFMAUI.Services.Api
{
    public class AuthApiService : IAuthApiService
    {
        private readonly IHttpClientService _httpClientService;
        private readonly SecureStorageService _secureStorage;
        private readonly ILogger<AuthApiService> _logger;
        private readonly IConnectivityService _connectivityService;

        public AuthApiService(
            IHttpClientService httpClientService,
            SecureStorageService secureStorage,
            ILogger<AuthApiService> logger,
            IConnectivityService connectivityService)
        {
            _httpClientService = httpClientService;
            _secureStorage = secureStorage;
            _logger = logger;
            _connectivityService = connectivityService;
        }

        public async Task<ApiResponse<TokenResponse>> LoginAsync(LoginRequestDto loginRequest)
        {
            string endpoint = ApiRoutes.Auth.Login;
            if (!_connectivityService.IsConnected())
                throw new NetworkUnavailableException();
            try
            {
                var response = await _httpClientService.PostAsync<LoginRequestDto, ApiResponse<TokenResponse>>(endpoint, loginRequest);
                if (response?.Success == true && response.Data != null)
                {
                    var tokenData = response.Data;
                    await _secureStorage.SaveTokenAsync(tokenData.Token, tokenData.Expiration, tokenData.RefreshToken, tokenData.RefreshTokenExpiration);
                    await _httpClientService.SetAuthenticationTokenAsync(tokenData.Token);
                }
                else
                {
                    await _httpClientService.ClearAuthenticationTokenAsync();
                    await _secureStorage.ClearTokenAsync();
                }
                return response ?? new ApiResponse<TokenResponse> { Success = false, Message = "Login failed" };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "AuthApiService: Login failed: {Message}", ex.Message);
                await _httpClientService.ClearAuthenticationTokenAsync();
                await _secureStorage.ClearTokenAsync();
                return new ApiResponse<TokenResponse> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<bool>> LogoutAsync()
        {
            try
            {
                await _httpClientService.ClearAuthenticationTokenAsync();
                await _secureStorage.ClearTokenAsync();
                return new ApiResponse<bool> { Success = true, Data = true };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "AuthApiService: Logout failed: {Message}", ex.Message);
                return new ApiResponse<bool> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<RegisterResponseDto>> RegisterAsync(RegisterRequestDto registerRequest)
        {
            string endpoint = ApiRoutes.Auth.Register;
            try
            {
                _logger?.LogInformation("AuthApiService: Attempting registration for user {Username}", registerRequest.Username);
                var response = await _httpClientService.PostAsync<RegisterRequestDto, ApiResponse<RegisterResponseDto>>(endpoint, registerRequest);
                return response ?? new ApiResponse<RegisterResponseDto> { Success = false, Message = "Registration failed" };
            }
            catch (ApiException ex)
            {
                _logger?.LogError(ex, "AuthApiService: API error during registration for {Username}: {Message}", registerRequest.Username, ex.Message);
                return new ApiResponse<RegisterResponseDto> { Success = false, Message = ex.Message, StatusCode = (int)ex.StatusCode };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "AuthApiService: Error during registration for {Username}: {Message}", registerRequest.Username, ex.Message);
                return new ApiResponse<RegisterResponseDto> { Success = false, Message = ex.Message };
            }
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
                _logger?.LogError(ex, "AuthApiService: Error checking authentication status: {Message}", ex.Message);
                return new ApiResponse<bool> { Success = false, Message = ex.Message };
            }
        }

        public async Task<bool> RegisterPushTokenAsync(PushTokenRegistrationDto registration)
        {
            try
            {
                var response = await _httpClientService.PostAsync<PushTokenRegistrationDto, ApiResponse<bool>>(ApiRoutes.PushToken.Register, registration);
                return response?.Success ?? false;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "AuthApiService: Push token registration failed");
                return false;
            }
        }

        public async Task<bool> UnregisterPushTokenAsync(string token)
        {
            try
            {
                var response = await _httpClientService.PostAsync<object, ApiResponse<bool>>(ApiRoutes.PushToken.Unregister, new { Token = token });
                return response?.Success ?? false;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "AuthApiService: Push token unregistration failed");
                return false;
            }
        }
    }
}
