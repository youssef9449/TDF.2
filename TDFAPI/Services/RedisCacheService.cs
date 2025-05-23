using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace TDFAPI.Services
{
    /// <summary>
    /// Redis-based cache service implementation for distributed caching
    /// </summary>
    public class RedisCacheService : ICacheService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<RedisCacheService> _logger;
        private readonly IDatabase _db;

        public RedisCacheService(IConnectionMultiplexer redis, ILogger<RedisCacheService> logger)
        {
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _db = _redis.GetDatabase();
        }

        /// <summary>
        /// Gets a value from cache if available, otherwise executes the factory method and caches the result
        /// </summary>
        public async Task<T> GetOrCreateAsync<T>(
            string key,
            Func<Task<T>> factory,
            int absoluteExpirationMinutes = 30,
            int slidingExpirationMinutes = 10)
        {
            // Try to get the value from cache
            var cachedValue = await _db.StringGetAsync(key);
            if (cachedValue.HasValue)
            {
                _logger.LogDebug("Cache hit for key: {Key}", key);
                try
                {
                    return TDFShared.Helpers.JsonSerializationHelper.Deserialize<T>(cachedValue);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deserializing cached value for key: {Key}", key);
                    // If deserialization fails, continue to create a new value
                }
            }

            // Cache miss, create new value
            _logger.LogDebug("Cache miss for key: {Key}", key);
            var result = await factory();

            try
            {
                var serializedResult = TDFShared.Helpers.JsonSerializationHelper.Serialize(result);

                // Calculate the cache expiration based on the absolute expiration
                var expiry = TimeSpan.FromMinutes(absoluteExpirationMinutes);

                // Store the value in Redis
                await _db.StringSetAsync(key, serializedResult, expiry);

                // We don't handle sliding expiration directly as Redis doesn't have native support,
                // but we could implement it with additional key tracking if needed
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error caching value for key: {Key}", key);
                // Continue even if caching fails, just return the result
            }

            return result;
        }

        /// <summary>
        /// Sets a value in the cache with the specified expiration
        /// </summary>
        public async Task<bool> SetAsync<T>(
            string key,
            T value,
            int absoluteExpirationMinutes = 30,
            int slidingExpirationMinutes = 10)
        {
            try
            {
                var serializedValue = TDFShared.Helpers.JsonSerializationHelper.Serialize(value);
                var expiry = TimeSpan.FromMinutes(absoluteExpirationMinutes);

                bool result = await _db.StringSetAsync(key, serializedValue, expiry);
                _logger.LogDebug("Item with key {Key} set in Redis cache", key);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting cache item with key: {Key}", key);
                return false;
            }
        }

        /// <summary>
        /// Gets a value from cache if available
        /// </summary>
        public async Task<T?> GetAsync<T>(string key) where T : class
        {
            try
            {
                var cachedValue = await _db.StringGetAsync(key);
                if (cachedValue.HasValue)
                {
                    _logger.LogDebug("Cache hit for key: {Key}", key);
                    return JsonSerializer.Deserialize<T>(cachedValue);
                }

                _logger.LogDebug("Cache miss for key: {Key}", key);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cache item with key: {Key}", key);
                return null;
            }
        }

        /// <summary>
        /// Removes an item from the cache
        /// </summary>
        public void Remove(string key)
        {
            _logger.LogDebug("Removing item with key {Key} from cache", key);
            _db.KeyDelete(key);
        }

        /// <summary>
        /// Gets a value from cache if available
        /// </summary>
        public bool TryGetValue<T>(string key, out T value)
        {
            var cachedValue = _db.StringGet(key);
            if (cachedValue.HasValue)
            {
                try
                {
                    value = JsonSerializer.Deserialize<T>(cachedValue);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deserializing cached value for key: {Key}", key);
                }
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Removes an item from the cache
        /// </summary>
        /// <param name="key">Cache key</param>
        public void RemoveFromCache(string key)
        {
            if (string.IsNullOrEmpty(key))
                return;

            _db.KeyDelete(key);
            _logger.LogDebug("Removed item from Redis cache with key: {Key}", key);
        }
    }
}