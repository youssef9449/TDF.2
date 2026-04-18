using System;
using TDFShared.Models;

namespace TDFShared.Http
{
    /// <summary>
    /// Default <see cref="IHttpTelemetry"/> implementation backed by a single
    /// <see cref="HttpClientStatistics"/> instance guarded by a lock. Intended
    /// to be registered as a singleton so metrics aggregate across every
    /// <see cref="System.Net.Http.HttpClient"/> the factory hands out.
    /// </summary>
    public sealed class HttpTelemetry : IHttpTelemetry
    {
        private readonly HttpClientStatistics _statistics = new();
        private readonly object _lock = new();

        public void Record(bool success, TimeSpan elapsed, bool isRetry)
        {
            var ms = (long)elapsed.TotalMilliseconds;
            lock (_lock)
            {
                _statistics.TotalRequests++;
                _statistics.TotalResponseTime += ms;

                if (success)
                {
                    _statistics.SuccessfulRequests++;
                }
                else
                {
                    _statistics.FailedRequests++;
                }

                if (isRetry)
                {
                    _statistics.RetriedRequests++;
                }

                if (ms > _statistics.MaxResponseTime)
                {
                    _statistics.MaxResponseTime = ms;
                }

                if (ms < _statistics.MinResponseTime || _statistics.MinResponseTime == long.MaxValue)
                {
                    _statistics.MinResponseTime = ms;
                }
            }
        }

        public HttpClientStatistics GetSnapshot()
        {
            lock (_lock)
            {
                return _statistics.Clone();
            }
        }
    }
}
