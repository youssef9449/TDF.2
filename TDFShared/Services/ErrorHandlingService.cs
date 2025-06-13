using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TDFShared.Exceptions;

namespace TDFShared.Services
{
    /// <summary>
    /// Centralized error handling service for generating user-friendly error messages
    /// </summary>
    public class ErrorHandlingService : IErrorHandlingService
    {
        private readonly ILogger<ErrorHandlingService> _logger;
        private readonly Dictionary<Type, Func<Exception, string>> _errorHandlers;

        public ErrorHandlingService(ILogger<ErrorHandlingService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _errorHandlers = InitializeErrorHandlers();
        }

        private Dictionary<Type, Func<Exception, string>> InitializeErrorHandlers()
        {
            return new Dictionary<Type, Func<Exception, string>>
            {
                { typeof(ApiException), ex => ((ApiException)ex).Message },
                { typeof(ValidationException), ex => ((ValidationException)ex).Message },
                { typeof(UnauthorizedAccessException), _ => "You don't have permission to perform this action." },
                { typeof(HttpRequestException), HandleHttpRequestException },
                { typeof(TaskCanceledException), _ => "The operation timed out. Please try again." },
                { typeof(ArgumentException), ex => $"Invalid input: {ex.Message}" },
                { typeof(InvalidOperationException), _ => "This operation cannot be performed at this time." },
                { typeof(NotSupportedException), _ => "This operation is not supported." }
            };
        }

        private string HandleHttpRequestException(Exception ex)
        {
            var httpEx = (HttpRequestException)ex;
            var message = httpEx.Message.ToLowerInvariant();

            if (IsNetworkError(httpEx))
                return "Network connection failed. Please check your internet connection and try again.";
            if (message.Contains("401"))
                return "Authentication failed. Please log in again.";
            if (message.Contains("403"))
                return "You don't have permission to perform this action.";
            if (message.Contains("404"))
                return "The requested resource was not found.";
            if (message.Contains("500"))
                return "Server error occurred. Please try again later.";
            if (message.Contains("timeout"))
                return "The request timed out. Please try again.";
            if (message.Contains("connection"))
                return "Connection error occurred. Please check your network connection.";

            return "An unexpected error occurred while communicating with the server.";
        }

        public string GetFriendlyErrorMessage(Exception exception, string? context = null)
        {
            if (exception == null)
                return "An unknown error occurred.";

            var contextPrefix = !string.IsNullOrEmpty(context) ? $"Error {context}: " : "Error: ";

            // Try to find a specific handler for the exception type
            var exceptionType = exception.GetType();
            if (_errorHandlers.TryGetValue(exceptionType, out var handler))
            {
                return $"{contextPrefix}{handler(exception)}";
            }

            // Check for derived types
            foreach (var kvp in _errorHandlers)
            {
                if (kvp.Key.IsAssignableFrom(exceptionType))
                {
                    return $"{contextPrefix}{kvp.Value(exception)}";
                }
            }

            // Log unexpected exception types
            _logger.LogWarning("Unhandled exception type: {ExceptionType}", exceptionType);
            return $"{contextPrefix}An unexpected error occurred. Please try again.";
        }

        public async Task ShowErrorAsync(Exception exception, string? context = null, string title = "Error")
        {
            var message = GetFriendlyErrorMessage(exception, context);
            await ShowErrorAsync(message, title);
            
            // Log the error with full details
            _logger.LogError(exception, "Error occurred in {Context}: {Message}", context ?? "unknown context", exception.Message);
        }

        public async Task ShowErrorAsync(string message, string title = "Error")
        {
            // This is a shared library method - actual UI display should be implemented in platform-specific projects
            // For now, we log the error message that would be displayed
            _logger.LogError("Error display requested: {Title} - {Message}", title, message);

            // Return completed task to maintain async signature
            await Task.CompletedTask;
        }

        public string LogAndGetFriendlyMessage(Exception exception, string context, ILogger? logger = null)
        {
            var loggerToUse = logger ?? _logger;
            var friendlyMessage = GetFriendlyErrorMessage(exception, context);

            loggerToUse.LogError(exception, "Error in {Context}: {Message}", context ?? "unknown context", exception.Message);

            return friendlyMessage;
        }

        public bool IsNetworkError(Exception exception)
        {
            if (exception is HttpRequestException httpEx)
            {
                var message = httpEx.Message.ToLowerInvariant();
                return message.Contains("network") ||
                       message.Contains("connection") ||
                       message.Contains("timeout") ||
                       message.Contains("unreachable") ||
                       message.Contains("dns") ||
                       message.Contains("host");
            }

            if (exception is TaskCanceledException)
                return true;

            if (exception is WebException webEx)
            {
                return webEx.Status == WebExceptionStatus.ConnectFailure ||
                       webEx.Status == WebExceptionStatus.Timeout ||
                       webEx.Status == WebExceptionStatus.NameResolutionFailure ||
                       webEx.Status == WebExceptionStatus.ConnectionClosed ||
                       webEx.Status == WebExceptionStatus.ProxyNameResolutionFailure;
            }

            return false;
        }

        public bool IsAuthenticationError(Exception exception)
        {
            if (exception is UnauthorizedAccessException)
                return true;

            if (exception is HttpRequestException httpEx)
            {
                var message = httpEx.Message.ToLowerInvariant();
                return message.Contains("401") ||
                       message.Contains("unauthorized") ||
                       message.Contains("authentication") ||
                       message.Contains("token");
            }

            if (exception is ApiException apiEx)
                return apiEx.StatusCode == HttpStatusCode.Unauthorized;

            return false;
        }

        public bool IsValidationError(Exception exception)
        {
            if (exception is ValidationException)
                return true;

            if (exception is ArgumentException)
                return true;

            if (exception is HttpRequestException httpEx)
            {
                var message = httpEx.Message.ToLowerInvariant();
                return message.Contains("400") ||
                       message.Contains("validation") ||
                       message.Contains("invalid") ||
                       message.Contains("bad request");
            }

            if (exception is ApiException apiEx)
                return apiEx.StatusCode == HttpStatusCode.BadRequest;

            return false;
        }
    }
}
