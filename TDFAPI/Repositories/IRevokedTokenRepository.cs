using System;
using System.Threading.Tasks;
using TDFAPI.Domain.Auth;

namespace TDFAPI.Repositories
{
    public interface IRevokedTokenRepository
    {
        /// <summary>
        /// Adds a token identifier (JTI) to the revocation list.
        /// </summary>
        /// <param name="jti">The JWT ID to revoke.</param>
        /// <param name="expiryDateUtc">The expiry date of the original token.</param>
        Task AddAsync(string jti, DateTime expiryDateUtc);

        /// <summary>
        /// Checks if a token identifier (JTI) exists in the revocation list.
        /// </summary>
        /// <param name="jti">The JWT ID to check.</param>
        /// <returns>True if the token is revoked, false otherwise.</returns>
        Task<bool> IsRevokedAsync(string jti);

        /// <summary>
        /// Removes expired revocation records from the store.
        /// </summary>
        Task RemoveExpiredAsync();
    }
} 