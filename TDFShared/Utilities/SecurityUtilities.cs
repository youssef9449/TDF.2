using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace TDFShared.Utilities
{
    /// <summary>
    /// Security utility methods for common security operations
    /// </summary>
    public static class SecurityUtilities
    {
        /// <summary>
        /// Generates a cryptographically secure random string
        /// </summary>
        /// <param name="length">Length of the string to generate</param>
        /// <param name="includeSpecialChars">Whether to include special characters</param>
        /// <returns>Random string</returns>
        public static string GenerateSecureRandomString(int length, bool includeSpecialChars = false)
        {
            if (length <= 0) throw new ArgumentException("Length must be positive", nameof(length));

            const string letters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string digits = "0123456789";
            const string specialChars = "!@#$%^&*()_+-=[]{}|;:,.<>?";

            var chars = letters + digits;
            if (includeSpecialChars)
                chars += specialChars;

            var result = new StringBuilder(length);
            using (var rng = RandomNumberGenerator.Create())
            {
                var buffer = new byte[4];
                for (int i = 0; i < length; i++)
                {
                    rng.GetBytes(buffer);
                    var randomValue = BitConverter.ToUInt32(buffer, 0);
                    result.Append(chars[(int)(randomValue % chars.Length)]);
                }
            }

            return result.ToString();
        }

        /// <summary>
        /// Generates a secure password with specified requirements
        /// </summary>
        /// <param name="length">Password length (minimum 8)</param>
        /// <param name="requireUppercase">Require at least one uppercase letter</param>
        /// <param name="requireLowercase">Require at least one lowercase letter</param>
        /// <param name="requireDigits">Require at least one digit</param>
        /// <param name="requireSpecialChars">Require at least one special character</param>
        /// <returns>Generated password</returns>
        public static string GenerateSecurePassword(
            int length = 12,
            bool requireUppercase = true,
            bool requireLowercase = true,
            bool requireDigits = true,
            bool requireSpecialChars = true)
        {
            if (length < 8) throw new ArgumentException("Password length must be at least 8", nameof(length));

            const string uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string lowercase = "abcdefghijklmnopqrstuvwxyz";
            const string digits = "0123456789";
            const string specialChars = "!@#$%^&*()_+-=[]{}|;:,.<>?";

            var requiredChars = new List<char>();
            var allChars = new StringBuilder();

            if (requireUppercase)
            {
                requiredChars.Add(GetRandomChar(uppercase));
                allChars.Append(uppercase);
            }

            if (requireLowercase)
            {
                requiredChars.Add(GetRandomChar(lowercase));
                allChars.Append(lowercase);
            }

            if (requireDigits)
            {
                requiredChars.Add(GetRandomChar(digits));
                allChars.Append(digits);
            }

            if (requireSpecialChars)
            {
                requiredChars.Add(GetRandomChar(specialChars));
                allChars.Append(specialChars);
            }

            // Fill remaining length with random characters from all allowed sets
            var remainingLength = length - requiredChars.Count;
            var allCharsString = allChars.ToString();
            
            for (int i = 0; i < remainingLength; i++)
            {
                requiredChars.Add(GetRandomChar(allCharsString));
            }

            // Shuffle the password characters
            return new string(ShuffleArray(requiredChars.ToArray()));
        }

        /// <summary>
        /// Sanitizes input to prevent common injection attacks
        /// </summary>
        /// <param name="input">Input string to sanitize</param>
        /// <param name="allowHtml">Whether to allow HTML tags</param>
        /// <returns>Sanitized string</returns>
        public static string SanitizeInput(string? input, bool allowHtml = false)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            var sanitized = input.Trim();

            if (!allowHtml)
            {
                // Remove HTML tags
                sanitized = Regex.Replace(sanitized, @"<[^>]*>", string.Empty);
            }

            // Remove potentially dangerous characters for SQL injection
            sanitized = sanitized.Replace("'", "''"); // Escape single quotes
            sanitized = Regex.Replace(sanitized, @"--.*$", string.Empty, RegexOptions.Multiline); // Remove SQL comments
            sanitized = Regex.Replace(sanitized, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline); // Remove SQL block comments

            // Remove script injection attempts
            sanitized = Regex.Replace(sanitized, @"<script\b[^<]*(?:(?!<\/script>)<[^<]*)*<\/script>", string.Empty, RegexOptions.IgnoreCase);
            sanitized = Regex.Replace(sanitized, @"javascript:", string.Empty, RegexOptions.IgnoreCase);
            sanitized = Regex.Replace(sanitized, @"vbscript:", string.Empty, RegexOptions.IgnoreCase);
            sanitized = Regex.Replace(sanitized, @"onload", string.Empty, RegexOptions.IgnoreCase);
            sanitized = Regex.Replace(sanitized, @"onerror", string.Empty, RegexOptions.IgnoreCase);

            return sanitized;
        }

        /// <summary>
        /// Validates that a string contains only allowed characters
        /// </summary>
        /// <param name="input">Input to validate</param>
        /// <param name="allowedPattern">Regex pattern for allowed characters</param>
        /// <returns>True if input contains only allowed characters</returns>
        public static bool ContainsOnlyAllowedCharacters(string? input, string allowedPattern)
        {
            if (string.IsNullOrEmpty(input))
                return true;

            var regex = new Regex($"^{allowedPattern}*$", RegexOptions.Compiled);
            return regex.IsMatch(input);
        }

        /// <summary>
        /// Checks if a string contains potentially dangerous patterns
        /// </summary>
        /// <param name="input">Input to check</param>
        /// <returns>True if dangerous patterns are detected</returns>
        public static bool ContainsDangerousPatterns(string? input)
        {
            if (string.IsNullOrEmpty(input))
                return false;

            var dangerousPatterns = new[]
            {
                @"<script\b[^<]*(?:(?!<\/script>)<[^<]*)*<\/script>", // Script tags
                @"javascript:", // JavaScript protocol
                @"vbscript:", // VBScript protocol
                @"data:text/html", // Data URLs with HTML
                @"(?:'|--|\b(select|union|insert|drop|alter|declare|xp_)\b)", // SQL injection patterns
                @"(\b(exec|execute|sp_|xp_)\b)", // SQL stored procedure calls
                @"(\b(cmd|command|shell|system)\b)", // Command injection patterns
                @"(\.\./|\.\.\\)", // Directory traversal
                @"(%2e%2e%2f|%2e%2e%5c)", // URL encoded directory traversal
            };

            return dangerousPatterns.Any(pattern => 
                Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled));
        }

        /// <summary>
        /// Generates a secure token for various purposes (reset tokens, API keys, etc.)
        /// </summary>
        /// <param name="length">Token length in bytes (will be base64 encoded)</param>
        /// <returns>Base64 encoded secure token</returns>
        public static string GenerateSecureToken(int length = 32)
        {
            if (length <= 0) throw new ArgumentException("Length must be positive", nameof(length));

            using (var rng = RandomNumberGenerator.Create())
            {
                var tokenBytes = new byte[length];
                rng.GetBytes(tokenBytes);
                return Convert.ToBase64String(tokenBytes);
            }
        }

        /// <summary>
        /// Computes SHA-256 hash of input string
        /// </summary>
        /// <param name="input">Input string to hash</param>
        /// <returns>SHA-256 hash as hexadecimal string</returns>
        public static string ComputeSha256Hash(string input)
        {
            if (string.IsNullOrEmpty(input))
                throw new ArgumentException("Input cannot be null or empty", nameof(input));

            using (var sha256 = SHA256.Create())
            {
                var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
        }

        /// <summary>
        /// Validates that a username meets security requirements
        /// </summary>
        /// <param name="username">Username to validate</param>
        /// <returns>Validation result with success flag and error message</returns>
        public static (bool isValid, string errorMessage) ValidateUsername(string? username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return (false, "Username cannot be empty");

            if (username.Length < 3)
                return (false, "Username must be at least 3 characters long");

            if (username.Length > 50)
                return (false, "Username cannot exceed 50 characters");

            // Allow letters, numbers, underscores, and hyphens
            if (!Regex.IsMatch(username, @"^[a-zA-Z0-9_-]+$"))
                return (false, "Username can only contain letters, numbers, underscores, and hyphens");

            // Must start with a letter
            if (!char.IsLetter(username[0]))
                return (false, "Username must start with a letter");

            // Check for reserved words
            var reservedWords = new[] { "admin", "administrator", "root", "system", "user", "guest", "test", "api", "null", "undefined" };
            if (reservedWords.Contains(username.ToLowerInvariant()))
                return (false, "Username cannot be a reserved word");

            return (true, string.Empty);
        }

        /// <summary>
        /// Gets a random character from a string
        /// </summary>
        private static char GetRandomChar(string chars)
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                var buffer = new byte[4];
                rng.GetBytes(buffer);
                var randomValue = BitConverter.ToUInt32(buffer, 0);
                return chars[(int)(randomValue % chars.Length)];
            }
        }

        /// <summary>
        /// Shuffles an array using Fisher-Yates algorithm
        /// </summary>
        private static T[] ShuffleArray<T>(T[] array)
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                for (int i = array.Length - 1; i > 0; i--)
                {
                    var buffer = new byte[4];
                    rng.GetBytes(buffer);
                    var randomIndex = (int)(BitConverter.ToUInt32(buffer, 0) % (i + 1));
                    
                    // Swap elements
                    (array[i], array[randomIndex]) = (array[randomIndex], array[i]);
                }
            }
            return array;
        }
    }
}
