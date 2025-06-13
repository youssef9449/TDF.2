using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TDFShared.DTOs.Common
{
    /// <summary>
    /// Represents a paginated result set
    /// </summary>
    /// <typeparam name="T">Type of items in the result</typeparam>
    public class PaginatedResult<T>
    {
        /// <summary>
        /// Items for the current page
        /// </summary>
        [JsonPropertyName("items")]
        public required IEnumerable<T> Items { get; set; }
        
        /// <summary>
        /// Total number of items across all pages
        /// </summary>
        [JsonPropertyName("totalCount")]
        public int TotalCount { get; set; }
        
        /// <summary>
        /// Current page number (1-based)
        /// </summary>
        [JsonPropertyName("pageNumber")]
        public int PageNumber { get; set; }
        
        /// <summary>
        /// Number of items per page
        /// </summary>
        [JsonPropertyName("pageSize")]
        public int PageSize { get; set; }
        
        /// <summary>
        /// Total number of pages
        /// </summary>
        [JsonPropertyName("totalPages")]
        public int TotalPages => (TotalCount + PageSize - 1) / PageSize;
        
        /// <summary>
        /// Whether there is a previous page
        /// </summary>
        [JsonPropertyName("hasPreviousPage")]
        public bool HasPreviousPage => PageNumber > 1;
        
        /// <summary>
        /// Whether there is a next page
        /// </summary>
        [JsonPropertyName("hasNextPage")]
        public bool HasNextPage => PageNumber < TotalPages;

        /// <summary>
        /// Default constructor
        /// </summary>
        public PaginatedResult() { }

        /// <summary>
        /// Creates a new paginated result with the specified items
        /// </summary>
        /// <param name="items">Items for the current page</param>
        /// <param name="page">Current page number</param>
        /// <param name="pageSize">Items per page</param>
        /// <param name="totalCount">Total items across all pages</param>
        public PaginatedResult(IEnumerable<T> items, int page, int pageSize, int totalCount)
        {
            Items = items;
            PageNumber = page;
            PageSize = pageSize;
            TotalCount = totalCount;
        }
    }
} 