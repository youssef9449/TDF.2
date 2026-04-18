using System;
using TDFShared.Models;

namespace TDFShared.Http
{
    /// <summary>
    /// Collects per-request metrics for the shared HTTP pipeline (total count,
    /// success/failure ratios, min/max/avg response times, retries). Populated
    /// by <see cref="HttpTelemetryHandler"/> and surfaced to callers via
    /// <see cref="IHttpClientService.GetStatistics"/> equivalents.
    /// </summary>
    public interface IHttpTelemetry
    {
        /// <summary>Records a single request outcome.</summary>
        /// <param name="success">True when the response indicated success.</param>
        /// <param name="elapsed">Total wall-clock elapsed time of the SendAsync call.</param>
        /// <param name="isRetry">True when the request was a retry attempt.</param>
        void Record(bool success, TimeSpan elapsed, bool isRetry);

        /// <summary>Returns a snapshot copy of the current statistics.</summary>
        HttpClientStatistics GetSnapshot();
    }
}
