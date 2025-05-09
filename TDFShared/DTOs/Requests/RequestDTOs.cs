using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using TDFShared.Models.User;

namespace TDFShared.DTOs.Requests
{
    /// <summary>
    /// DTO for creating a new request
    /// </summary>
    public class RequestCreateDto
    {
        [Required]
        public int UserId { get; set; }

        [Required]
        public string LeaveType { get; set; } = string.Empty;
        
        [Required]
        public DateTime RequestStartDate { get; set; }
        
        public DateTime? RequestEndDate { get; set; } // Nullable if not always required
        
        public TimeSpan? RequestBeginningTime { get; set; }
        
        public TimeSpan? RequestEndingTime { get; set; }
        
        public string? RequestReason { get; set; }
    }

    /// <summary>
    /// DTO for updating an existing request
    /// </summary>
    public class RequestUpdateDto 
    {
        [Required]
        public string LeaveType { get; set; } = string.Empty;
        
        [Required]
        public DateTime RequestStartDate { get; set; }
        
        public DateTime? RequestEndDate { get; set; } // Nullable if not always required
        
        public TimeSpan? RequestBeginningTime { get; set; }
        
        public TimeSpan? RequestEndingTime { get; set; }
        
        public string? RequestReason { get; set; }
        
        public string? Remarks { get; set; } // Optional remarks from updater/approver

        /// <summary>
        /// Used for optimistic concurrency control
        /// </summary>
        [JsonPropertyName("rowVersion")]
        public byte[]? RowVersion { get; set; }
    }

    /// <summary>
    /// DTO for approving a request
    /// </summary>
    public class RequestApprovalDto
    {
        /// <summary>
        /// New status for the request (typically "Approved")
        /// </summary>
        [Required]
        [JsonPropertyName("status")]
        public string Status { get; set; } = "Approved";

        /// <summary>
        /// Optional comment about the approval
        /// </summary>
        [JsonPropertyName("comment")]
        public string? Comment { get; set; }
    }

    /// <summary>
    /// DTO for rejecting a request
    /// </summary>
    public class RequestRejectDto
    {
        /// <summary>
        /// Reason for rejecting the request
        /// </summary>
        [Required]
        [JsonPropertyName("rejectReason")]
        public string RejectReason { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO for request pagination parameters
    /// </summary>
    public class RequestPaginationDto
    {
        /// <summary>
        /// Page number (1-based)
        /// </summary>
        [JsonPropertyName("page")]
        public int Page { get; set; } = 1;

        /// <summary>
        /// Number of items per page
        /// </summary>
        [JsonPropertyName("pageSize")]
        public int PageSize { get; set; } = 10;

        /// <summary>
        /// Field to sort by
        /// </summary>
        [JsonPropertyName("sortBy")]
        public string SortBy { get; set; } = "RequestFromDay";

        /// <summary>
        /// Sort direction (true for ascending, false for descending)
        /// </summary>
        [JsonPropertyName("ascending")]
        public bool Ascending { get; set; } = false;

        /// <summary>
        /// Filter by status (or "All")
        /// </summary>
        [JsonPropertyName("filterStatus")]
        public string FilterStatus { get; set; } = "All";

        /// <summary>
        /// Filter by request type (or "All")
        /// </summary>
        [JsonPropertyName("filterType")]
        public string FilterType { get; set; } = "All";

        /// <summary>
        /// Filter for requests after this date
        /// </summary>
        [JsonPropertyName("fromDate")]
        public DateTime? FromDate { get; set; }

        /// <summary>
        /// Filter for requests before this date
        /// </summary>
        [JsonPropertyName("toDate")]
        public DateTime? ToDate { get; set; }

        /// <summary>
        /// Optional: Filter by User ID
        /// </summary>
        [JsonPropertyName("userId")]
        public int? UserId { get; set; }

        /// <summary>
        /// Optional: Filter by Department
        /// </summary>
        [JsonPropertyName("department")]
        public string? Department { get; set; }

        /// <summary>
        /// Optional: If true, only return the total count of matching records
        /// </summary>
        [JsonPropertyName("countOnly")]
        public bool CountOnly { get; set; } = false;
    }

    /// <summary>
    /// Complete DTO for request data returned by the API
    /// </summary>
    public class RequestResponseDto
    {
        [Required]
        public Guid Id { get; set; } // Changed from int to Guid
        
        [Required]
        public int RequestUserID { get; set; }
        
        public string? UserName { get; set; } // Added for display

        [Required]
        public string LeaveType { get; set; } = string.Empty;
        
        [Required]
        public DateTime RequestStartDate { get; set; }
        
        public DateTime? RequestEndDate { get; set; }
        
        public TimeSpan? RequestBeginningTime { get; set; }
        
        public TimeSpan? RequestEndingTime { get; set; }
        
        public string? RequestReason { get; set; }

        public string RequestDepartment { get; set; } = string.Empty;

        [Required]
        public string Status { get; set; } = "Pending";
        
        public string? Remarks { get; set; } // Approver/Admin remarks
        
        public string? ApproverName { get; set; } // Added for display
        
        public DateTime CreatedDate { get; set; }
        
        public DateTime? LastModifiedDate { get; set; }
        
        // Optional calculated fields, remove if not provided by API
        public int? RequestNumberOfDays { get; set; }
        public int? RemainingBalance { get; set; }

        /// <summary>
        /// Used for optimistic concurrency control
        /// </summary>
        [JsonPropertyName("rowVersion")]
        public byte[]? RowVersion { get; set; }
    }

    public class RequestStatusUpdateDto
    {
        public string Status { get; set; } // e.g., Approved, Rejected, Cancelled
        public string? Remarks { get; set; }
    }
} 