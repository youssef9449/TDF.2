using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TDFMAUI.Config;
using TDFShared.Constants;
using TDFShared.DTOs.Auth;
using TDFShared.DTOs.Common;

namespace TDFMAUI.Services
{
    public class ApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;
        private string? _authToken;
        
        public ApiClient()
        {
            var handler = new HttpClientHandler();
            if (ApiConfig.DevelopmentMode)
            {
                handler.ServerCertificateCustomValidationCallback = 
                    (sender, cert, chain, sslPolicyErrors) => true;
            }
            
            _httpClient = new HttpClient(handler);
            _httpClient.Timeout = TimeSpan.FromSeconds(ApiConfig.Timeout);
            
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }
        
        public bool IsAuthenticated => !string.IsNullOrEmpty(_authToken);
        
        public async Task<bool> LoginAsync(string username, string password)
        {
            try
            {
                var loginRequest = new LoginRequest
                {
                    Username = username,
                    Password = password
                };
                
                var content = new StringContent(
                    JsonSerializer.Serialize(loginRequest, _jsonOptions),
                    Encoding.UTF8,
                    new MediaTypeHeaderValue("application/json"));
                    
                var response = await _httpClient.PostAsync(
                    $"{ApiConfig.BaseUrl.TrimEnd('/')}/{ApiRoutes.Auth.Login}", 
                    content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var loginResponse = JsonSerializer.Deserialize<ApiResponse<TokenResponse>>(
                        responseContent, 
                        _jsonOptions);
                        
                    if (loginResponse?.Success == true && loginResponse.Data != null)
                    {
                        _authToken = loginResponse.Data.Token;
                        _httpClient.DefaultRequestHeaders.Authorization = 
                            new AuthenticationHeaderValue("Bearer", _authToken);
                            
                        App.CurrentUser = loginResponse.Data.User;
                        return true;
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Login error: {ex.Message}");
                return false;
            }
        }
        
        public void Logout()
        {
            _authToken = null;
            _httpClient.DefaultRequestHeaders.Authorization = null;
            App.CurrentUser = null;
        }
        
        public async Task<T?> GetAsync<T>(string endpoint) where T : class
        {
            try
            {
                var response = await _httpClient.GetAsync($"{ApiConfig.BaseUrl.TrimEnd('/')}/{endpoint}");
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<T>>(
                    content,
                    _jsonOptions);
                    
                return apiResponse?.Data;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"API error: {ex.Message}");
                return null;
            }
        }
        
        public async Task<TResponse?> PostAsync<TRequest, TResponse>(string endpoint, TRequest data) 
            where TRequest : class
            where TResponse : class
        {
            try
            {
                var content = new StringContent(
                    JsonSerializer.Serialize(data, _jsonOptions),
                    Encoding.UTF8,
                    new MediaTypeHeaderValue("application/json"));
                    
                var response = await _httpClient.PostAsync(
                    $"{ApiConfig.BaseUrl.TrimEnd('/')}/{endpoint}", 
                    content);
                response.EnsureSuccessStatusCode();
                
                var responseContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<TResponse>>(
                    responseContent,
                    _jsonOptions);
                    
                return apiResponse?.Data;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"API error: {ex.Message}");
                return null;
            }
        }
        
        public async Task<TResponse?> PutAsync<TRequest, TResponse>(string endpoint, TRequest data) 
            where TRequest : class
            where TResponse : class
        {
            try
            {
                var content = new StringContent(
                    JsonSerializer.Serialize(data),
                    Encoding.UTF8,
                    new MediaTypeHeaderValue("application/json"));
                    
                var response = await _httpClient.PutAsync($"{ApiConfig.BaseUrl.TrimEnd('/')}/{endpoint}", content);
                response.EnsureSuccessStatusCode();
                
                var responseContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<TResponse>>(
                    responseContent,
                    _jsonOptions);
                    
                return apiResponse?.Data;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"API error: {ex.Message}");
                return null;
            }
        }
        
        public async Task<bool> DeleteAsync(string endpoint)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"{ApiConfig.BaseUrl.TrimEnd('/')}/{endpoint}");
                response.EnsureSuccessStatusCode();
                
                var responseContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<bool>>(
                    responseContent,
                    _jsonOptions);
                    
                return apiResponse?.Success == true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"API error: {ex.Message}");
                return false;
            }
        }      
    }
} 