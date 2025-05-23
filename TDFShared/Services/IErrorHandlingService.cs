using System;
using System.Threading.Tasks;

namespace TDFShared.Services
{
    /// <summary>
    /// Interface for centralized error handling and user-friendly error message generation
    /// </summary>
    public interface IErrorHandlingService
    {
        /// <summary>
        /// Gets a user-friendly error message from an exception
        /// </summary>
        /// <param name="exception">The exception to process</param>
        /// <param name="context">Optional context for the error (e.g., "loading data", "saving request")</param>
        /// <returns>User-friendly error message</returns>
        string GetFriendlyErrorMessage(Exception exception, string context = null);

        /// <summary>
        /// Shows an error message to the user using the appropriate UI mechanism
        /// </summary>
        /// <param name="exception">The exception to display</param>
        /// <param name="context">Optional context for the error</param>
        /// <param name="title">Optional custom title for the error dialog</param>
        Task ShowErrorAsync(Exception exception, string context = null, string title = "Error");

        /// <summary>
        /// Shows a custom error message to the user
        /// </summary>
        /// <param name="message">The error message to display</param>
        /// <param name="title">Optional title for the error dialog</param>
        Task ShowErrorAsync(string message, string title = "Error");

        /// <summary>
        /// Logs an error with appropriate context and returns a user-friendly message
        /// </summary>
        /// <param name="exception">The exception to log</param>
        /// <param name="context">Context for the error</param>
        /// <param name="logger">Optional logger instance</param>
        /// <returns>User-friendly error message</returns>
        string LogAndGetFriendlyMessage(Exception exception, string context, Microsoft.Extensions.Logging.ILogger logger = null);

        /// <summary>
        /// Determines if an exception is a network-related error
        /// </summary>
        /// <param name="exception">The exception to check</param>
        /// <returns>True if the exception is network-related</returns>
        bool IsNetworkError(Exception exception);

        /// <summary>
        /// Determines if an exception is an authentication-related error
        /// </summary>
        /// <param name="exception">The exception to check</param>
        /// <returns>True if the exception is authentication-related</returns>
        bool IsAuthenticationError(Exception exception);

        /// <summary>
        /// Determines if an exception is a validation-related error
        /// </summary>
        /// <param name="exception">The exception to check</param>
        /// <returns>True if the exception is validation-related</returns>
        bool IsValidationError(Exception exception);
    }
}
