using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using TDFShared.DTOs.Common;
using TDFShared.Exceptions;

namespace TDFShared.Utilities
{
    /// <summary>
    /// Utilities for handling API responses and creating standardized error responses
    /// </summary>
    public static class ApiResponseUtilities
    {
        /// <summary>
        /// Creates a successful API response
        /// </summary>
        /// <typeparam name="T">Type of data</typeparam>
        /// <param name="data">Response data</param>
        /// <param name="message">Success message</param>
        /// <param name="statusCode">HTTP status code</param>
        /// <returns>Successful API response</returns>
        public static ApiResponse<T> Success<T>(T data, string message = "Operation completed successfully", HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            return ApiResponse<T>.SuccessResponse(data, message, statusCode);
        }

        /// <summary>
        /// Creates an error API response
        /// </summary>
        /// <typeparam name="T">Type of data</typeparam>
        /// <param name="message">Error message</param>
        /// <param name="statusCode">HTTP status code</param>
        /// <param name="errorDetails">Additional error details</param>
        /// <param name="validationErrors">Validation errors</param>
        /// <returns>Error API response</returns>
        public static ApiResponse<T> Error<T>(string message, HttpStatusCode statusCode = HttpStatusCode.BadRequest, 
            string? errorDetails = null, Dictionary<string, List<string>>? validationErrors = null)
        {
            return ApiResponse<T>.ErrorResponse(message, statusCode, errorDetails, validationErrors);
        }

        /// <summary>
        /// Creates an API response from an exception
        /// </summary>
        /// <typeparam name="T">Type of data</typeparam>
        /// <param name="exception">Exception to convert</param>
        /// <param name="includeStackTrace">Whether to include stack trace in development</param>
        /// <returns>Error API response</returns>
        public static ApiResponse<T> FromException<T>(Exception exception, bool includeStackTrace = false)
        {
            return exception switch
            {
                ApiException apiEx => Error<T>(apiEx.Message, apiEx.StatusCode, 
                    includeStackTrace ? apiEx.StackTrace : null, 
                    apiEx.ValidationErrors?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToList())),
                
                ValidationException valEx => Error<T>(valEx.Message, HttpStatusCode.BadRequest, 
                    includeStackTrace ? valEx.StackTrace : null),
                
                UnauthorizedAccessException => Error<T>("Access denied", HttpStatusCode.Unauthorized),
                
                ArgumentException argEx => Error<T>(argEx.Message, HttpStatusCode.BadRequest, 
                    includeStackTrace ? argEx.StackTrace : null),
                
                InvalidOperationException invOpEx => Error<T>(invOpEx.Message, HttpStatusCode.Conflict, 
                    includeStackTrace ? invOpEx.StackTrace : null),
                
                NotImplementedException => Error<T>("Feature not implemented", HttpStatusCode.NotImplemented),
                
                TimeoutException => Error<T>("Request timed out", HttpStatusCode.RequestTimeout),
                
                _ => Error<T>("An unexpected error occurred", HttpStatusCode.InternalServerError, 
                    includeStackTrace ? exception.ToString() : exception.Message)
            };
        }

        /// <summary>
        /// Tries to parse an API response from JSON content
        /// </summary>
        /// <typeparam name="T">Type of data</typeparam>
        /// <param name="jsonContent">JSON content to parse</param>
        /// <param name="options">JSON serializer options</param>
        /// <returns>Parsed API response or null if parsing failed</returns>
        public static ApiResponse<T>? TryParseApiResponse<T>(string jsonContent, JsonSerializerOptions? options = null)
        {
            if (string.IsNullOrEmpty(jsonContent))
                return null;

            try
            {
                options ??= new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                return JsonSerializer.Deserialize<ApiResponse<T>>(jsonContent, options);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>
        /// Extracts error message from HTTP response content
        /// </summary>
        /// <param name="responseContent">HTTP response content</param>
        /// <param name="defaultMessage">Default message if extraction fails</param>
        /// <returns>Extracted or default error message</returns>
        public static string ExtractErrorMessage(string responseContent, string defaultMessage = "An error occurred")
        {
            if (string.IsNullOrEmpty(responseContent))
                return defaultMessage;

            try
            {
                // Try to parse as ApiResponse first
                var apiResponse = TryParseApiResponse<object>(responseContent);
                if (apiResponse != null && !string.IsNullOrEmpty(apiResponse.Message))
                {
                    return apiResponse.Message;
                }

                // Try to parse as a simple error object
                using var document = JsonDocument.Parse(responseContent);
                var root = document.RootElement;

                // Look for common error message properties
                var messageProperties = new[] { "message", "error", "errorMessage", "detail", "title" };
                
                foreach (var prop in messageProperties)
                {
                    if (root.TryGetProperty(prop, out var element) && element.ValueKind == JsonValueKind.String)
                    {
                        var message = element.GetString();
                        if (!string.IsNullOrEmpty(message))
                            return message;
                    }
                }

                // If no structured error found, return the raw content (truncated if too long)
                return responseContent.Length > 200 ? responseContent.Substring(0, 200) + "..." : responseContent;
            }
            catch (JsonException)
            {
                // If JSON parsing fails, return raw content or default message
                return responseContent.Length > 200 ? responseContent.Substring(0, 200) + "..." : responseContent;
            }
        }

        /// <summary>
        /// Gets a user-friendly error message based on HTTP status code
        /// </summary>
        /// <param name="statusCode">HTTP status code</param>
        /// <param name="responseContent">Response content for additional context</param>
        /// <returns>User-friendly error message</returns>
        public static string GetFriendlyErrorMessage(HttpStatusCode statusCode, string? responseContent = null)
        {
            var baseMessage = statusCode switch
            {
                HttpStatusCode.BadRequest => "Invalid request. Please check your input and try again.",
                HttpStatusCode.Unauthorized => "Authentication required. Please log in again.",
                HttpStatusCode.Forbidden => "You don't have permission to access this resource.",
                HttpStatusCode.NotFound => "The requested resource was not found.",
                HttpStatusCode.MethodNotAllowed => "This operation is not allowed.",
                HttpStatusCode.Conflict => "The request conflicts with the current state of the resource.",
                HttpStatusCode.UnprocessableEntity => "The request contains invalid data.",
                HttpStatusCode.TooManyRequests => "Too many requests. Please wait before trying again.",
                HttpStatusCode.InternalServerError => "A server error occurred. Please try again later.",
                HttpStatusCode.BadGateway => "Service temporarily unavailable. Please try again later.",
                HttpStatusCode.ServiceUnavailable => "Service temporarily unavailable. Please try again later.",
                HttpStatusCode.GatewayTimeout => "The request timed out. Please try again.",
                _ => $"Request failed with status {(int)statusCode}: {statusCode}"
            };

            // Try to extract more specific error message from response content
            if (!string.IsNullOrEmpty(responseContent))
            {
                var extractedMessage = ExtractErrorMessage(responseContent, baseMessage);
                if (extractedMessage != baseMessage && !string.IsNullOrEmpty(extractedMessage))
                {
                    return extractedMessage;
                }
            }

            return baseMessage;
        }

        /// <summary>
        /// Determines if an HTTP status code indicates a retryable error
        /// </summary>
        /// <param name="statusCode">HTTP status code</param>
        /// <returns>True if the error is retryable, false otherwise</returns>
        public static bool IsRetryableError(HttpStatusCode statusCode)
        {
            return statusCode switch
            {
                HttpStatusCode.RequestTimeout => true,
                HttpStatusCode.TooManyRequests => true,
                HttpStatusCode.InternalServerError => true,
                HttpStatusCode.BadGateway => true,
                HttpStatusCode.ServiceUnavailable => true,
                HttpStatusCode.GatewayTimeout => true,
                _ => false
            };
        }

        /// <summary>
        /// Determines if an exception indicates a retryable error
        /// </summary>
        /// <param name="exception">Exception to check</param>
        /// <returns>True if the error is retryable, false otherwise</returns>
        public static bool IsRetryableException(Exception exception)
        {
            return exception switch
            {
                ApiException apiEx => IsRetryableError(apiEx.StatusCode),
                TimeoutException => true,
                TaskCanceledException => true,
                HttpRequestException => true,
                _ => false
            };
        }
    }
}
