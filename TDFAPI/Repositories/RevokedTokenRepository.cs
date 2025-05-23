using System;
using System.Threading.Tasks;
using TDFAPI.Data;
using TDFAPI.Domain.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace TDFAPI.Repositories
{
    public class RevokedTokenRepository(ApplicationDbContext context, ILogger<RevokedTokenRepository> logger) : IRevokedTokenRepository
    {
        private readonly ApplicationDbContext _context = context;
        private readonly ILogger<RevokedTokenRepository> _logger = logger;

        public async Task AddAsync(string jti, DateTime expiryDateUtc, int? userId = null)
        {
            if (string.IsNullOrWhiteSpace(jti))
            {
                _logger.LogWarning("Attempted to add a null or empty JTI to revoked tokens.");
                return;
            }

            // Check if already exists to prevent duplicate primary key errors
            var exists = await _context.RevokedTokens.AnyAsync(rt => rt.Jti == jti && rt.ExpiryDate > DateTime.UtcNow);
            if (!exists)
            {
                var revokedToken = new RevokedToken
                {
                    Jti = jti,
                    ExpiryDate = expiryDateUtc,
                    UserId = userId,
                    RevocationDate = DateTime.UtcNow
                };
                await _context.RevokedTokens.AddAsync(revokedToken);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Added token JTI {Jti} for user {UserId} to revocation list.", jti, userId?.ToString() ?? "unknown");
            }
            else
            {
                _logger.LogDebug("Token JTI {Jti} is already in the revocation list.", jti);
            }
        }

        public async Task<bool> IsRevokedAsync(string jti)
        {
            if (string.IsNullOrWhiteSpace(jti))
            {
                return false; // Cannot check an empty JTI
            }

            // Consider caching this check for performance if needed, but direct DB check is simplest
            var isRevoked = await _context.RevokedTokens
                                        .AsNoTracking() // Read-only operation
                                        .AnyAsync(rt => rt.Jti == jti);

            if (isRevoked)
            {
                _logger.LogWarning("Attempt to use revoked token with JTI: {Jti}", jti);
            }
            return isRevoked;
        }

        public async Task RemoveExpiredAsync()
        {
            try
            {
                var now = DateTime.UtcNow;
                var expiredTokens = await _context.RevokedTokens
                                                .Where(rt => rt.ExpiryDate < now)
                                                .ToListAsync();

                if (expiredTokens.Any())
                {
                    _context.RevokedTokens.RemoveRange(expiredTokens);
                    int count = await _context.SaveChangesAsync();
                    _logger.LogInformation("Removed {Count} expired revoked token records.", count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing expired revoked tokens: {Message}", ex.Message);
            }
        }
    }
} 