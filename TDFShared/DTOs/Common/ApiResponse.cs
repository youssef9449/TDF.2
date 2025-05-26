using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Net; // Added for HttpStatusCode

namespace TDFShared.DTOs.Common
{
    /// <summary>
    /// Base class for API responses containing common properties
    /// </summary>
    public class ApiResponseBase
    {
        /// <summary>
        /// Whether the request was successful
        /// </summary>
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        /// <summary>
        /// Message describing the result (especially useful for errors)
        /// </summary>
        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Optional detailed error message
        /// </summary>
        [JsonPropertyName("errorMessage")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// HTTP status code associated with the response
        /// </summary>
        [JsonPropertyName("statusCode")]
        public int StatusCode { get; set; } = (int)HttpStatusCode.OK;

        /// <summary>
        /// Optional validation errors if applicable
        /// </summary>
        [JsonPropertyName("errors")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, List<string>>? Errors { get; set; }
    }

    /// <summary>
    /// Standard API response wrapper for consistent response format
    /// </summary>
    /// <typeparam name="T">Type of data being returned</typeparam>
    public class ApiResponse<T> : ApiResponseBase
    {
        /// <summary>
        /// Whether the request was successful
        /// </summary>
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        /// <summary>
        /// Message describing the result (especially useful for errors)
        /// </summary>
        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty; // Initialize to avoid null issues

        /// <summary>
        /// Optional detailed error message
        /// </summary>
        [JsonPropertyName("errorMessage")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ErrorMessage { get; set; } // Added

        /// <summary>
        /// HTTP status code associated with the response
        /// </summary>
        [JsonPropertyName("statusCode")]
        public int StatusCode { get; set; } = (int)HttpStatusCode.OK; // Added and initialized

        /// <summary>
        /// The data payload of the response
        /// </summary>
        [JsonPropertyName("data")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public T? Data { get; set; } // Changed to nullable T?

        /// <summary>
        /// Optional validation errors if applicable
        /// </summary>
        [JsonPropertyName("errors")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, List<string>>? Errors { get; set; } // Changed to nullable dictionary?

        /// <summary>
        /// Default constructor
        /// </summary>
        public ApiResponse() { } // Added default constructor

        /// <summary>
        /// Creates a successful response with data
        /// </summary>
        public static ApiResponse<T> SuccessResponse(T data, string message = "Operation completed successfully", HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            return new ApiResponse<T>
            {
                Success = true,
                Message = message,
                Data = data,
                StatusCode = (int)statusCode
            };
        }

        /// <summary>
        /// Creates an error response
        /// </summary>
        public static ApiResponse<T> ErrorResponse(string message, HttpStatusCode statusCode = HttpStatusCode.BadRequest, string? errorMessage = null, Dictionary<string, List<string>>? errors = null)
        {
            return new ApiResponse<T>
            {
                Success = false,
                Message = message,
                StatusCode = (int)statusCode,
                ErrorMessage = errorMessage,
                Errors = errors,
                Data = default // Ensure data is default for error responses
            };
        }

        /// <summary>
        /// Creates an error response with validation errors
        /// </summary>
        /// <param name="validationErrors">Dictionary of field validation errors (string[])</param>
        /// <param name="message">Optional error message</param>
        /// <param name="statusCode">HTTP status code</param>
        /// <returns>An ApiResponse with validation errors</returns>
        public static ApiResponse<T> ValidationErrorResponse(Dictionary<string, string[]> validationErrors, string message = "Validation failed", HttpStatusCode statusCode = HttpStatusCode.BadRequest)
        {
            var errors = new Dictionary<string, List<string>>();
            if (validationErrors != null)
            {
                foreach (var key in validationErrors.Keys)
                {
                    if (validationErrors[key] != null && validationErrors[key].Any())
                    {
                        errors.Add(key, validationErrors[key].ToList());
                    }
                }
            }

            return new ApiResponse<T>
            {
                Success = false,
                Message = message,
                StatusCode = (int)statusCode,
                Errors = errors.Any() ? errors : null, // Only include errors if there are any
                Data = default
            };
        }

        /// <summary>
        /// Creates an error response from a ModelStateDictionary
        /// </summary>
        /// <param name="modelState">The ModelState containing validation errors</param>
        /// <param name="message">Optional error message</param>
        /// <param name="statusCode">HTTP status code</param>
        /// <returns>An ApiResponse with validation errors</returns>
        public static ApiResponse<T> FromModelState(
            Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary modelState,
            string message = "Validation failed",
            HttpStatusCode statusCode = HttpStatusCode.BadRequest)
        {
            var errors = new Dictionary<string, List<string>>();
            if (modelState != null)
            {
                foreach (var keyModelStatePair in modelState)
                {
                    var key = keyModelStatePair.Key;
                    var errorMessages = keyModelStatePair.Value.Errors?
                        .Select(error => error.ErrorMessage)
                        .Where(msg => !string.IsNullOrEmpty(msg))
                        .ToList();

                    if (errorMessages != null && errorMessages.Any())
                    {
                        errors.Add(key, errorMessages);
                    }
                }
            }

            return new ApiResponse<T>
            {
                Success = false,
                Message = message,
                StatusCode = (int)statusCode,
                Errors = errors.Any() ? errors : null,
                Data = default
            };
        }
    }
} 