using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace TDFAPI.CQRS.Behaviors
{
    /// <summary>
    /// MediatR pipeline behavior that adds logging and correlation IDs to requests
    /// </summary>
    /// <typeparam name="TRequest">The request type</typeparam>
    /// <typeparam name="TResponse">The response type</typeparam>
    public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

        public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
        {
            _logger = logger;
        }

        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            // Generate correlation ID if not present
            var correlationId = Activity.Current?.Id ?? Guid.NewGuid().ToString();
            
            using var scope = _logger.BeginScope(
                new Dictionary<string, object> 
                {
                    ["CorrelationId"] = correlationId, 
                    ["RequestType"] = typeof(TRequest).Name 
                });

            var requestName = typeof(TRequest).Name;
            var requestGuid = Guid.NewGuid().ToString();

            _logger.LogInformation(
                "Begin Request: {RequestName} {@Request} [CorrelationId: {CorrelationId}]",
                requestName,
                request,
                correlationId);

            var stopwatch = Stopwatch.StartNew();
            try
            {
                var response = await next();
                stopwatch.Stop();

                _logger.LogInformation(
                    "End Request: {RequestName} completed in {ElapsedMilliseconds}ms [CorrelationId: {CorrelationId}]",
                    requestName,
                    stopwatch.ElapsedMilliseconds,
                    correlationId);

                return response;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(
                    ex,
                    "Request: {RequestName} failed in {ElapsedMilliseconds}ms [CorrelationId: {CorrelationId}]",
                    requestName,
                    stopwatch.ElapsedMilliseconds,
                    correlationId);
                throw;
            }
        }
    }
} 