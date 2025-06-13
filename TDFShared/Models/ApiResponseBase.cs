using System.Collections.Generic;

namespace TDFShared.Models
{
    /// <summary>
    /// Base class for API responses
    /// </summary>
    public class ApiResponseBase
    {
        /// <summary>
        /// Gets or sets whether the request was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the response message
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// Gets or sets the error message if the request failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the HTTP status code
        /// </summary>
        public int StatusCode { get; set; }

        /// <summary>
        /// Gets or sets any validation errors that occurred
        /// </summary>
        public Dictionary<string, string>? ValidationErrors { get; set; }
    }

    /// <summary>
    /// Generic base class for API responses with data
    /// </summary>
    /// <typeparam name="T">The type of data in the response</typeparam>
    public class ApiResponseBase<T> : ApiResponseBase
    {
        /// <summary>
        /// Gets or sets the response data
        /// </summary>
        public T? Data { get; set; }
    }
} 