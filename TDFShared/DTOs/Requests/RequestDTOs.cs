using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using TDFShared.Models.User;
using TDFShared.Enums;
using TDFShared.Validation;

namespace TDFShared.DTOs.Requests
{
    /// <summary>
    /// DTO for creating a new request
    /// </summary>
    public class RequestCreateDto
    {
        /// <summary>User ID of the requester</summary>
        [Required(ErrorMessage = "User ID is required")]
        [Range(1, int.MaxValue, ErrorMessage = "User ID must be a positive number")]
        public int UserId { get; set; }

        /// <summary>Type of leave</summary>
        [Required(ErrorMessage = "Leave type is required")]
        public LeaveType LeaveType { get; set; }

        /// <summary>Start date of the request</summary>
        [Required(ErrorMessage = "Start date is required")]
        [FutureDate(1, ErrorMessage = "Start date must be at least 1 day from today")]
        public DateTime RequestStartDate { get; set; }

        /// <summary>End date of the request</summary>
        [DateRange(nameof(RequestStartDate), 30)]
        public DateTime? RequestEndDate { get; set; }

        /// <summary>Beginning time for partial day</summary>
        [RequiredForLeaveType(nameof(LeaveType), LeaveType.Permission, LeaveType.ExternalAssignment)]
        public TimeSpan? RequestBeginningTime { get; set; }

        /// <summary>Ending time for partial day</summary>
        [RequiredForLeaveType(nameof(LeaveType), LeaveType.Permission, LeaveType.ExternalAssignment)]
        [TimeRange(nameof(RequestBeginningTime))]
        public TimeSpan? RequestEndingTime { get; set; }

        /// <summary>Reason for the request</summary>
        [StringLength(500, ErrorMessage = "Reason cannot exceed 500 characters")]
        public string? RequestReason { get; set; }

        /// <summary>Remarks for the request</summary>
        [StringLength(500, ErrorMessage = "Remarks cannot exceed 500 characters")]
        public string? RequestRemarks { get; set; }
    }

    /// <summary>
    /// DTO for updating an existing request
    /// </summary>
    public class RequestUpdateDto
    {
        /// <summary>Type of leave</summary>
        [Required(ErrorMessage = "Leave type is required")]
        public LeaveType LeaveType { get; set; }

        /// <summary>Start date of the request</summary>
        [Required(ErrorMessage = "Start date is required")]
        [FutureDate(0, ErrorMessage = "Start date cannot be in the past")]
        public DateTime RequestStartDate { get; set; }

        /// <summary>End date of the request</summary>
        [DateRange(nameof(RequestStartDate), 30)]
        public DateTime? RequestEndDate { get; set; }

        /// <summary>Beginning time for partial day</summary>
        [RequiredForLeaveType(nameof(LeaveType), LeaveType.Permission, LeaveType.ExternalAssignment)]
        public TimeSpan? RequestBeginningTime { get; set; }

        /// <summary>Ending time for partial day</summary>
        [RequiredForLeaveType(nameof(LeaveType), LeaveType.Permission, LeaveType.ExternalAssignment)]
        [TimeRange(nameof(RequestBeginningTime))]
        public TimeSpan? RequestEndingTime { get; set; }

        /// <summary>Reason for the request</summary>
        [StringLength(500, ErrorMessage = "Reason cannot exceed 500 characters")]
        public string? RequestReason { get; set; }

        /// <summary>Remarks for the request</summary>
        [StringLength(500, ErrorMessage = "Remarks cannot exceed 500 characters")]
        public string? RequestRemarks { get; set; }

        /// <summary>Row version for concurrency</summary>
        [JsonPropertyName("rowVersion")]
        public byte[]? RowVersion { get; set; }
    }

    /// <summary>
    /// DTO for approving a request
    /// </summary>
    public class RequestApprovalDto
    {
        /// <summary>New status for the request (typically "Approved")</summary>
        [Required]
        [JsonPropertyName("status")]
        public RequestStatus Status { get; set; } = RequestStatus.Approved;

        /// <summary>Optional comment about the approval</summary>
        [JsonPropertyName("comment")]
        public string? Comment { get; set; }
                /// <summary>Remarks from updater/approver</summary>
        public string? ManagerRemarks { get; set; } // Optional remarks from updater/approver
    }

    /// <summary>
    /// DTO for rejecting a request
    /// </summary>
    public class RequestRejectDto
    {
        /// <summary>Reason for rejecting the request</summary>
        [Required]
        [JsonPropertyName("rejectReason")]
        public string RejectReason { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO for request pagination parameters
    /// </summary>
    public class RequestPaginationDto
    {
        /// <summary>Page number (1-based)</summary>
        [JsonPropertyName("page")]
        public int Page { get; set; } = 1;

        /// <summary>Number of items per page</summary>
        [JsonPropertyName("pageSize")]
        public int PageSize { get; set; } = 10;

        /// <summary>Field to sort by</summary>
        [JsonPropertyName("sortBy")]
        public string SortBy { get; set; } = "RequestFromDay";

        /// <summary>Sort direction (true for ascending, false for descending)</summary>
        [JsonPropertyName("ascending")]
        public bool Ascending { get; set; } = false;

        /// <summary>Filter by status (e.g., Pending, Approved). Null or omitted for all statuses.</summary>
        [JsonPropertyName("filterStatus")]
        public RequestStatus? FilterStatus { get; set; }

        /// <summary>Filter by request type (e.g., Annual, Unpaid). Null or omitted for all types.</summary>
        [JsonPropertyName("filterType")]
        public LeaveType? FilterType { get; set; }

        /// <summary>Filter for requests after this date</summary>
        [JsonPropertyName("fromDate")]
        public DateTime? FromDate { get; set; }

        /// <summary>Filter for requests before this date</summary>
        [JsonPropertyName("toDate")]
        public DateTime? ToDate { get; set; }

        /// <summary>Optional: Filter by User ID</summary>
        [JsonPropertyName("userId")]
        public int? UserId { get; set; }

        /// <summary>Optional: Filter by Department</summary>
        [JsonPropertyName("department")]
        public string? Department { get; set; }

        /// <summary>Optional: If true, only return the total count of matching records</summary>
        [JsonPropertyName("countOnly")]
        public bool CountOnly { get; set; } = false;
    }

    /// <summary>
    /// Complete DTO for request data returned by the API
    /// </summary>
    public class RequestResponseDto
    {
        /// <summary>Unique ID of the request</summary>
        [Required]
        public int RequestID { get; set; }

        /// <summary>User ID of the requester</summary>
        [Required]
        public int RequestUserID { get; set; }

        /// <summary>Name of the requester</summary>
        public string? UserName { get; set; } // Added for display

        /// <summary>Type of leave</summary>
        [Required]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public LeaveType LeaveType { get; set; }

        /// <summary>Start date of the request</summary>
        [Required]
        public DateTime RequestStartDate { get; set; }

        /// <summary>End date of the request</summary>
        public DateTime? RequestEndDate { get; set; }

        /// <summary>Beginning time for partial day</summary>
        public TimeSpan? RequestBeginningTime { get; set; }

        /// <summary>Ending time for partial day</summary>
        public TimeSpan? RequestEndingTime { get; set; }

        /// <summary>Reason for the request</summary>
        public string? RequestReason { get; set; }

        /// <summary>Department of the requester</summary>
        public string RequestDepartment { get; set; } = string.Empty;

        /// <summary>Status of the request</summary>
        [Required]
        public RequestStatus Status { get; set; } = Enums.RequestStatus.Pending;

        /// <summary>HR status of the request</summary>
        public RequestStatus HRStatus { get; set; } = Enums.RequestStatus.Pending;

        /// <summary>Remarks from approver/admin</summary>
        public string? Remarks { get; set; } // Approver/Admin remarks

        /// <summary>Name of the approver</summary>
        public string? ApproverName { get; set; } // Added for display

        /// <summary>Date the request was created</summary>
        public DateTime CreatedDate { get; set; }

        /// <summary>Date the request was last modified</summary>
        public DateTime? LastModifiedDate { get; set; }

        /// <summary>Number of days requested</summary>
        public int? RequestNumberOfDays { get; set; }

        /// <summary>Remaining balance of leave</summary>
        public int? RemainingBalance { get; set; }

        /// <summary>Row version for concurrency</summary>
        [JsonPropertyName("rowVersion")]
        public byte[]? RowVersion { get; set; }
    }

    /// <summary>
    /// DTO for updating the status of a request
    /// </summary>
    public class RequestStatusUpdateDto
    {
        /// <summary>New status of the request</summary>
        public RequestStatus Status { get; set; } // e.g., Approved, Rejected, Cancelled

        /// <summary>Remarks for the status update</summary>
        public string? Remarks { get; set; }
    }
}