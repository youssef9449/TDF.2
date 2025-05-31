using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TDFShared.Models.Request
{
    [Table("AnnualLeave")]
    /// <summary>
    /// Represents an entity for annual leave.
    /// </summary>
    public class AnnualLeaveEntity
    {
        [Key]
        [Column("UserID")]
        /// <summary>
        /// Gets or sets the user ID.
        /// </summary>
        public int UserID { get; set; }
        
        [Column("FullName")]
        [StringLength(100)]
        /// <summary>
        /// Gets or sets the full name of the user.
        /// </summary>
        public required string FullName { get; set; }
        
        [Column("Annual")]
        /// <summary>
        /// Gets or sets the total annual leave days.
        /// </summary>
        public int Annual { get; set; }
        
        [Column("EmergencyLeave")]
        /// <summary>
        /// Gets or sets the total emergency leave days.
        /// </summary>
        public int EmergencyLeave { get; set; }
        
        [Column("AnnualUsed")]
        /// <summary>
        /// Gets or sets the number of annual leave days used.
        /// </summary>
        public int AnnualUsed { get; set; } = 0;
        
        [Column("EmergencyUsed")]
        /// <summary>
        /// Gets or sets the number of emergency leave days used.
        /// </summary>
        public int EmergencyUsed { get; set; } = 0;
        
        // Method to calculate Annual Balance
        /// <summary>
        /// Calculates the remaining annual leave balance.
        /// </summary>
        /// <returns>The remaining annual leave days.</returns>
        public int GetAnnualBalance() => Annual - AnnualUsed;
        
        // Method to calculate Emergency Balance
        /// <summary>
        /// Calculates the remaining emergency leave balance.
        /// </summary>
        /// <returns>The remaining emergency leave days.</returns>
        public int GetEmergencyBalance() => EmergencyLeave - EmergencyUsed;
       // Method to calculate Permissions Balance
        /// <summary>
        /// Calculates the remaining permissions balance.
        /// </summary>
        /// <returns>The remaining permissions days.</returns>
        public int GetPermissionsBalance() => Permissions - PermissionsUsed;
        
        [Column("Permissions")]
        /// <summary>
        /// Gets or sets the total permissions days.
        /// </summary>
        public int Permissions { get; set; }
        
        [Column("PermissionsUsed")]
        /// <summary>
        /// Gets or sets the number of permissions days used.
        /// </summary>
        public int PermissionsUsed { get; set; }
        
        [Column("UnpaidUsed")]
        /// <summary>
        /// Gets or sets the number of unpaid leave days used.
        /// </summary>
        public int UnpaidUsed { get; set; } = 0;

        [Column("WorkFromHomeUsed")]
        /// <summary>
        /// Gets or sets the number of work from home days used.
        /// </summary>
        public int WorkFromHomeUsed { get; set; } = 0;
        
    }
} 