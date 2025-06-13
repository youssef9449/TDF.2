using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TDFShared.Models.Request
{
    /// <summary>
    /// Represents an entity for annual leave.
    /// </summary>
    [Table("AnnualLeave")]
    public class AnnualLeaveEntity
    {
        /// <summary>
        /// Gets or sets the user ID.
        /// </summary>
        [Key]
        [Column("UserID")]
        public int UserID { get; set; }
        
        /// <summary>
        /// Gets or sets the full name of the user.
        /// </summary>
        [Column("FullName")]
        [StringLength(100)]
        public required string FullName { get; set; }
        
        /// <summary>
        /// Gets or sets the total annual leave days.
        /// </summary>
        [Column("Annual")]
        public int Annual { get; set; }
        
        /// <summary>
        /// Gets or sets the total emergency leave days.
        /// </summary>
        [Column("EmergencyLeave")]
        public int EmergencyLeave { get; set; }
        
        /// <summary>
        /// Gets or sets the number of annual leave days used.
        /// </summary>
        [Column("AnnualUsed")]
        public int AnnualUsed { get; set; } = 0;
        
        /// <summary>
        /// Gets or sets the number of emergency leave days used.
        /// </summary>
        [Column("EmergencyUsed")]
        public int EmergencyUsed { get; set; } = 0;
        
        /// <summary>
        /// Calculates the remaining annual leave balance.
        /// </summary>
        /// <returns>The remaining annual leave days.</returns>
        public int GetAnnualBalance() => Annual - AnnualUsed;
        
        /// <summary>
        /// Calculates the remaining emergency leave balance.
        /// </summary>
        /// <returns>The remaining emergency leave days.</returns>
        public int GetEmergencyBalance() => EmergencyLeave - EmergencyUsed;
        
        /// <summary>
        /// Calculates the remaining permissions balance.
        /// </summary>
        /// <returns>The remaining permissions days.</returns>
        public int GetPermissionsBalance() => Permissions - PermissionsUsed;
        
        /// <summary>
        /// Gets or sets the total permissions days.
        /// </summary>
        [Column("Permissions")]
        public int Permissions { get; set; }
        
        /// <summary>
        /// Gets or sets the number of permissions days used.
        /// </summary>
        [Column("PermissionsUsed")]
        public int PermissionsUsed { get; set; }
        
        /// <summary>
        /// Gets or sets the number of unpaid leave days used.
        /// </summary>
        [Column("UnpaidUsed")]
        public int UnpaidUsed { get; set; } = 0;

        /// <summary>
        /// Gets or sets the number of work from home days used.
        /// </summary>
        [Column("WorkFromHomeUsed")]
        public int WorkFromHomeUsed { get; set; } = 0;
    }
} 