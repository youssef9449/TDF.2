using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using TDFAPI.Repositories;
using TDFShared.DTOs.Auth;
using TDFShared.DTOs.Users;
using TDFAPI.Extensions;

namespace TDFAPI.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IRevokedTokenRepository _revokedTokenRepository;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        
        // Constants for password hashing - using upgraded values
        private const int PBKDF2_ITERATIONS = 310000; // Increased iterations for modern hardware
        private const int SALT_SIZE_BYTES = 32;       // Increased salt size for better security
        private const int HASH_SIZE_BYTES = 32;
        
        // Add account lockout parameters
        private readonly int _maxFailedAttempts;
        private readonly TimeSpan _lockoutDuration;
        
        public AuthService(
            IUserRepository userRepository,
            IRevokedTokenRepository revokedTokenRepository,
            IConfiguration configuration,
            ILogger<AuthService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _userRepository = userRepository;
            _revokedTokenRepository = revokedTokenRepository;
            _configuration = configuration;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            
            // Load lockout settings from configuration
            _maxFailedAttempts = _configuration.GetValue<int>("Security:MaxFailedLoginAttempts", 5);
            var lockoutMinutes = _configuration.GetValue<int>("Security:LockoutDurationMinutes", 15);
            _lockoutDuration = TimeSpan.FromMinutes(lockoutMinutes);
        }
        
        public async Task<TokenResponse?> LoginAsync(string username, string password)
        {
            // Username and password should already be validated by controller
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                _logger.LogWarning("Login attempt with empty username or password");
                return null;
            }
            
            var user = await _userRepository.GetByUsernameAsync(username);
            if (user == null)
            {
                // Don't log username for security reasons to prevent user enumeration
                _logger.LogWarning("Login attempt failed: user not found");
                
                // Use constant time comparison even for non-existent users to prevent timing attacks
                VerifyPassword(password, Convert.ToBase64String(RandomNumberGenerator.GetBytes(HASH_SIZE_BYTES)), 
                    Convert.ToBase64String(RandomNumberGenerator.GetBytes(SALT_SIZE_BYTES)));
                    
                return null;
            }
            
            // Get authentication data for the user
            var userAuth = await _userRepository.GetUserAuthDataAsync(user.UserID);
            if (userAuth == null)
            {
                _logger.LogWarning("Login attempt failed: auth data not found for user ID {UserId}", user.UserID);
                return null;
            }
            
            // Check account lockout status
            if (userAuth.IsLocked && userAuth.LockoutEnd.HasValue && userAuth.LockoutEnd.Value > DateTime.UtcNow)
            {
                var remainingLockoutTime = userAuth.LockoutEnd.Value - DateTime.UtcNow;
                _logger.LogWarning("Login attempt for locked account. User ID: {UserId}, Remaining lockout: {Minutes} minutes", 
                    user.UserID, Math.Ceiling(remainingLockoutTime.TotalMinutes));
                    
                // Still perform password verification to prevent timing attacks
                VerifyPassword(password, userAuth.PasswordHash ?? string.Empty, userAuth.PasswordSalt ?? string.Empty);
                
                return null;
            }
            
            if (string.IsNullOrEmpty(userAuth.PasswordHash) || string.IsNullOrEmpty(userAuth.PasswordSalt))
            {
                _logger.LogWarning("Login attempt failed: password hash or salt is null for user ID {UserId}", user.UserID);
                return null;
            }
            
            if (!VerifyPassword(password, userAuth.PasswordHash, userAuth.PasswordSalt))
            {
                // Record failed login attempt
                int failedAttempts = 1; // Default to 1 failed attempt
                
                // Check if we need to lock the account
                bool isLocked = false;
                DateTime? lockoutEnd = null;
                
                if (failedAttempts >= _maxFailedAttempts)
                {
                    isLocked = true;
                    lockoutEnd = DateTime.UtcNow.Add(_lockoutDuration);
                    _logger.LogWarning("Account locked due to multiple failed attempts. User ID: {UserId}", user.UserID);
                }
                
                // Update user failed attempts and lockout status
                await _userRepository.UpdateLoginAttemptsAsync(
                    user.UserID, 
                    failedAttempts, 
                    isLocked,
                    lockoutEnd);
                    
                // Log minimal info to prevent leaking sensitive data
                _logger.LogWarning("Login attempt failed: invalid password for user ID {UserId}. Failed attempts: {FailedAttempts}", 
                    user.UserID, failedAttempts);
                    
                return null;
            }
            
            // Reset failed login attempts on successful login
            await _userRepository.UpdateLoginAttemptsAsync(
                user.UserID, 
                0, 
                false,
                null);
            
            var tokenExpiryMinutes = Convert.ToDouble(
                _configuration["Jwt:TokenValidityInMinutes"] ?? "60");
                
            var refreshTokenExpiryDays = Convert.ToDouble(
                _configuration["Jwt:RefreshTokenValidityInDays"] ?? "7");
                
            // Generate tokens
            var token = GenerateJwtToken(user);
            var refreshToken = GenerateRefreshToken();
            var tokenExpiration = DateTime.UtcNow.AddMinutes(tokenExpiryMinutes);
            var refreshTokenExpiration = DateTime.UtcNow.AddDays(refreshTokenExpiryDays);
            
            // Save refresh token and login information to database
            var ipAddress = _httpContextAccessor.HttpContext?.GetRealIpAddress() ?? "Unknown";
            
            await _userRepository.UpdateAfterLoginAsync(
                user.UserID, 
                refreshToken, 
                refreshTokenExpiration, 
                DateTime.UtcNow,
                ipAddress);
                    
            _logger.LogInformation("User {UserId} successfully logged in from {IP}", 
                user.UserID, ipAddress);
            
            return new TokenResponse
            {
                Token = token,
                RefreshToken = refreshToken,
                Expiration = tokenExpiration,
                RefreshTokenExpiration = refreshTokenExpiration,
                UserId = user.UserID,
                Username = user.Username,
                FullName = user.FullName,
                IsAdmin = user.IsAdmin,
                RequiresPasswordChange = false // Set a default value
            };
        }
        
        public async Task<TokenResponse?> RefreshTokenAsync(string token, string refreshToken)
        {
            var principal = GetPrincipalFromExpiredToken(token);
            if (principal == null)
            {
                _logger.LogWarning("Invalid access token provided for refresh");
                return null;
            }
            
            // Check if token is actually expired - if not, don't refresh unnecessarily
            var expiryUnixTime = principal.Claims
                .FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Exp)?.Value;
            
            if (!string.IsNullOrEmpty(expiryUnixTime) && long.TryParse(expiryUnixTime, out var expiry))
            {
                var expiryDateTime = DateTimeOffset.FromUnixTimeSeconds(expiry).UtcDateTime;
                if (expiryDateTime > DateTime.UtcNow)
                {
                    _logger.LogInformation("Refresh token requested for non-expired token");
                    // Token is still valid, no need to refresh
                    return null;
                }
            }
            
            var userId = int.TryParse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;
            if (userId == 0)
            {
                _logger.LogWarning("Unable to extract user ID from token");
                return null;
            }
            
            var user = await _userRepository.GetByIdAsync(userId);
            var userAuth = await _userRepository.GetUserAuthDataAsync(userId);
            
            // Check if user exists, refresh token matches, and it's not expired
            if (user == null || userAuth == null || userAuth.RefreshToken != refreshToken || userAuth.RefreshTokenExpiryTime <= DateTime.UtcNow)
            {
                 _logger.LogWarning("Invalid or expired refresh token provided for user ID {UserId}", userId);
                 // Optionally, invalidate the stored token if a mismatch occurs to enhance security
                 // await _userRepository.UpdateRefreshTokenAsync(userId, null, null);
                 return null; 
            }
            
            var tokenExpiryMinutes = Convert.ToDouble(
                _configuration["Jwt:TokenValidityInMinutes"] ?? "60");
                
            var refreshTokenExpiryDays = Convert.ToDouble(
                _configuration["Jwt:RefreshTokenValidityInDays"] ?? "7");
            
            // Generate new tokens
            var newToken = GenerateJwtToken(user);
            var newRefreshToken = GenerateRefreshToken();
            var tokenExpiration = DateTime.UtcNow.AddMinutes(tokenExpiryMinutes);
            var refreshTokenExpiration = DateTime.UtcNow.AddDays(refreshTokenExpiryDays);
            
            // Update refresh token in database
            await _userRepository.UpdateRefreshTokenAsync(user.UserID, newRefreshToken, refreshTokenExpiration);
            
            _logger.LogInformation("Tokens refreshed for user ID {UserId}", user.UserID);
            
            return new TokenResponse
            {
                Token = newToken,
                RefreshToken = newRefreshToken,
                Expiration = tokenExpiration,
                RefreshTokenExpiration = refreshTokenExpiration,
                UserId = user.UserID,
                Username = user.Username,
                FullName = user.FullName,
                IsAdmin = user.IsAdmin,
                RequiresPasswordChange = false // Set default value
            };
        }
        
        public string GenerateJwtToken(UserDto user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = GetJwtSecretKey();
            
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserID.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
            };
            
            if (!string.IsNullOrEmpty(user.FullName))
                claims.Add(new Claim(ClaimTypes.GivenName, user.FullName));
                         
            // Add role claims
            if (user.IsAdmin)
                claims.Add(new Claim(ClaimTypes.Role, "Admin"));
            
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(Convert.ToDouble(_configuration["Jwt:TokenValidityInMinutes"] ?? "60")),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"]
            };
            
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
        
        private ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
        {
            var key = GetJwtSecretKey();
            
            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = false, // Allow expired tokens
                ClockSkew = TimeSpan.Zero
            };
            
            var tokenHandler = new JwtSecurityTokenHandler();
            
            try
            {
                var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out var securityToken);
                
                if (securityToken is not JwtSecurityToken jwtSecurityToken || 
                    !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                {
                    return null;
                }
                
                return principal;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Token validation failed: {Message}", ex.Message);
                return null;
            }
        }
        
        private string GenerateRefreshToken()
        {
            var randomNumber = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }
        
        private byte[] GetJwtSecretKey()
        {
            // Get secret key from environment variable first, fallback to configuration
            var secretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY");
            if (string.IsNullOrWhiteSpace(secretKey))
            {
                secretKey = _configuration["Jwt:SecretKey"];
                if (string.IsNullOrWhiteSpace(secretKey))
                {
                    throw new InvalidOperationException("JWT secret key not configured");
                }
            }
            
            return Encoding.ASCII.GetBytes(secretKey);
        }
        
        // Upgraded to PBKDF2 with configurable iterations
        public string HashPassword(string password, out string salt)
        {
            byte[] saltBytes = new byte[SALT_SIZE_BYTES];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(saltBytes);
            }
            
            byte[] hashBytes = GetHashBytes(password, saltBytes);
            
            salt = Convert.ToBase64String(saltBytes);
            return Convert.ToBase64String(hashBytes);
        }
        
        public bool VerifyPassword(string password, string storedHash, string salt)
        {
            try
            {
                byte[] saltBytes = Convert.FromBase64String(salt);
                byte[] storedHashBytes = Convert.FromBase64String(storedHash);
                byte[] computedHashBytes = GetHashBytes(password, saltBytes);
                
                // Use constant-time comparison to prevent timing attacks
                return CryptographicOperations.FixedTimeEquals(storedHashBytes, computedHashBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying password: {Message}", ex.Message);
                return false;
            }
        }
        
        private byte[] GetHashBytes(string password, byte[] salt)
        {
            // Use PBKDF2 with HMACSHA512 for stronger security
            using (var pbkdf2 = new Rfc2898DeriveBytes(
                password, 
                salt, 
                PBKDF2_ITERATIONS, 
                HashAlgorithmName.SHA512))
            {
                return pbkdf2.GetBytes(HASH_SIZE_BYTES);
            }
        }
        
        // Add new RevokeToken method using the repository
        public async Task RevokeTokenAsync(string jti, DateTime expiryDateUtc)
        {
            await _revokedTokenRepository.AddAsync(jti, expiryDateUtc);
            // Consider triggering the removal of expired tokens periodically via a background service
            // instead of calling it on every revocation.
            // await _revokedTokenRepository.RemoveExpiredAsync(); 
        }

        // Add new IsTokenRevokedAsync method using the repository
        public async Task<bool> IsTokenRevokedAsync(string jti)
        {
            return await _revokedTokenRepository.IsRevokedAsync(jti);
        }
        
        // Add password strength validation
        public static bool IsPasswordStrong(string password)
        {
            // Password must be at least 8 characters
            if (password.Length < 8)
                return false;
            
            // Password must contain at least one uppercase letter
            if (!password.Any(char.IsUpper))
                return false;
            
            // Password must contain at least one lowercase letter
            if (!password.Any(char.IsLower))
                return false;
            
            // Password must contain at least one digit
            if (!password.Any(char.IsDigit))
                return false;
            
            // Password must contain at least one special character
            if (!password.Any(c => !char.IsLetterOrDigit(c)))
                return false;
            
            return true;
        }
    }
}