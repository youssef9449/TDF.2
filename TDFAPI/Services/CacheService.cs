using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace TDFAPI.Services
{
    /// <summary>
    /// Service for caching data to improve performance
    /// </summary>
    public class CacheService : ICacheService
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<CacheService> _logger;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

        public CacheService(IMemoryCache cache, ILogger<CacheService> logger)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets a value from cache if available, otherwise executes the factory method and caches the result
        /// </summary>
        /// <typeparam name="T">Type of cached item</typeparam>
        /// <param name="key">Cache key</param>
        /// <param name="factory">Factory method to generate value if not in cache</param>
        /// <param name="absoluteExpirationMinutes">Absolute expiration time in minutes</param>
        /// <param name="slidingExpirationMinutes">Sliding expiration time in minutes</param>
        /// <returns>The cached or generated value</returns>
        public async Task<T> GetOrCreateAsync<T>(
            string key,
            Func<Task<T>> factory,
            int absoluteExpirationMinutes = 30,
            int slidingExpirationMinutes = 10)
        {
            if (_cache.TryGetValue(key, out T cachedValue))
            {
                _logger.LogDebug("Cache hit for key: {Key}", key);
                return cachedValue;
            }

            var lockObj = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            await lockObj.WaitAsync();

            try
            {
                // Double-check after acquiring the lock
                if (_cache.TryGetValue(key, out cachedValue))
                {
                    _logger.LogDebug("Cache hit after lock for key: {Key}", key);
                    return cachedValue;
                }

                _logger.LogDebug("Cache miss for key: {Key}", key);
                
                var result = await factory();

                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(absoluteExpirationMinutes))
                    .SetSlidingExpiration(TimeSpan.FromMinutes(slidingExpirationMinutes))
                    .RegisterPostEvictionCallback((key, value, reason, state) =>
                    {
                        _logger.LogDebug("Item with key {Key} evicted from cache. Reason: {Reason}", key, reason);
                        
                        // Try to remove the lock if the item is evicted
                        if (_locks.TryRemove(key.ToString(), out _))
                        {
                            _logger.LogDebug("Lock for key {Key} removed", key);
                        }
                    });

                _cache.Set(key, result, cacheOptions);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting or creating cache item with key: {Key}", key);
                throw;
            }
            finally
            {
                lockObj.Release();
            }
        }

        /// <summary>
        /// Sets a value in the cache with the specified expiration
        /// </summary>
        public Task<bool> SetAsync<T>(
            string key,
            T value,
            int absoluteExpirationMinutes = 30,
            int slidingExpirationMinutes = 10)
        {
            try
            {
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(absoluteExpirationMinutes))
                    .SetSlidingExpiration(TimeSpan.FromMinutes(slidingExpirationMinutes))
                    .RegisterPostEvictionCallback((key, value, reason, state) =>
                    {
                        _logger.LogDebug("Item with key {Key} evicted from cache. Reason: {Reason}", key, reason);
                        
                        // Try to remove the lock if the item is evicted
                        if (_locks.TryRemove(key.ToString(), out _))
                        {
                            _logger.LogDebug("Lock for key {Key} removed", key);
                        }
                    });

                _cache.Set(key, value, cacheOptions);
                _logger.LogDebug("Item with key {Key} set in cache", key);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting cache item with key: {Key}", key);
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// Gets a value from cache if available
        /// </summary>
        public Task<T?> GetAsync<T>(string key) where T : class
        {
            try
            {
                if (_cache.TryGetValue(key, out T? value))
                {
                    _logger.LogDebug("Cache hit for key: {Key}", key);
                    return Task.FromResult(value);
                }
                
                _logger.LogDebug("Cache miss for key: {Key}", key);
                return Task.FromResult<T?>(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cache item with key: {Key}", key);
                return Task.FromResult<T?>(null);
            }
        }

        /// <summary>
        /// Removes an item from the cache
        /// </summary>
        /// <param name="key">Cache key</param>
        public void RemoveFromCache(string key)
        {
            if (string.IsNullOrEmpty(key))
                return;
            
            _cache.Remove(key);
            
            // Clean up any associated locks
            if (_locks.TryRemove(key, out var lockObj))
            {
                _logger.LogDebug("Removed lock for key: {Key}", key);
            }
            
            _logger.LogDebug("Removed item from cache with key: {Key}", key);
        }

        /// <summary>
        /// Gets a value from cache if available
        /// </summary>
        /// <typeparam name="T">Type of cached item</typeparam>
        /// <param name="key">Cache key</param>
        /// <param name="value">Retrieved value (null if not found)</param>
        /// <returns>True if value was found in cache</returns>
        public bool TryGetValue<T>(string key, out T? value)
        {
            return _cache.TryGetValue(key, out value);
        }
    }
} 