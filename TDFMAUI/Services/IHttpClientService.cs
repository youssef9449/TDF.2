using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace TDFMAUI.Services
{
    /// <summary>
    /// Defines basic HTTP client operations.
    /// </summary>
    public interface IHttpClientService : IDisposable
    {
        Task<T> GetAsync<T>(string endpoint);
        Task<TResponse> PostAsync<TRequest, TResponse>(string endpoint, TRequest data);
        Task PostAsync<TRequest>(string endpoint, TRequest data); // Added for fire-and-forget posts
        Task<TResponse> PutAsync<TRequest, TResponse>(string endpoint, TRequest data);
        Task PutAsync<TRequest>(string endpoint, TRequest data); // Added for fire-and-forget puts
        Task<HttpResponseMessage> DeleteAsync(string endpoint);
        void SetAuthorizationHeader(string token);
        void ClearAuthorizationHeader();
        Task EnsureSuccessStatusCodeAsync(HttpResponseMessage response);
        Task<string> GetRawAsync(string endpoint);
    }
} 