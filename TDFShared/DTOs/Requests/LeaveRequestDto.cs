using System.Text.Json.Serialization;

namespace TDFShared.DTOs.Requests
{
    /// <summary>
    /// DTO representing a leave request summary for display.
    /// </summary>
    public class LeaveRequestDto
    {
        /// <summary>
        /// Unique identifier for the leave request.
        /// </summary>
        [JsonPropertyName("id")]
        public int Id { get; set; }

        /// <summary>
        /// Type of leave (e.g., Vacation, Sick).
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Current status of the request (e.g., Pending, Approved, Rejected).
        /// </summary>
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Formatted string representing the date range of the leave.
        /// </summary>
        [JsonPropertyName("dateRange")]
        public string DateRange { get; set; } = string.Empty;
    }
} 