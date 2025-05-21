using System;
using System.Security.Cryptography;
using System.Text;

namespace TDFShared.Services
{
    public static class Security
    {
        public static bool VerifyPassword(string password, string storedHash, string salt)
        {
            string hash = HashPassword(password, salt);
            return hash == storedHash;
        }

        public static string HashPassword(string password, string salt)
        {
            using var sha256 = SHA256.Create();
            byte[] passwordBytes = Encoding.UTF8.GetBytes(password + salt);
            byte[] hashBytes = sha256.ComputeHash(passwordBytes);
            return Convert.ToBase64String(hashBytes);
        }

        public static string GenerateSalt()
        {
            byte[] saltBytes = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(saltBytes);
            return Convert.ToBase64String(saltBytes);
        }
    }
}