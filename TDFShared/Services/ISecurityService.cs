using System;
using System.Collections.Generic;
using System.Security.Claims;
using TDFShared.DTOs.Users;

namespace TDFShared.Services
{
    /// <summary>
    /// Interface for security operations including password hashing, validation, and JWT token operations
    /// </summary>
    public interface ISecurityService
    {
        /// <summary>
        /// Hashes a password using a secure algorithm with a generated salt
        /// </summary>
        /// <param name="password">The password to hash</param>
        /// <param name="salt">The generated salt (output parameter)</param>
        /// <returns>The hashed password</returns>
        string HashPassword(string password, out string salt);

        /// <summary>
        /// Hashes a password using a secure algorithm with a provided salt
        /// </summary>
        /// <param name="password">The password to hash</param>
        /// <param name="salt">The salt to use</param>
        /// <returns>The hashed password</returns>
        string HashPassword(string password, string salt);

        /// <summary>
        /// Verifies a password against a stored hash and salt
        /// </summary>
        /// <param name="password">The password to verify</param>
        /// <param name="storedHash">The stored password hash</param>
        /// <param name="salt">The salt used for hashing</param>
        /// <returns>True if the password is valid, false otherwise</returns>
        bool VerifyPassword(string password, string storedHash, string salt);

        /// <summary>
        /// Generates a cryptographically secure salt
        /// </summary>
        /// <returns>A base64-encoded salt</returns>
        string GenerateSalt();

        /// <summary>
        /// Validates password strength according to security requirements
        /// </summary>
        /// <param name="password">The password to validate</param>
        /// <param name="validationMessage">Detailed validation message if password is invalid</param>
        /// <returns>True if password meets requirements, false otherwise</returns>
        bool IsPasswordStrong(string password, out string validationMessage);

        /// <summary>
        /// Validates password strength and throws exception if invalid
        /// </summary>
        /// <param name="password">The password to validate</param>
        /// <exception cref="ValidationException">Thrown when password does not meet requirements</exception>
        void ValidatePassword(string password);

        /// <summary>
        /// Generates a JWT token for a user (client-side token generation for testing/development)
        /// </summary>
        /// <param name="user">The user to generate token for</param>
        /// <param name="secretKey">The secret key for signing</param>
        /// <param name="issuer">Token issuer</param>
        /// <param name="audience">Token audience</param>
        /// <param name="expirationMinutes">Token expiration in minutes</param>
        /// <returns>JWT token string</returns>
        string GenerateJwtToken(UserDto user, string secretKey, string issuer, string audience, int expirationMinutes = 60);

        /// <summary>
        /// Validates a JWT token and extracts claims
        /// </summary>
        /// <param name="token">The JWT token to validate</param>
        /// <param name="secretKey">The secret key for validation</param>
        /// <param name="issuer">Expected issuer</param>
        /// <param name="audience">Expected audience</param>
        /// <returns>Tuple containing validation result, claims principal, and error reason</returns>
        (bool isValid, ClaimsPrincipal? principal, string errorReason) ValidateJwtToken(string token, string secretKey, string issuer, string audience);

        /// <summary>
        /// Extracts user ID from JWT token claims
        /// </summary>
        /// <param name="principal">Claims principal from validated token</param>
        /// <returns>User ID if found, null otherwise</returns>
        int? GetUserIdFromClaims(ClaimsPrincipal principal);

        /// <summary>
        /// Extracts user roles from JWT token claims
        /// </summary>
        /// <param name="principal">Claims principal from validated token</param>
        /// <returns>List of user roles</returns>
        IEnumerable<string> GetRolesFromClaims(ClaimsPrincipal principal);
    }
}
