using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Polly;
using TDFShared.Models;

namespace TDFShared.Http
{
    /// <summary>
    /// Delegating handler that retries transient HTTP failures using an
    /// exponential-backoff Polly policy. Replaces the per-call retry loop that
    /// previously lived inside <see cref="TDFShared.Services.HttpClientService"/>
    /// so the same policy is applied uniformly to every verb (including
    /// <see cref="HttpClient.SendAsync(HttpRequestMessage)"/> direct callers).
    /// </summary>
    public sealed class PollyRetryingHandler : DelegatingHandler
    {
        private const int MaxRetries = 3;
        private static readonly TimeSpan InitialBackoff = TimeSpan.FromSeconds(1);

        private readonly ILogger<PollyRetryingHandler> _logger;
        private readonly IHttpTelemetry? _telemetry;
        private readonly IAsyncPolicy<HttpResponseMessage> _policy;

        public PollyRetryingHandler(
            ILogger<PollyRetryingHandler> logger,
            IHttpTelemetry? telemetry = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _telemetry = telemetry;

            _policy = Policy<HttpResponseMessage>
                .HandleResult(r => !r.IsSuccessStatusCode && IsRetryableStatusCode(r.StatusCode))
                .Or<HttpRequestException>()
                .Or<TaskCanceledException>()
                .Or<IOException>()
                .WaitAndRetryAsync(
                    retryCount: MaxRetries,
                    sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)) + InitialBackoff,
                    onRetry: (outcome, delay, retryCount, ctx) =>
                    {
                        var endpoint = ctx.TryGetValue("endpoint", out var v) ? v?.ToString() : "unknown";
                        _logger.LogWarning(
                            "Retry attempt {RetryCount}/{MaxRetries} for {Endpoint} after {DelayMs}ms",
                            retryCount, MaxRetries, endpoint, delay.TotalMilliseconds);

                        _telemetry?.Record(success: false, elapsed: TimeSpan.Zero, isRetry: true);
                    });
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var context = new Context
            {
                ["endpoint"] = request.RequestUri?.ToString() ?? "unknown"
            };

            return _policy.ExecuteAsync(
                (_, ct) => base.SendAsync(request, ct),
                context,
                cancellationToken);
        }

        private static bool IsRetryableStatusCode(HttpStatusCode statusCode) =>
            statusCode is HttpStatusCode.RequestTimeout
                or HttpStatusCode.TooManyRequests
                or HttpStatusCode.InternalServerError
                or HttpStatusCode.BadGateway
                or HttpStatusCode.ServiceUnavailable
                or HttpStatusCode.GatewayTimeout;
    }
}
