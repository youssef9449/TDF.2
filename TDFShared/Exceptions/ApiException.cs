using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Runtime.Serialization;

namespace TDFShared.Exceptions
{
    /// <summary>
    /// Exception thrown for API-related errors
    /// </summary>
    public class ApiException : Exception
    {
        /// <summary>
        /// HTTP status code of the error
        /// </summary>
        public HttpStatusCode StatusCode { get; }
        
        /// <summary>
        /// Raw response content from the API
        /// </summary>
        public string ResponseContent { get; }
        
        /// <summary>
        /// Validation errors keyed by property name
        /// </summary>
        public Dictionary<string, string[]> ValidationErrors { get; }

        /// <summary>
        /// Creates a new API exception with status code and message
        /// </summary>
        public ApiException(HttpStatusCode statusCode, string message, string responseContent = null) 
            : base(message)
        {
            StatusCode = statusCode;
            ResponseContent = responseContent;
        }

        /// <summary>
        /// Creates a new API exception with status code, message, and validation errors
        /// </summary>
        public ApiException(HttpStatusCode statusCode, string message, Dictionary<string, string[]> validationErrors, string responseContent = null) 
            : this(statusCode, message, responseContent)
        {
            ValidationErrors = validationErrors;
        }
        
        /// <summary>
        /// Creates a new API exception with a message and inner exception
        /// </summary>
        public ApiException(string message, Exception innerException = null)
            : base(message, innerException)
        {
            StatusCode = HttpStatusCode.InternalServerError;
        }

        /// <summary>
        /// Indicates if this is an authentication/authorization error
        /// </summary>
        public bool IsAuthenticationError => 
            StatusCode == HttpStatusCode.Unauthorized || 
            StatusCode == HttpStatusCode.Forbidden;

        /// <summary>
        /// Indicates if this is a network-related error
        /// </summary>
        public bool IsNetworkError =>
            StatusCode == 0;

        /// <summary>
        /// Indicates if this is a client-side error (400-level)
        /// </summary>
        public bool IsClientError =>
            (int)StatusCode >= 400 && (int)StatusCode < 500;

        /// <summary>
        /// Indicates if this is a server-side error (500-level)
        /// </summary>
        public bool IsServerError =>
            (int)StatusCode >= 500;
    }
} 