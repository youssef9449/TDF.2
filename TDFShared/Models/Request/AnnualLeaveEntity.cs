using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TDFShared.Models.Request
{
    [Table("AnnualLeave")]
    public class AnnualLeaveEntity
    {
        [Key]
        [Column("UserID")]
        public int UserID { get; set; }
        
        [Column("FullName")]
        [StringLength(100)]
        public string FullName { get; set; }
        
        [Column("Annual")]
        public int Annual { get; set; }
        
        [Column("EmergencyLeave")]
        public int EmergencyLeave { get; set; }
        
        [Column("AnnualUsed")]
        public int AnnualUsed { get; set; } = 0;
        
        [Column("EmergencyUsed")]
        public int EmergencyUsed { get; set; } = 0;
        
        // Method to calculate Annual Balance
        public int GetAnnualBalance() => Annual - AnnualUsed;
        
        // Method to calculate Emergency Balance
        public int GetEmergencyBalance() => EmergencyLeave - EmergencyUsed;
       // Method to calculate Permissions Balance
        public int GetPermissionsBalance() => Permissions - PermissionsUsed;
        
        [Column("Permissions")]
        public int Permissions { get; set; }
        
        [Column("PermissionsUsed")]
        public int PermissionsUsed { get; set; }   
        
        [Column("UnpaidUsed")]
        public int UnpaidUsed { get; set; } = 0;

        [Column("WorkFromHomeUsed")]
        public int WorkFromHomeUsed { get; set; } = 0;
        
    }
} 