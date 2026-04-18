using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;
using TDFAPI.Exceptions;
using TDFAPI.Extensions;

namespace TDFAPI.Middleware
{
    // Extended ProblemDetails class with additional properties
    public class ExtendedProblemDetails : ProblemDetails
    {
        [JsonPropertyName("errorId")]
        public string ErrorId { get; set; } = string.Empty;

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }

        [JsonPropertyName("debug")]
        public string? Debug { get; set; }
    }

    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;
        private readonly IWebHostEnvironment _env;

        public GlobalExceptionMiddleware(
            RequestDelegate next,
            ILogger<GlobalExceptionMiddleware> logger,
            IWebHostEnvironment env)
        {
            _next = next;
            _logger = logger;
            _env = env;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                // Log detailed crash information
                LogDetailedCrashInformation(context, ex);

                // Also log to file to ensure we capture it
                LogToFile(context, ex);

                await HandleExceptionAsync(context, ex);
            }
        }

        private void LogDetailedCrashInformation(HttpContext context, Exception ex)
        {
            // Basic exception logging - keep it simple
            // Sanitize the exception message to prevent format string conflicts
            var sanitizedMessage = SanitizeLogMessage(ex.Message);
            _logger.LogError(ex,
                "API Error: {Path} {Method} - {ExceptionType}: {Message}",
                context.Request.Path,
                context.Request.Method,
                ex.GetType().Name,
                sanitizedMessage);
        }

        private void LogToFile(HttpContext context, Exception ex)
        {
            try
            {
                // Ensure logs directory exists in the application directory
                var logsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                if (!Directory.Exists(logsPath))
                {
                    Directory.CreateDirectory(logsPath);
                }

                // Create crash log file with timestamp
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var crashLogPath = Path.Combine(logsPath, $"error_{timestamp}.txt");

                // Keep it simple with just the essential crash details
                var crashDetails = new System.Text.StringBuilder();
                crashDetails.AppendLine($"API Error: {DateTime.Now}");
                crashDetails.AppendLine($"Request: {context.Request.Method} {context.Request.Path}{context.Request.QueryString}");
                crashDetails.AppendLine($"Remote IP: {context.GetRealIpAddress()}");
                crashDetails.AppendLine();

                // Exception details
                crashDetails.AppendLine($"Exception: {ex.GetType().FullName}");
                crashDetails.AppendLine($"Message: {ex.Message}");
                crashDetails.AppendLine();
                crashDetails.AppendLine("Stack Trace:");
                crashDetails.AppendLine(ex.StackTrace);

                // Inner exception if present
                if (ex.InnerException != null)
                {
                    crashDetails.AppendLine();
                    crashDetails.AppendLine($"Inner Exception: {ex.InnerException.GetType().FullName}");
                    crashDetails.AppendLine($"Inner Message: {ex.InnerException.Message}");
                }

                // Write to file
                File.WriteAllText(crashLogPath, crashDetails.ToString());
            }
            catch (Exception logEx)
            {
                // If we can't log to file, at least try to log the failure reason
                // Sanitize the exception message to prevent format string conflicts
                var sanitizedMessage = SanitizeLogMessage(logEx.Message);
                _logger.LogError(logEx, "Failed to write error details to log file: {Message}", sanitizedMessage);
            }
        }

        // Standardized ApiResponse for exceptions
        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var errorId = Guid.NewGuid().ToString();
            var (statusCode, userMessage) = ExceptionToResponseMapper.Map(exception);

            // Log the error with the error ID for correlation
            var sanitizedMessage = SanitizeLogMessage(exception.Message);
            _logger.LogError(exception, "Error ID: {ErrorId} - {ExceptionType}: {Message}",
                errorId, exception.GetType().Name, sanitizedMessage);

            // Create standardized ApiResponse
            var apiResponse = TDFShared.DTOs.Common.ApiResponse<object>.ErrorResponse(
                userMessage,
                (HttpStatusCode)statusCode);

            // In development, we can add more details to the message
            if (_env.IsDevelopment())
            {
                apiResponse.Message += $" (Debug ID: {errorId})";
            }

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = statusCode;

            await context.Response.WriteAsync(TDFShared.Helpers.JsonSerializationHelper.SerializePretty(apiResponse));
        }

        /// <summary>
        /// Sanitizes log messages to prevent format string conflicts with structured logging
        /// </summary>
        private static string SanitizeLogMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
                return message ?? string.Empty;

            // Replace curly braces that could be interpreted as format placeholders
            return message.Replace("{", "{{").Replace("}", "}}");
        }
    }
}