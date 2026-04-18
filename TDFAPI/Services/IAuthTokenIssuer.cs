using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TDFShared.DTOs.Auth;
using TDFShared.DTOs.Users;

namespace TDFAPI.Services
{
    /// <summary>
    /// Server-facing authentication contract used by TDFAPI controllers and
    /// MediatR handlers. Owns operations that only make sense with database
    /// access and the signing key on hand: issuing new JWTs, revoking
    /// tokens, hashing passwords, and reading claims from the current
    /// <c>HttpContext</c>. The MAUI client consumes
    /// <see cref="TDFShared.Contracts.IAuthClient"/> instead and has no
    /// knowledge of any of these operations.
    /// </summary>
    public interface IAuthTokenIssuer
    {
        /// <summary>Issues a fresh access+refresh pair for a verified user.</summary>
        Task<TokenResponse?> LoginAsync(string username, string password);

        /// <summary>Rotates the access token using a refresh token.</summary>
        Task<TokenResponse?> RefreshTokenAsync(string token, string refreshToken);

        /// <summary>Hashes a password with a generated per-user salt.</summary>
        string HashPassword(string password, out string salt);

        /// <summary>Verifies a password against a stored hash and salt.</summary>
        bool VerifyPassword(string password, string storedHash, string salt);

        /// <summary>Revokes a JTI so subsequent presentations are rejected.</summary>
        Task RevokeTokenAsync(string jti, DateTime expiryDateUtc, int? userId = null);

        /// <summary>Checks whether a JTI is on the revocation list.</summary>
        Task<bool> IsTokenRevokedAsync(string jti);

        /// <summary>Revokes the current caller's token (used by the server-side logout endpoint).</summary>
        Task<bool> LogoutAsync();

        /// <summary>Reads the current user's ID from the ASP.NET claims principal.</summary>
        Task<int> GetCurrentUserIdAsync();

        /// <summary>Reads the current user's roles from the ASP.NET claims principal.</summary>
        Task<IReadOnlyList<string>> GetUserRolesAsync();

        /// <summary>Reads the current user's department claim.</summary>
        Task<string?> GetCurrentUserDepartmentAsync();

        /// <summary>Loads the full <see cref="UserDto"/> for the authenticated caller.</summary>
        Task<UserDto?> GetCurrentUserAsync();
    }
}
