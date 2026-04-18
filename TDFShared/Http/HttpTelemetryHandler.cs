using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace TDFShared.Http
{
    /// <summary>
    /// Delegating handler that measures the elapsed time of each
    /// <see cref="HttpClient.SendAsync(HttpRequestMessage)"/> and forwards the
    /// outcome to <see cref="IHttpTelemetry"/>. When installed after
    /// <see cref="PollyRetryingHandler"/> in the pipeline it observes a single
    /// wall-clock elapsed value per logical request regardless of how many
    /// retry attempts the retry handler performed internally.
    /// </summary>
    public sealed class HttpTelemetryHandler : DelegatingHandler
    {
        private readonly IHttpTelemetry _telemetry;
        private readonly ILogger<HttpTelemetryHandler> _logger;

        public HttpTelemetryHandler(IHttpTelemetry telemetry, ILogger<HttpTelemetryHandler> logger)
        {
            _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
                stopwatch.Stop();
                _telemetry.Record(response.IsSuccessStatusCode, stopwatch.Elapsed, isRetry: false);
                return response;
            }
            catch
            {
                stopwatch.Stop();
                _telemetry.Record(success: false, stopwatch.Elapsed, isRetry: false);
                throw;
            }
        }
    }
}
