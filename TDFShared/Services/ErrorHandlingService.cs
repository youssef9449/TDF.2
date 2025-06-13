using System;
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

        public ErrorHandlingService(ILogger<ErrorHandlingService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string GetFriendlyErrorMessage(Exception exception, string? context = null)
        {
            if (exception == null)
                return "An unknown error occurred.";

            var contextPrefix = !string.IsNullOrEmpty(context) ? $"Error {context}: " : "Error: ";

            return exception switch
            {
                ApiException apiEx => $"{contextPrefix}{apiEx.Message}",
                ValidationException validationEx => $"{contextPrefix}{validationEx.Message}",
                UnauthorizedAccessException => $"{contextPrefix}You don't have permission to perform this action.",
                HttpRequestException httpEx when IsNetworkError(httpEx) =>
                    $"{contextPrefix}Network connection failed. Please check your internet connection and try again.",
                HttpRequestException httpEx when httpEx.Message.Contains("401") =>
                    $"{contextPrefix}Authentication failed. Please log in again.",
                HttpRequestException httpEx when httpEx.Message.Contains("403") =>
                    $"{contextPrefix}You don't have permission to perform this action.",
                HttpRequestException httpEx when httpEx.Message.Contains("404") =>
                    $"{contextPrefix}The requested resource was not found.",
                HttpRequestException httpEx when httpEx.Message.Contains("500") =>
                    $"{contextPrefix}Server error occurred. Please try again later.",
                TaskCanceledException =>
                    $"{contextPrefix}The operation timed out. Please try again.",
                ArgumentException argEx =>
                    $"{contextPrefix}Invalid input: {argEx.Message}",
                InvalidOperationException =>
                    $"{contextPrefix}This operation cannot be performed at this time.",
                NotSupportedException =>
                    $"{contextPrefix}This operation is not supported.",
                _ => $"{contextPrefix}An unexpected error occurred. Please try again."
            };
        }

        public async Task ShowErrorAsync(Exception exception, string? context = null, string title = "Error")
        {
            var message = GetFriendlyErrorMessage(exception, context);
            await ShowErrorAsync(message, title);
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
            return exception switch
            {
                HttpRequestException httpEx =>
                    httpEx.Message.ToLowerInvariant().Contains("network") ||
                    httpEx.Message.ToLowerInvariant().Contains("connection") ||
                    httpEx.Message.ToLowerInvariant().Contains("timeout") ||
                    httpEx.Message.ToLowerInvariant().Contains("unreachable"),
                TaskCanceledException => true,
                WebException webEx =>
                    webEx.Status == WebExceptionStatus.ConnectFailure ||
                    webEx.Status == WebExceptionStatus.Timeout ||
                    webEx.Status == WebExceptionStatus.NameResolutionFailure,
                _ => false
            };
        }

        public bool IsAuthenticationError(Exception exception)
        {
            return exception switch
            {
                UnauthorizedAccessException => true,
                HttpRequestException httpEx =>
                    httpEx.Message.Contains("401") ||
                    httpEx.Message.ToLowerInvariant().Contains("unauthorized"),
                ApiException apiEx =>
                    apiEx.StatusCode == HttpStatusCode.Unauthorized,
                _ => false
            };
        }

        public bool IsValidationError(Exception exception)
        {
            return exception switch
            {
                ValidationException => true,
                ArgumentNullException => true,
                ArgumentOutOfRangeException => true,
                ArgumentException => true, // Must come after more specific ArgumentException types
                HttpRequestException httpEx =>
                    httpEx.Message.Contains("400") ||
                    httpEx.Message.ToLowerInvariant().Contains("validation"),
                ApiException apiEx =>
                    apiEx.StatusCode == HttpStatusCode.BadRequest,
                _ => false
            };
        }
    }
}
