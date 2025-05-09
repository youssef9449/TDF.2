using System;
using System.Text.Json.Serialization;

namespace TDFShared.DTOs.Messages
{
    /// <summary>
    /// DTO for message pagination parameters
    /// </summary>
    public class MessagePaginationDto
    {
        /// <summary>
        /// Page number (1-based)
        /// </summary>
        [JsonPropertyName("pageNumber")]
        public int PageNumber { get; set; } = 1;

        /// <summary>
        /// Number of items per page
        /// </summary>
        [JsonPropertyName("pageSize")]
        public int PageSize { get; set; } = 20;

        /// <summary>
        /// Only include unread messages
        /// </summary>
        [JsonPropertyName("unreadOnly")]
        public bool UnreadOnly { get; set; } = false;

        /// <summary>
        /// Filter by messages from a specific user
        /// </summary>
        [JsonPropertyName("fromUserId")]
        public int? FromUserId { get; set; }

        /// <summary>
        /// Filter by messages after this date
        /// </summary>
        [JsonPropertyName("startDate")]
        public DateTime? StartDate { get; set; }

        /// <summary>
        /// Filter by messages before this date
        /// </summary>
        [JsonPropertyName("endDate")]
        public DateTime? EndDate { get; set; }

        /// <summary>
        /// Sort messages by most recent first (default)
        /// </summary>
        [JsonPropertyName("sortDesc")]
        public bool SortDescending { get; set; } = true;
    }
} 