using System;
using System.Threading.Tasks;

namespace TDFAPI.Services
{
    /// <summary>
    /// Interface for cache service
    /// </summary>
    public interface ICacheService
    {
        /// <summary>
        /// Gets a value from cache if available, otherwise executes the factory method and caches the result
        /// </summary>
        /// <typeparam name="T">Type of cached item</typeparam>
        /// <param name="key">Cache key</param>
        /// <param name="factory">Factory method to generate value if not in cache</param>
        /// <param name="absoluteExpirationMinutes">Absolute expiration time in minutes</param>
        /// <param name="slidingExpirationMinutes">Sliding expiration time in minutes</param>
        /// <returns>The cached or generated value</returns>
        Task<T> GetOrCreateAsync<T>(
            string key,
            Func<Task<T>> factory,
            int absoluteExpirationMinutes = 30,
            int slidingExpirationMinutes = 10);

        /// <summary>
        /// Sets a value in the cache with the specified expiration
        /// </summary>
        /// <typeparam name="T">Type of cached item</typeparam>
        /// <param name="key">Cache key</param>
        /// <param name="value">Value to store in cache</param>
        /// <param name="absoluteExpirationMinutes">Absolute expiration time in minutes</param>
        /// <param name="slidingExpirationMinutes">Sliding expiration time in minutes</param>
        /// <returns>True if successfully stored in cache</returns>
        Task<bool> SetAsync<T>(
            string key,
            T value,
            int absoluteExpirationMinutes = 30,
            int slidingExpirationMinutes = 10);

        /// <summary>
        /// Gets a value from cache if available
        /// </summary>
        /// <typeparam name="T">Type of cached item</typeparam>
        /// <param name="key">Cache key</param>
        /// <returns>The cached value or default if not found</returns>
        Task<T?> GetAsync<T>(string key) where T : class;

        /// <summary>
        /// Attempts to get a value from cache if available
        /// </summary>
        /// <typeparam name="T">Type of cached item</typeparam>
        /// <param name="key">Cache key</param>
        /// <param name="value">Retrieved value (default if not found)</param>
        /// <returns>True if value was found in cache</returns>
        bool TryGetValue<T>(string key, out T? value);

        /// <summary>
        /// Removes an item from the cache
        /// </summary>
        /// <param name="key">Cache key</param>
        void RemoveFromCache(string key);
    }
} 