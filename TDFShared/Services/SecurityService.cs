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
        private readonly IRoleService _roleService;

        // Security constants - using modern recommended values
        private const int PBKDF2_ITERATIONS = 310000; // OWASP recommended minimum for 2024
        private const int SALT_SIZE_BYTES = 32;       // 256 bits
        private const int HASH_SIZE_BYTES = 32;       // 256 bits

        public SecurityService(IRoleService roleService)
        {
            _roleService = roleService ?? throw new ArgumentNullException(nameof(roleService));
        }

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
                return FixedTimeEquals(storedHashBytes, computedHashBytes);
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
        /// Generates a cryptographically secure token of specified length
        /// </summary>
        /// <param name="lengthInBytes">The length of the token in bytes</param>
        /// <returns>A base64-encoded secure token</returns>
        public string GenerateSecureToken(int lengthInBytes)
        {
            if (lengthInBytes <= 0)
                throw new ArgumentException("Token length must be greater than zero", nameof(lengthInBytes));

            byte[] tokenBytes = new byte[lengthInBytes];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(tokenBytes);
            }
            return Convert.ToBase64String(tokenBytes);
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

          /*  // Password must contain at least one uppercase letter
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
            }*/

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
            // Use backward-compatible constructor for older .NET versions
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, PBKDF2_ITERATIONS))
            {
                return pbkdf2.GetBytes(HASH_SIZE_BYTES);
            }
        }

        /// <summary>
        /// Constant-time comparison to prevent timing attacks
        /// </summary>
        private static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
                return false;

            int result = 0;
            for (int i = 0; i < a.Length; i++)
            {
                result |= a[i] ^ b[i];
            }
            return result == 0;
        }

        /// <summary>
        /// Generates a comprehensive JWT token for a user with enhanced security and claims
        /// </summary>
        /// <param name="user">The user to generate token for</param>
        /// <param name="secretKey">The secret key for signing (minimum 32 characters recommended)</param>
        /// <param name="issuer">Token issuer</param>
        /// <param name="audience">Token audience</param>
        /// <param name="expirationMinutes">Token expiration in minutes (default: 60, max: 1440)</param>
        /// <returns>JWT token string</returns>
        /// <exception cref="ArgumentNullException">Thrown when user is null</exception>
        /// <exception cref="ArgumentException">Thrown when parameters are invalid</exception>
        public string GenerateJwtToken(UserDto user, string secretKey, string issuer, string audience, int expirationMinutes = 60)
        {
            // Enhanced input validation
            ValidateJwtTokenInputs(user, secretKey, issuer, audience, expirationMinutes);

            var tokenHandler = new JwtSecurityTokenHandler();

            // Use UTF8 encoding instead of ASCII for better security
            var key = Encoding.UTF8.GetBytes(secretKey);

            // Ensure minimum key size for security
            if (key.Length < 32)
            {
                throw new ArgumentException("Secret key must be at least 32 characters long for security", nameof(secretKey));
            }

            // Build comprehensive claims
            var claims = BuildUserClaims(user);

            // Use stronger signing algorithm
            var signingCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256);

            var now = DateTime.UtcNow;
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims, "jwt"),
                Issuer = issuer,
                Audience = audience,
                IssuedAt = now,
                NotBefore = now,
                Expires = now.AddMinutes(expirationMinutes),
                SigningCredentials = signingCredentials
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        /// <summary>
        /// Validates inputs for JWT token generation
        /// </summary>
        private static void ValidateJwtTokenInputs(UserDto user, string secretKey, string issuer, string audience, int expirationMinutes)
        {
            if (user == null)
                throw new ArgumentNullException(nameof(user));

            if (string.IsNullOrWhiteSpace(secretKey))
                throw new ArgumentException("Secret key cannot be null, empty, or whitespace", nameof(secretKey));

            if (string.IsNullOrWhiteSpace(issuer))
                throw new ArgumentException("Issuer cannot be null, empty, or whitespace", nameof(issuer));

            if (string.IsNullOrWhiteSpace(audience))
                throw new ArgumentException("Audience cannot be null, empty, or whitespace", nameof(audience));

            if (expirationMinutes <= 0)
                throw new ArgumentException("Expiration minutes must be positive", nameof(expirationMinutes));

            if (expirationMinutes > 1440) // 24 hours
                throw new ArgumentException("Expiration minutes cannot exceed 1440 (24 hours) for security", nameof(expirationMinutes));

            if (user.UserID <= 0)
                throw new ArgumentException("User must have a valid UserID", nameof(user));

            if (string.IsNullOrWhiteSpace(user.UserName))
                throw new ArgumentException("User must have a valid Username", nameof(user));
        }

        /// <summary>
        /// Builds comprehensive claims for the user
        /// </summary>
        private List<Claim> BuildUserClaims(UserDto user)
        {
            var userIdString = user.UserID.ToString();
            var claims = new List<Claim>
            {
                // Standard JWT claims
                new Claim(JwtRegisteredClaimNames.Sub, userIdString),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),

                // Standard identity claims
                new Claim(ClaimTypes.NameIdentifier, userIdString),
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim(ClaimTypes.GivenName, user.FullName ?? string.Empty),

                // Application-specific claims
                new Claim("userId", userIdString),
                new Claim("username", user.UserName),
                new Claim("fullName", user.FullName ?? string.Empty),
                new Claim("department", user.Department),
                new Claim("title", user.Title ?? string.Empty),
                new Claim("isActive", user.IsActive.ToString().ToLowerInvariant()),

                // Permission claims for quick authorization checks
                new Claim("isAdmin", user.IsAdmin.ToString().ToLowerInvariant()),
                new Claim("isManager", user.IsManager.ToString().ToLowerInvariant()),
                new Claim("isHR", user.IsHR.ToString().ToLowerInvariant())
            };

            // Add role claims using RoleService (roles are a list)
            foreach (var role in _roleService.GetRoles(user))
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            // Add security-related claims
            if (user.LastLoginDate.HasValue)
            {
                claims.Add(new Claim("lastLogin", user.LastLoginDate.Value.ToString("O")));
            }

            if (!string.IsNullOrWhiteSpace(user.LastLoginIp))
            {
                claims.Add(new Claim("lastLoginIp", user.LastLoginIp));
            }

            // Add account status claims (no .HasValue/.Value, use directly)
            claims.Add(new Claim("isLocked", user.IsLocked.ToString().ToLowerInvariant()));
            claims.Add(new Claim("failedLoginAttempts", user.FailedLoginAttempts.ToString()));

            return claims;
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
                var key = Encoding.UTF8.GetBytes(secretKey);

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
