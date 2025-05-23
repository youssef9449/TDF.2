using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using TDFShared.DTOs.Users;
using TDFShared.Exceptions;

namespace TDFShared.Services
{
    /// <summary>
    /// Shared security service providing password hashing, validation, and JWT token operations
    /// Uses modern cryptographic standards and best practices
    /// </summary>
    public class SecurityService : ISecurityService
    {
        // Security constants - using modern recommended values
        private const int PBKDF2_ITERATIONS = 310000; // OWASP recommended minimum for 2024
        private const int SALT_SIZE_BYTES = 32;       // 256 bits
        private const int HASH_SIZE_BYTES = 32;       // 256 bits

        /// <summary>
        /// Hashes a password using PBKDF2 with SHA-512 and generates a new salt
        /// </summary>
        public string HashPassword(string password, out string salt)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Password cannot be null or empty", nameof(password));

            // Generate cryptographically secure salt
            byte[] saltBytes = new byte[SALT_SIZE_BYTES];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(saltBytes);
            }

            // Hash password with salt
            byte[] hashBytes = GetHashBytes(password, saltBytes);

            salt = Convert.ToBase64String(saltBytes);
            return Convert.ToBase64String(hashBytes);
        }

        /// <summary>
        /// Hashes a password using PBKDF2 with SHA-512 and provided salt
        /// </summary>
        public string HashPassword(string password, string salt)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Password cannot be null or empty", nameof(password));
            if (string.IsNullOrEmpty(salt))
                throw new ArgumentException("Salt cannot be null or empty", nameof(salt));

            try
            {
                byte[] saltBytes = Convert.FromBase64String(salt);
                byte[] hashBytes = GetHashBytes(password, saltBytes);
                return Convert.ToBase64String(hashBytes);
            }
            catch (FormatException ex)
            {
                throw new ArgumentException("Invalid salt format", nameof(salt), ex);
            }
        }

        /// <summary>
        /// Verifies a password against stored hash and salt using constant-time comparison
        /// </summary>
        public bool VerifyPassword(string password, string storedHash, string salt)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedHash) || string.IsNullOrEmpty(salt))
                return false;

            try
            {
                byte[] saltBytes = Convert.FromBase64String(salt);
                byte[] storedHashBytes = Convert.FromBase64String(storedHash);
                byte[] computedHashBytes = GetHashBytes(password, saltBytes);

                // Use constant-time comparison to prevent timing attacks
                return CryptographicOperations.FixedTimeEquals(storedHashBytes, computedHashBytes);
            }
            catch (Exception)
            {
                // Any exception during verification should result in failed authentication
                return false;
            }
        }

        /// <summary>
        /// Generates a cryptographically secure salt
        /// </summary>
        public string GenerateSalt()
        {
            byte[] saltBytes = new byte[SALT_SIZE_BYTES];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(saltBytes);
            }
            return Convert.ToBase64String(saltBytes);
        }

        /// <summary>
        /// Validates password strength according to security requirements
        /// </summary>
        public bool IsPasswordStrong(string password, out string validationMessage)
        {
            validationMessage = string.Empty;
            
            if (string.IsNullOrEmpty(password))
            {
                validationMessage = "Password cannot be empty.";
                return false;
            }

            // Password must be at least 8 characters
            if (password.Length < 8)
            {
                validationMessage = "Password must be at least 8 characters long.";
                return false;
            }

            // Password must contain at least one uppercase letter
            if (!password.Any(char.IsUpper))
            {
                validationMessage = "Password must contain at least one uppercase letter.";
                return false;
            }

            // Password must contain at least one lowercase letter
            if (!password.Any(char.IsLower))
            {
                validationMessage = "Password must contain at least one lowercase letter.";
                return false;
            }

            // Password must contain at least one digit
            if (!password.Any(char.IsDigit))
            {
                validationMessage = "Password must contain at least one digit.";
                return false;
            }

            // Password must contain at least one special character
            if (!password.Any(c => !char.IsLetterOrDigit(c)))
            {
                validationMessage = "Password must contain at least one special character (e.g., !@#$).";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validates password strength and throws exception if invalid
        /// </summary>
        public void ValidatePassword(string password)
        {
            if (!IsPasswordStrong(password, out string message))
            {
                throw new ValidationException(message);
            }
        }

        /// <summary>
        /// Generates PBKDF2 hash bytes using SHA-512
        /// </summary>
        private byte[] GetHashBytes(string password, byte[] salt)
        {
            using (var pbkdf2 = new Rfc2898DeriveBytes(
                password, 
                salt, 
                PBKDF2_ITERATIONS, 
                HashAlgorithmName.SHA512))
            {
                return pbkdf2.GetBytes(HASH_SIZE_BYTES);
            }
        }

        /// <summary>
        /// Generates a JWT token for a user (primarily for client-side testing/development)
        /// </summary>
        public string GenerateJwtToken(UserDto user, string secretKey, string issuer, string audience, int expirationMinutes = 60)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            if (string.IsNullOrEmpty(secretKey)) throw new ArgumentException("Secret key cannot be null or empty", nameof(secretKey));
            if (string.IsNullOrEmpty(issuer)) throw new ArgumentException("Issuer cannot be null or empty", nameof(issuer));
            if (string.IsNullOrEmpty(audience)) throw new ArgumentException("Audience cannot be null or empty", nameof(audience));

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(secretKey);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserID.ToString()),
                new Claim(ClaimTypes.Name, user.Username ?? string.Empty),
                new Claim("fullName", user.FullName ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            // Add roles to claims
            if (user.Roles != null)
            {
                claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
            }

            // Add boolean claims for quick access
            if (user.IsAdmin) claims.Add(new Claim("isAdmin", "true"));
            if (user.IsManager) claims.Add(new Claim("isManager", "true"));
            if (user.IsHR) claims.Add(new Claim("isHR", "true"));

            var creds = new SigningCredentials(
                new SymmetricSecurityKey(key), 
                SecurityAlgorithms.HmacSha256Signature);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(expirationMinutes),
                SigningCredentials = creds,
                Issuer = issuer,
                Audience = audience
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        /// <summary>
        /// Validates a JWT token and extracts claims
        /// </summary>
        public (bool isValid, ClaimsPrincipal? principal, string errorReason) ValidateJwtToken(string token, string secretKey, string issuer, string audience)
        {
            if (string.IsNullOrEmpty(token))
                return (false, null, "Token is null or empty");

            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.ASCII.GetBytes(secretKey);

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidIssuer = issuer,
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                var principal = tokenHandler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);
                return (true, principal, string.Empty);
            }
            catch (SecurityTokenExpiredException)
            {
                return (false, null, "Token has expired");
            }
            catch (SecurityTokenInvalidSignatureException)
            {
                return (false, null, "Invalid token signature");
            }
            catch (SecurityTokenException ex)
            {
                return (false, null, $"Token validation failed: {ex.Message}");
            }
            catch (Exception ex)
            {
                return (false, null, $"Unexpected error during token validation: {ex.Message}");
            }
        }

        /// <summary>
        /// Extracts user ID from JWT token claims
        /// </summary>
        public int? GetUserIdFromClaims(ClaimsPrincipal principal)
        {
            if (principal == null) return null;

            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                return userId;
            }

            return null;
        }

        /// <summary>
        /// Extracts user roles from JWT token claims
        /// </summary>
        public IEnumerable<string> GetRolesFromClaims(ClaimsPrincipal principal)
        {
            if (principal == null) return Enumerable.Empty<string>();

            return principal.FindAll(ClaimTypes.Role).Select(c => c.Value);
        }
    }
}
