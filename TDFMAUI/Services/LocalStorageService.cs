using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;

namespace TDFMAUI.Services
{
    public class LocalStorageService : ILocalStorageService
    {
        private readonly ILogger<LocalStorageService> _logger;

        public LocalStorageService(ILogger<LocalStorageService> logger)
        {
            // Log constructor entry
            logger?.LogInformation("LocalStorageService constructor started.");

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Log constructor exit
            logger?.LogInformation("LocalStorageService constructor finished.");
        }

        public async Task<T> GetItemAsync<T>(string key)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            string serializedData = await SecureStorage.GetAsync(key);
            
            if (string.IsNullOrEmpty(serializedData))
                return default;

            return JsonConvert.DeserializeObject<T>(serializedData);
        }

        public async Task SetItemAsync<T>(string key, T value)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            string serializedData = JsonConvert.SerializeObject(value);
            await SecureStorage.SetAsync(key, serializedData);
        }

        public async Task RemoveItemAsync(string key)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            SecureStorage.Remove(key);
            await Task.CompletedTask;
        }

        public async Task ClearAsync()
        {
            SecureStorage.RemoveAll();
            await Task.CompletedTask;
        }
    }
} 