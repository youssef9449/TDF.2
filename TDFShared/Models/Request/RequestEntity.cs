using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using TDFShared.DTOs.Users;

namespace TDFShared.Models.Request
{
    /// <summary>
    /// Represents a time off, leave, or other type of request in the system
    /// </summary>
    public class RequestEntity
    {
        /// <summary>
        /// Unique identifier for the request
        /// </summary>
        [Key]
        public Guid Id { get; set; }

        /// <summary>
        /// ID of the user who created the request
        /// </summary>
        public int RequestUserID { get; set; }

        /// <summary>
        /// Full name of the user who created the request
        /// </summary>
        public string RequestUserFullName { get; set; } = string.Empty;

        /// <summary>
        /// Type of request (e.g., "Time Off", "Sick Leave", etc.)
        /// </summary>
        public string RequestType { get; set; } = string.Empty;

        /// <summary>
        /// Reason for the request provided by the requester
        /// </summary>
        public string RequestReason { get; set; } = string.Empty;

        /// <summary>
        /// Current status of the request (Pending, Approved, Rejected)
        /// </summary>
        public string RequestStatus { get; set; } = "Pending";

        /// <summary>
        /// Person who closed the request (approver or rejecter)
        /// </summary>
        public string? RequestCloser { get; set; }

        /// <summary>
        /// HR department status for the request
        /// </summary>
        public string RequestHRStatus { get; set; } = "Pending";

        /// <summary>
        /// HR person who closed the request
        /// </summary>
        public string? RequestHRCloser { get; set; }

        /// <summary>
        /// Start date of the request period
        /// </summary>
        [Required]
        public DateTime RequestFromDay { get; set; }

        /// <summary>
        /// End date of the request period (if applicable)
        /// </summary>
        public DateTime? RequestToDay { get; set; }

        /// <summary>
        /// Start time for partial day requests
        /// </summary>
        public TimeSpan? RequestBeginningTime { get; set; }

        /// <summary>
        /// End time for partial day requests
        /// </summary>
        public TimeSpan? RequestEndingTime { get; set; }

        /// <summary>
        /// Reason provided if the request was rejected
        /// </summary>
        public string? RequestRejectReason { get; set; }

        /// <summary>
        /// Department of the requesting user
        /// </summary>
        public string RequestDepartment { get; set; } = string.Empty;

        /// <summary>
        /// Total number of days requested
        /// </summary>
        [Column("request_number_of_days")]
        public int RequestNumberOfDays { get; set; }

        /// <summary>
        /// Additional comments from the approver
        /// </summary>
        public string? ApproverComment { get; set; }

        /// <summary>
        /// When the request was created
        /// </summary>
        [Column("request_created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When the request was last updated
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// When the request was approved
        /// </summary>
        public DateTime? ApprovedAt { get; set; }

        /// <summary>
        /// When the request was rejected
        /// </summary>
        public DateTime? RejectedAt { get; set; }

        /// <summary>
        /// Row version for optimistic concurrency
        /// </summary>
        public byte[]? RowVersion { get; set; }

        /// <summary>
        /// Reference to the associated user entity (not DTO)
        /// </summary>
        [JsonIgnore]
        [ForeignKey("RequestUserID")]
        public virtual object? User { get; set; }

        /// <summary>
        /// User DTO for API responses (not mapped to database)
        /// </summary>
        [NotMapped]
        [JsonPropertyName("user")]
        public UserDto? UserDto { get; set; }

        /// <summary>
        /// Additional remarks for the request
        /// </summary>
        public string? Remarks { get; set; }
    }

    // Static class to hold Request Status constants
    public static class RequestStatus
    {
        public const string Pending = "Pending";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
        public const string Cancelled = "Cancelled"; // Added Cancelled Status
    }
}