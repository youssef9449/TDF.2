using System;
using System.Security.Cryptography;
using System.Text;

namespace TDFShared.Services
{
    /// <summary>
    /// DEPRECATED: Use SecurityService instead for better security and modern cryptographic standards
    /// This class is maintained for backward compatibility but uses weak SHA-256 hashing
    /// </summary>
    [Obsolete("Use SecurityService instead. This class uses weak SHA-256 hashing and will be removed in a future version.")]
    public static class Security
    {
        private static readonly ISecurityService _securityService = new SecurityService();

        /// <summary>
        /// DEPRECATED: Use SecurityService.VerifyPassword() instead
        /// This method uses weak SHA-256 hashing for backward compatibility
        /// </summary>
        [Obsolete("Use SecurityService.VerifyPassword() instead")]
        public static bool VerifyPassword(string password, string storedHash, string salt)
        {
            string hash = HashPassword(password, salt);
            return hash == storedHash;
        }

        /// <summary>
        /// DEPRECATED: Use SecurityService.HashPassword() instead
        /// This method uses weak SHA-256 hashing for backward compatibility
        /// </summary>
        [Obsolete("Use SecurityService.HashPassword() instead")]
        public static string HashPassword(string password, string salt)
        {
            using var sha256 = SHA256.Create();
            byte[] passwordBytes = Encoding.UTF8.GetBytes(password + salt);
            byte[] hashBytes = sha256.ComputeHash(passwordBytes);
            return Convert.ToBase64String(hashBytes);
        }

        /// <summary>
        /// DEPRECATED: Use SecurityService.GenerateSalt() instead
        /// </summary>
        [Obsolete("Use SecurityService.GenerateSalt() instead")]
        public static string GenerateSalt()
        {
            return _securityService.GenerateSalt();
        }
    }
}