using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using TDFAPI.Exceptions;

namespace TDFAPI.CQRS.Behaviors
{
    /// <summary>
    /// MediatR pipeline behaviour that annotates unhandled exceptions with
    /// the request name and the HTTP status code they will map to, then
    /// rethrows so <c>GlobalExceptionMiddleware</c> can produce the
    /// client-facing <c>ApiResponse</c>. This gives us structured log
    /// correlation between CQRS handler failures and the final HTTP
    /// response without swallowing the exception.
    /// </summary>
    public class ExceptionLoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        private readonly ILogger<ExceptionLoggingBehavior<TRequest, TResponse>> _logger;

        public ExceptionLoggingBehavior(ILogger<ExceptionLoggingBehavior<TRequest, TResponse>> logger)
        {
            _logger = logger;
        }

        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            try
            {
                return await next();
            }
            catch (Exception ex)
            {
                var (statusCode, _) = ExceptionToResponseMapper.Map(ex);
                var requestName = typeof(TRequest).Name;

                if (statusCode >= 500)
                {
                    _logger.LogError(
                        ex,
                        "CQRS request {RequestName} failed with unmapped exception {ExceptionType}: {Message} (will surface as HTTP {StatusCode})",
                        requestName,
                        ex.GetType().Name,
                        ex.Message,
                        statusCode);
                }
                else
                {
                    _logger.LogWarning(
                        "CQRS request {RequestName} threw {ExceptionType} which maps to HTTP {StatusCode}: {Message}",
                        requestName,
                        ex.GetType().Name,
                        statusCode,
                        ex.Message);
                }

                throw;
            }
        }
    }
}
