using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using TDFShared.DTOs.Users;
using TDFShared.DTOs.Requests;
using TDFShared.Enums;
using TDFShared.Models.User;

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
        public Guid Id { get; set; }

        [Key]
        [Column("RequestID")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int RequestID { get; set; }

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
        public LeaveType RequestType { get; set; }

        /// <summary>
        /// Reason for the request provided by the requester
        /// </summary>
        public string RequestReason { get; set; } = string.Empty;

        /// <summary>
        /// Current status of the request (Pending, Approved, Rejected)
        /// </summary>
        public RequestStatus RequestStatus { get; set; } = Enums.RequestStatus.Pending;

        /// <summary>
        /// Person who closed the request (approver or rejecter)
        /// </summary>
        public string? RequestCloser { get; set; }

        /// <summary>
        /// Manager who approved the request
        /// </summary>
        public int? ManagerApproverId { get; set; }

        /// <summary>
        /// HR department status for the request
        /// </summary>
        public RequestStatus RequestHRStatus { get; set; } = RequestStatus.Pending;

        /// <summary>
        /// HR person who closed the request
        /// </summary>
        public string? RequestHRCloser { get; set; }

        public int? HRApproverId { get; set; }

        public string? HRRemarks { get; set; }

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
        /// Manager's remarks when approving/rejecting the request
        /// </summary>
        public string? ManagerRemarks { get; set; }

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
        public virtual UserEntity? User { get; set; }

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
}