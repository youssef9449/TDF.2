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
          /// Request ID
         /// </summary>
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
        [Required]
        [Column("RequestType")]
        public LeaveType RequestType { get; set; }


        /// <summary>
        /// Reason for the request provided by the requester
        /// </summary>
        [Column("RequestReason")]
        [MaxLength(255)]
        public string? RequestReason { get; set; }


        /// <summary>
        /// Current status of the request (Pending, Approved, Rejected)
        /// </summary>
        [Required]
        [Column("RequestManagerStatus")]
        public RequestStatus RequestManagerStatus { get; set; } = Enums.RequestStatus.Pending;


        /// <summary>
        /// HR department status for the request
        /// </summary>
        [Required]
        [Column("RequestHRStatus")]
        public RequestStatus RequestHRStatus { get; set; } = RequestStatus.Pending;


        /// <summary>
        /// Manager who approved the request
        /// </summary>
        [Column("ManagerApproverId")]
        public int? ManagerApproverId { get; set; }


        /// <summary>
        /// ID of the HR personnel who approved the request
        /// </summary>
        [Column("HRApproverId")]
        public int? HRApproverId { get; set; }


        /// <summary>
        /// Remarks or comments provided by HR regarding the request
        /// </summary>
        [Column("HRRemarks")]
        [MaxLength(255)]
        public string? HRRemarks { get; set; }


        /// <summary>
        /// Manager's remarks when approving/rejecting the request
        /// </summary>
        [Column("ManagerRemarks")]
        [MaxLength(255)]
        public string? ManagerRemarks { get; set; }


        /// <summary>
        /// Start date of the request period
        /// </summary>
        [Required]
        [Column("RequestFromDay", TypeName = "date")]
        public DateTime RequestFromDay { get; set; }


        /// <summary>
        /// End date of the request period (if applicable)
        /// </summary>
        [Column("RequestToDay", TypeName = "date")]
        public DateTime? RequestToDay { get; set; }


        /// <summary>
        /// Start time for partial day requests
        /// </summary>
        [Column("RequestBeginningTime", TypeName = "time(7)")]
        public TimeSpan? RequestBeginningTime { get; set; }


        /// <summary>
        /// End time for partial day requests
        /// </summary>
        [Column("RequestEndingTime", TypeName = "time(7)")]
        public TimeSpan? RequestEndingTime { get; set; }


        /// <summary>
        /// Department of the requesting user
        /// </summary>
        [Required]
        [Column("RequestDepartment")]
        [MaxLength(255)]
        public string RequestDepartment { get; set; } = string.Empty;


        /// <summary>
        /// Total number of days requested
        /// </summary>
        [Column("request_number_of_days")]
        public int? RequestNumberOfDays { get; set; }


        /// <summary>
        /// When the request was created
        /// </summary>
        [Column("CreatedAt")]
        public DateTime? CreatedAt { get; set; }


        /// <summary>
        /// When the request was last updated
        /// </summary>
        [Column("UpdatedAt")]
        public DateTime? UpdatedAt { get; set; }


        /// <summary>
        /// Row version for optimistic concurrency
        /// </summary>
        [Column("RowVersion")]
        [Timestamp]
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

    }
}