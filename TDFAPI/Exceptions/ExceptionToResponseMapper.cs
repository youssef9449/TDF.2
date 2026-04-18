using System;
using System.Net;
using Microsoft.AspNetCore.Http;
using TDFShared.Exceptions;

namespace TDFAPI.Exceptions
{
    /// <summary>
    /// Central mapping from thrown <see cref="Exception"/> instances to the
    /// HTTP status code and user-facing message the API should return.
    /// This is the single source of truth used by both the global exception
    /// middleware and any CQRS behaviour that needs to translate exceptions.
    /// </summary>
    public static class ExceptionToResponseMapper
    {
        /// <summary>
        /// Mapped HTTP status + user-friendly message for the supplied
        /// <paramref name="exception"/>. Unknown exceptions fall through to
        /// <see cref="StatusCodes.Status500InternalServerError"/>.
        /// </summary>
        public static (int StatusCode, string Message) Map(Exception exception)
        {
            return exception switch
            {
                ApiException apiEx => ((int)apiEx.StatusCode, apiEx.Message),

                ValidationException => (
                    StatusCodes.Status400BadRequest,
                    exception.Message),

                BusinessRuleException => (
                    StatusCodes.Status422UnprocessableEntity,
                    exception.Message),

                EntityNotFoundException => (
                    StatusCodes.Status404NotFound,
                    exception.Message),

                UnauthorizedAccessException domainUnauthorized => (
                    StatusCodes.Status403Forbidden,
                    domainUnauthorized.Message),

                ConcurrencyException => (
                    StatusCodes.Status409Conflict,
                    exception.Message),

                DomainException domainEx => (
                    StatusCodes.Status400BadRequest,
                    domainEx.Message),

                // CQRS handlers throw System.UnauthorizedAccessException after the
                // caller is already authenticated (i.e. the JWT bearer pipeline
                // has already accepted the request). Treating those as 403
                // Forbidden rather than 401 Unauthorized matches the observable
                // behaviour every controller used to hand-roll via
                // try/catch + StatusCode(403, ...).
                System.UnauthorizedAccessException => (
                    StatusCodes.Status403Forbidden,
                    exception.Message),

                ArgumentException => (
                    StatusCodes.Status400BadRequest,
                    $"Invalid input provided: {exception.Message}"),

                InvalidOperationException => (
                    StatusCodes.Status400BadRequest,
                    $"The requested operation is invalid: {exception.Message}"),

                KeyNotFoundException => (
                    StatusCodes.Status404NotFound,
                    "The requested resource was not found"),

                System.IO.FileNotFoundException => (
                    StatusCodes.Status404NotFound,
                    "The requested file was not found"),

                NotImplementedException => (
                    StatusCodes.Status501NotImplemented,
                    "This feature is not implemented yet"),

                _ => (
                    StatusCodes.Status500InternalServerError,
                    "An unexpected error occurred. Please try again later.")
            };
        }

        /// <summary>Convenience overload returning the status code as <see cref="HttpStatusCode"/>.</summary>
        public static HttpStatusCode MapStatusCode(Exception exception)
        {
            var (status, _) = Map(exception);
            return (HttpStatusCode)status;
        }
    }
}
