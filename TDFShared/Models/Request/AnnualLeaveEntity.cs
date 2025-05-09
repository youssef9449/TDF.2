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
        
        [Column("CasualLeave")]
        public int CasualLeave { get; set; }
        
        [Column("AnnualUsed")]
        public int AnnualUsed { get; set; } = 0;
        
        [Column("CasualUsed")]
        public int CasualUsed { get; set; } = 0;
        
        // Method to calculate Annual Balance
        public int GetAnnualBalance() => Annual - AnnualUsed;
        
        // Method to calculate Casual Balance
        public int GetCasualBalance() => CasualLeave - CasualUsed;
        
        [Column("Permissions")]
        public int Permissions { get; set; }
        
        [Column("PermissionsUsed")]
        public int PermissionsUsed { get; set; }
        
        // Method to calculate Permissions Balance
        public int GetPermissionsBalance() => Permissions - PermissionsUsed;
        
        [Column("UnpaidUsed")]
        public int UnpaidUsed { get; set; } = 0;
        
        // Add alias property for UnpaidLeaveUsed
        [NotMapped]
        public int UnpaidLeaveUsed
        {
            get { return UnpaidUsed; }
            set { UnpaidUsed = value; }
        }
    }
} 