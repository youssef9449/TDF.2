using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TDFShared.DTOs.Users;
using TDFShared.Models.User;

namespace TDFAPI.Domain.Models.User
{
    // Represents the many-to-many relationship between Users and Roles
    public class UserRole
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public UserDto User { get; set; }

        [Required]
        [MaxLength(50)] // Max length for role name (e.g., "Admin", "Manager", "HR")
        public string RoleName { get; set; } = string.Empty;
    }
} 