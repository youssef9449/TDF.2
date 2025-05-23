using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TDFAPI.Domain.Auth
{
    [Table("RevokedTokens")]
    public class RevokedToken
    {
        /// <summary>
        /// The unique identifier for the revoked token record.
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// The unique identifier (JTI - JWT ID) of the revoked token.
        /// </summary>
        [Required]
        [MaxLength(100)]
        [Column("Jti")]
        public string Jti { get; set; } = string.Empty;

        /// <summary>
        /// The date and time when the token expires.
        /// </summary>
        [Required]
        [Column("ExpiryDate")]
        public DateTime ExpiryDate { get; set; }

        /// <summary>
        /// The date and time when the token was revoked.
        /// </summary>
        [Required]
        [Column("RevocationDate")]
        public DateTime RevocationDate { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// The ID of the user associated with the revoked token.
        /// </summary>
        [Column("UserId")]
        public int? UserId { get; set; }

        /// <summary>
        /// The reason for revoking the token (optional).
        /// </summary>
        [MaxLength(255)]
        [Column("Reason")]
        public string? Reason { get; set; }
    }
}