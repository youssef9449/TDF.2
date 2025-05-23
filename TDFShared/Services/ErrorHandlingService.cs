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

        public string GetFriendlyErrorMessage(Exception exception, string context = null)
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

        public async Task ShowErrorAsync(Exception exception, string context = null, string title = "Error")
        {
            var message = GetFriendlyErrorMessage(exception, context);
            await ShowErrorAsync(message, title);
        }

        public async Task ShowErrorAsync(string message, string title = "Error")
        {
            // In MAUI applications, use Shell.Current.DisplayAlert
            // In API applications, this would be handled differently
            try
            {
                if (Microsoft.Maui.Controls.Shell.Current != null)
                {
                    await Microsoft.Maui.Controls.Shell.Current.DisplayAlert(title, message, "OK");
                }
                else
                {
                    // Fallback for non-MAUI contexts
                    _logger.LogError("Error display requested but no UI context available: {Title} - {Message}", title, message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to display error message: {Title} - {Message}", title, message);
            }
        }

        public string LogAndGetFriendlyMessage(Exception exception, string context, ILogger logger = null)
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
                    httpEx.Message.Contains("network", StringComparison.OrdinalIgnoreCase) ||
                    httpEx.Message.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
                    httpEx.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                    httpEx.Message.Contains("unreachable", StringComparison.OrdinalIgnoreCase),
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
                    httpEx.Message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase),
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
                ArgumentException => true,
                ArgumentNullException => true,
                ArgumentOutOfRangeException => true,
                HttpRequestException httpEx => 
                    httpEx.Message.Contains("400") || 
                    httpEx.Message.Contains("validation", StringComparison.OrdinalIgnoreCase),
                ApiException apiEx => 
                    apiEx.StatusCode == HttpStatusCode.BadRequest,
                _ => false
            };
        }
    }
}
