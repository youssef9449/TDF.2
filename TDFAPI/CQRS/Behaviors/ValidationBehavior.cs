using MediatR;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TDFShared.Validation;

namespace TDFAPI.CQRS.Behaviors
{
    /// <summary>
    /// Pipeline behavior for validating requests using shared validation service
    /// </summary>
    /// <typeparam name="TRequest">Request type</typeparam>
    /// <typeparam name="TResponse">Response type</typeparam>
    public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        private readonly IValidationService _validationService;
        private readonly ILogger<ValidationBehavior<TRequest, TResponse>> _logger;

        public ValidationBehavior(
            IValidationService validationService,
            ILogger<ValidationBehavior<TRequest, TResponse>> logger)
        {
            _validationService = validationService;
            _logger = logger;
        }

        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            var typeName = request.GetType().Name;

            _logger.LogDebug("Validating request {RequestType}", typeName);

            // Use shared validation service for data annotation validation
            var validationResult = _validationService.ValidateObject(request);

            if (validationResult.IsValid)
            {
                _logger.LogDebug("Validation successful for request {RequestType}", typeName);
                return await next();
            }

            _logger.LogWarning(
                "Validation failed for {RequestType} with {ErrorCount} error(s): {Errors}",
                typeName,
                validationResult.Errors.Count,
                string.Join(", ", validationResult.Errors));

            throw new TDFShared.Exceptions.ValidationException(string.Join("; ", validationResult.Errors));
        }
    }
}