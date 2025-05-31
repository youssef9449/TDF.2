using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TDFShared.DTOs.Auth;
using TDFShared.DTOs.Users;

namespace TDFShared.Services
{
    public interface IAuthService
    {
        /// <summary>
        /// Authenticates a user with the provided username and password
        /// </summary>
        Task<TokenResponse?> LoginAsync(string username, string password);
        
        /// <summary>
        /// Refreshes an expired access token using a refresh token
        /// </summary>
        Task<TokenResponse?> RefreshTokenAsync(string token, string refreshToken);
        
        /// <summary>
        /// Generates a JWT token for the specified user
        /// </summary>
        string GenerateJwtToken(UserDto user);
        
        /// <summary>
        /// Hashes a password with a generated salt
        /// </summary>
        string HashPassword(string password, out string salt);
        
        /// <summary>
        /// Verifies a password against a stored hash and salt
        /// </summary>
        bool VerifyPassword(string password, string storedHash, string salt);
        
        /// <summary>
        /// Revokes a token by its JTI (JWT ID)
        /// </summary>
        /// <param name="jti">The JWT ID of the token to revoke</param>
        /// <param name="expiryDateUtc">The expiration date of the token</param>
        /// <param name="userId">The ID of the user associated with the token (optional)</param>
        Task RevokeTokenAsync(string jti, DateTime expiryDateUtc, int? userId = null);
        
        /// <summary>
        /// Checks if a token has been revoked
        /// </summary>
        Task<bool> IsTokenRevokedAsync(string jti);
        
        /// <summary>
        /// Logs out the current user by revoking their tokens
        /// </summary>
        /// <returns>True if logout was successful, false otherwise</returns>
        Task<bool> LogoutAsync();

        /// <summary>
        /// Gets the current user's ID
        /// </summary>
        Task<int> GetCurrentUserIdAsync();

        /// <summary>
        /// Gets the roles of the current user
        /// </summary>
        /// <returns>A list of role names the user belongs to. Returns an empty list if no roles are found.</returns>
        /// <remarks>
        /// This method extracts roles from the current user's claims (from the JWT token).
        /// The roles are typically mapped from the 'role' claim or 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role' claim.
        /// </remarks>
        Task<IReadOnlyList<string>> GetUserRolesAsync();

        /// <summary>
        /// Gets the current user's department
        /// </summary>
        Task<string?> GetCurrentUserDepartmentAsync();

        /// <summary>
        /// Gets the current user's details
        /// </summary>
        Task<UserDto?> GetCurrentUserAsync();

        Task<string?> GetCurrentTokenAsync();
        Task SetAuthenticationTokenAsync(string token);
    }
}