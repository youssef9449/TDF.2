using System.Collections.Generic;
using System.Threading.Tasks;
using TDFShared.DTOs.Auth;
using TDFShared.DTOs.Users;

namespace TDFShared.Contracts
{
    /// <summary>
    /// Client-facing authentication contract used by MAUI ViewModels and
    /// components that only need to log in, log out, refresh the current
    /// token, or read "who am I" claim data. Server-side operations
    /// (password hashing, revocation list, JWT signing) live on the
    /// TDFAPI-only <c>IAuthTokenIssuer</c> and are intentionally kept off
    /// this contract so clients cannot accidentally call them.
    /// </summary>
    public interface IAuthClient
    {
        /// <summary>Authenticates a user and caches the returned tokens locally.</summary>
        Task<TokenResponse?> LoginAsync(string username, string password);

        /// <summary>Uses the stored refresh token to obtain a new access token.</summary>
        Task<TokenResponse?> RefreshTokenAsync(string token, string refreshToken);

        /// <summary>Signs the user out locally and invalidates the current token server-side.</summary>
        Task<bool> LogoutAsync();

        /// <summary>Returns the cached access token (from secure storage or in-memory session) or null.</summary>
        Task<string?> GetCurrentTokenAsync();

        /// <summary>Persists a freshly issued access token into client-side storage.</summary>
        Task SetAuthenticationTokenAsync(string token);

        /// <summary>Extracts the current user's ID from the cached JWT.</summary>
        Task<int> GetCurrentUserIdAsync();

        /// <summary>Extracts the current user's roles from the cached JWT claims.</summary>
        Task<IReadOnlyList<string>> GetUserRolesAsync();

        /// <summary>Extracts the current user's department from the cached JWT claims.</summary>
        Task<string?> GetCurrentUserDepartmentAsync();

        /// <summary>Loads the full <see cref="UserDto"/> for the current user (may hit the API).</summary>
        Task<UserDto?> GetCurrentUserAsync();
    }
}
