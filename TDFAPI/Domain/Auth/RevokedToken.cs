using System;
using System.ComponentModel.DataAnnotations;

namespace TDFAPI.Domain.Auth
{
    public class RevokedToken
    {
        /// <summary>
        /// The unique identifier (JTI - JWT ID) of the revoked token.
        /// This will be the primary key.
        /// </summary>
        [Key]
        [Required]
        [MaxLength(100)] // Ensure ample size for JWT IDs
        public string Jti { get; set; } = string.Empty;

        /// <summary>
        /// The date and time until which this revocation record should be kept.
        /// After this time, the record can be safely deleted as the original token would have expired anyway.
        /// </summary>
        [Required]
        public DateTime ExpiryDateUtc { get; set; }
    }
} 