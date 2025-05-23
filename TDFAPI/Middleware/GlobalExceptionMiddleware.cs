using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;
using TDFAPI.Extensions;
//using TDFAPI.Exceptions;

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
            _logger.LogError(ex,
                "API Error: {Path} {Method} - {ExceptionType}: {Message}",
                context.Request.Path,
                context.Request.Method,
                ex.GetType().Name,
                ex.Message);
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
                _logger.LogError(logEx, "Failed to write error details to log file: {Message}", logEx.Message);
            }
        }

        // Updated to use Problem Details (RFC 7807)
        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var errorId = Guid.NewGuid().ToString();
            var statusCode = GetStatusCode(exception);

            // Log the error with the error ID for correlation
            _logger.LogError(exception, "Error ID: {ErrorId} - {ExceptionType}: {Message}",
                errorId, exception.GetType().Name, exception.Message);

            // Create RFC 7807 Problem Details response with extended properties
            var problemDetails = new ExtendedProblemDetails
            {
                Status = statusCode,
                Title = GetProblemTitle(exception),
                Detail = GetUserFriendlyMessage(exception),
                Type = GetProblemType(exception),
                Instance = context.Request.Path,
                ErrorId = errorId,
                Timestamp = DateTime.UtcNow
            };

            // Include debug information in development environment
            if (_env.IsDevelopment())
            {
                problemDetails.Debug = exception.ToString();
            }

            context.Response.ContentType = "application/problem+json";
            context.Response.StatusCode = statusCode;

            await context.Response.WriteAsync(TDFShared.Helpers.JsonSerializationHelper.SerializePretty(problemDetails));
        }

        private int GetStatusCode(Exception exception)
        {
            // Map exception types to appropriate status codes
            return exception switch
            {
                UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
                ArgumentException => StatusCodes.Status400BadRequest,
                InvalidOperationException => StatusCodes.Status400BadRequest,
                KeyNotFoundException => StatusCodes.Status404NotFound,
                FileNotFoundException => StatusCodes.Status404NotFound,
                NotImplementedException => StatusCodes.Status501NotImplemented,
                // Add more mappings as needed
                _ => StatusCodes.Status500InternalServerError
            };
        }

        private string GetUserFriendlyMessage(Exception exception)
        {
            // Provide user-friendly messages based on exception type
            return exception switch
            {
                UnauthorizedAccessException => "You are not authorized to perform this action",
                ArgumentException => $"Invalid input provided: {exception.Message}",
                InvalidOperationException => $"The requested operation is invalid: {exception.Message}",
                KeyNotFoundException => "The requested resource was not found",
                FileNotFoundException => "The requested file was not found",
                NotImplementedException => "This feature is not implemented yet",
                // Add more mappings as needed
                _ => "An unexpected error occurred. Please try again later."
            };
        }

        private string GetProblemTitle(Exception exception)
        {
            return exception switch
            {
                UnauthorizedAccessException => "Unauthorized",
                ArgumentException => "Bad Request",
                InvalidOperationException => "Bad Request",
                KeyNotFoundException => "Not Found",
                FileNotFoundException => "Not Found",
                NotImplementedException => "Not Implemented",
                // Add more mappings as needed
                _ => "Internal Server Error"
            };
        }

        private string GetProblemType(Exception exception)
        {
            string baseUrl = "https://httpstatuses.com/";

            return exception switch
            {
                UnauthorizedAccessException => $"{baseUrl}401",
                ArgumentException => $"{baseUrl}400",
                InvalidOperationException => $"{baseUrl}400",
                KeyNotFoundException => $"{baseUrl}404",
                FileNotFoundException => $"{baseUrl}404",
                NotImplementedException => $"{baseUrl}501",
                // Add more mappings as needed
                _ => $"{baseUrl}500"
            };
        }
    }
}