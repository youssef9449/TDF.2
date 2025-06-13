using System;

namespace TDFShared.Models
{
    /// <summary>
    /// Statistics for HTTP client operations
    /// </summary>
    public class HttpClientStatistics
    {
        /// <summary>
        /// Total number of requests made
        /// </summary>
        public long TotalRequests { get; set; }

        /// <summary>
        /// Number of successful requests
        /// </summary>
        public long SuccessfulRequests { get; set; }

        /// <summary>
        /// Number of failed requests
        /// </summary>
        public long FailedRequests { get; set; }

        /// <summary>
        /// Number of retried requests
        /// </summary>
        public long RetriedRequests { get; set; }

        /// <summary>
        /// Total response time in milliseconds
        /// </summary>
        public long TotalResponseTime { get; set; }

        /// <summary>
        /// Minimum response time in milliseconds
        /// </summary>
        public long MinResponseTime { get; set; } = long.MaxValue;

        /// <summary>
        /// Maximum response time in milliseconds
        /// </summary>
        public long MaxResponseTime { get; set; }

        /// <summary>
        /// Average response time in milliseconds
        /// </summary>
        public double AverageResponseTime => TotalRequests == 0 ? 0 : (double)TotalResponseTime / TotalRequests;

        /// <summary>
        /// Creates a deep copy of the statistics
        /// </summary>
        /// <returns>A new instance with the same values</returns>
        public HttpClientStatistics Clone()
        {
            return new HttpClientStatistics
            {
                TotalRequests = TotalRequests,
                SuccessfulRequests = SuccessfulRequests,
                FailedRequests = FailedRequests,
                RetriedRequests = RetriedRequests,
                TotalResponseTime = TotalResponseTime,
                MinResponseTime = MinResponseTime,
                MaxResponseTime = MaxResponseTime
            };
        }
    }
} 