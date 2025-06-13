using System;
using System.Collections.Generic;
using System.Net.Http;

namespace TDFShared.Configuration
{
    /// <summary>
    /// Configuration settings for HTTP client services
    /// </summary>
    public class HttpClientConfiguration
    {
        /// <summary>
        /// Base URL for API requests
        /// </summary>
        public string BaseUrl { get; set; } = string.Empty;

        /// <summary>
        /// Request timeout in seconds
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Whether to use exponential backoff for retries
        /// </summary>
        public bool UseExponentialBackoff { get; set; } = true;

        /// <summary>
        /// Maximum number of concurrent requests
        /// </summary>
        public int MaxConcurrentRequests { get; set; } = 10;

        /// <summary>
        /// Default headers to include with all requests
        /// </summary>
        public Dictionary<string, string> DefaultHeaders { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// User agent string for requests
        /// </summary>
        public string UserAgent { get; set; } = "TDF-HttpClient/1.0";

        /// <summary>
        /// Whether to automatically decompress responses
        /// </summary>
        public bool AutomaticDecompression { get; set; } = true;

        /// <summary>
        /// Whether to follow redirects automatically
        /// </summary>
        public bool FollowRedirects { get; set; } = true;

        /// <summary>
        /// Maximum number of redirects to follow
        /// </summary>
        public int MaxRedirects { get; set; } = 10;

        /// <summary>
        /// Whether to validate SSL certificates
        /// </summary>
        public bool ValidateSslCertificates { get; set; } = true;

        /// <summary>
        /// Connection pool settings
        /// </summary>
        public ConnectionPoolSettings ConnectionPool { get; set; } = new ConnectionPoolSettings();

        /// <summary>
        /// Retry policy settings
        /// </summary>
        public RetryPolicySettings RetryPolicy { get; set; } = new RetryPolicySettings();

        /// <summary>
        /// Circuit breaker settings
        /// </summary>
        public CircuitBreakerSettings CircuitBreaker { get; set; } = new CircuitBreakerSettings();

        /// <summary>
        /// Logging settings
        /// </summary>
        public HttpLoggingSettings Logging { get; set; } = new HttpLoggingSettings();

        /// <summary>
        /// Validates the configuration and throws if invalid
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrEmpty(BaseUrl))
                throw new InvalidOperationException("BaseUrl is required");

            if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out _))
                throw new InvalidOperationException("BaseUrl must be a valid absolute URI");

            if (TimeoutSeconds <= 0)
                throw new InvalidOperationException("TimeoutSeconds must be greater than 0");

            if (MaxConcurrentRequests <= 0)
                throw new InvalidOperationException("MaxConcurrentRequests must be greater than 0");

            if (MaxRedirects < 0)
                throw new InvalidOperationException("MaxRedirects must be 0 or greater");

            ConnectionPool.Validate();
            RetryPolicy.Validate();
            CircuitBreaker.Validate();
        }

        /// <summary>
        /// Creates a default configuration
        /// </summary>
        public static HttpClientConfiguration Default => new HttpClientConfiguration();

        /// <summary>
        /// Creates a configuration for development environments
        /// </summary>
        public static HttpClientConfiguration Development => new HttpClientConfiguration
        {
            ValidateSslCertificates = false,
            Logging = new HttpLoggingSettings
            {
                LogRequests = true,
                LogResponses = true,
                LogHeaders = true,
                LogContent = true
            }
        };

        /// <summary>
        /// Creates a configuration for production environments
        /// </summary>
        public static HttpClientConfiguration Production => new HttpClientConfiguration
        {
            ValidateSslCertificates = true,
            Logging = new HttpLoggingSettings
            {
                LogRequests = true,
                LogResponses = false,
                LogHeaders = false,
                LogContent = false
            }
        };
    }

    /// <summary>
    /// Connection pool settings
    /// </summary>
    public class ConnectionPoolSettings
    {
        /// <summary>
        /// Maximum number of connections per endpoint
        /// </summary>
        public int MaxConnectionsPerEndpoint { get; set; } = 10;

        /// <summary>
        /// Connection idle timeout in seconds
        /// </summary>
        public int IdleTimeoutSeconds { get; set; } = 90;

        /// <summary>
        /// Connection lifetime in seconds
        /// </summary>
        public int LifetimeSeconds { get; set; } = 600;

        /// <summary>
        /// Validates the connection pool settings and throws if invalid.
        /// </summary>
        public void Validate()
        {
            if (MaxConnectionsPerEndpoint <= 0)
                throw new InvalidOperationException("MaxConnectionsPerEndpoint must be greater than 0");

            if (IdleTimeoutSeconds <= 0)
                throw new InvalidOperationException("IdleTimeoutSeconds must be greater than 0");

            if (LifetimeSeconds <= 0)
                throw new InvalidOperationException("LifetimeSeconds must be greater than 0");
        }
    }

    /// <summary>
    /// Retry policy settings
    /// </summary>
    public class RetryPolicySettings
    {
        /// <summary>
        /// Maximum number of retry attempts
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Initial delay between retries in milliseconds
        /// </summary>
        public int InitialRetryDelayMs { get; set; } = 1000;

        /// <summary>
        /// Maximum delay between retries in milliseconds
        /// </summary>
        public int MaxRetryDelayMs { get; set; } = 30000;

        /// <summary>
        /// HTTP status codes that should trigger a retry
        /// </summary>
        public int[] RetryableStatusCodes { get; set; } = { 408, 429, 500, 502, 503, 504 };

        /// <summary>
        /// Exception types that should trigger a retry
        /// </summary>
        public string[] RetryableExceptions { get; set; } = { "HttpRequestException", "TaskCanceledException", "TimeoutException" };

        /// <summary>
        /// Jitter factor for retry delays (0.0 to 1.0)
        /// </summary>
        public double JitterFactor { get; set; } = 0.1;

        /// <summary>
        /// Validates the retry policy settings
        /// </summary>
        public void Validate()
        {
            if (MaxRetries < 0)
                throw new ArgumentException("MaxRetries must be non-negative", nameof(MaxRetries));

            if (InitialRetryDelayMs < 0)
                throw new ArgumentException("InitialRetryDelayMs must be non-negative", nameof(InitialRetryDelayMs));

            if (MaxRetryDelayMs < InitialRetryDelayMs)
                throw new ArgumentException("MaxRetryDelayMs must be greater than or equal to InitialRetryDelayMs", nameof(MaxRetryDelayMs));

            if (JitterFactor < 0.0 || JitterFactor > 1.0)
                throw new InvalidOperationException("JitterFactor must be between 0.0 and 1.0");
        }
    }

    /// <summary>
    /// Circuit breaker settings
    /// </summary>
    public class CircuitBreakerSettings
    {
        /// <summary>
        /// Whether circuit breaker is enabled
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Number of consecutive failures before opening circuit
        /// </summary>
        public int FailureThreshold { get; set; } = 5;

        /// <summary>
        /// Duration to keep circuit open in seconds
        /// </summary>
        public int OpenDurationSeconds { get; set; } = 30;

        /// <summary>
        /// Number of successful calls needed to close circuit
        /// </summary>
        public int SuccessThreshold { get; set; } = 3;

        /// <summary>
        /// Validates the circuit breaker settings
        /// </summary>
        public void Validate()
        {
            if (FailureThreshold < 1)
                throw new ArgumentException("FailureThreshold must be at least 1", nameof(FailureThreshold));

            if (OpenDurationSeconds <= 0)
                throw new InvalidOperationException("OpenDurationSeconds must be greater than 0");

            if (SuccessThreshold <= 0)
                throw new InvalidOperationException("SuccessThreshold must be greater than 0");
        }
    }

    /// <summary>
    /// HTTP logging settings
    /// </summary>
    public class HttpLoggingSettings
    {
        /// <summary>
        /// Whether to log HTTP requests
        /// </summary>
        public bool LogRequests { get; set; } = true;

        /// <summary>
        /// Whether to log HTTP responses
        /// </summary>
        public bool LogResponses { get; set; } = false;

        /// <summary>
        /// Whether to log HTTP headers
        /// </summary>
        public bool LogHeaders { get; set; } = false;

        /// <summary>
        /// Whether to log HTTP content
        /// </summary>
        public bool LogContent { get; set; } = false;

        /// <summary>
        /// Maximum content length to log
        /// </summary>
        public int MaxContentLength { get; set; } = 1024;

        /// <summary>
        /// Headers to exclude from logging (for security)
        /// </summary>
        public string[] ExcludedHeaders { get; set; } = { "Authorization", "Cookie", "Set-Cookie" };
    }
}
