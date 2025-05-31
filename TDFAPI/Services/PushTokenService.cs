using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TDFAPI.Data;
using TDFAPI.Models;
using TDFShared.DTOs.Users;

namespace TDFAPI.Services
{
    public class PushTokenService : IPushTokenService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PushTokenService> _logger;

        public PushTokenService(
            ApplicationDbContext context,
            ILogger<PushTokenService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task RegisterTokenAsync(int userId, PushTokenRegistrationDto registration)
        {
            try
            {
                // Check if token already exists
                var existingToken = await _context.PushTokens
                    .FirstOrDefaultAsync(t => t.Token == registration.Token);

                if (existingToken != null)
                {
                    // Update existing token
                    existingToken.LastUsedAt = DateTime.UtcNow;
                    existingToken.IsActive = true;
                    existingToken.DeviceName = registration.DeviceName;
                    existingToken.DeviceModel = registration.DeviceModel;
                    existingToken.AppVersion = registration.AppVersion;
                }
                else
                {
                    // Create new token
                    var newToken = new PushToken
                    {
                        UserId = userId,
                        Token = registration.Token,
                        Platform = registration.Platform,
                        CreatedAt = DateTime.UtcNow,
                        LastUsedAt = DateTime.UtcNow,
                        IsActive = true,
                        DeviceName = registration.DeviceName,
                        DeviceModel = registration.DeviceModel,
                        AppVersion = registration.AppVersion
                    };

                    _context.PushTokens.Add(newToken);
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering push token for user {UserId}", userId);
                throw;
            }
        }

        public async Task UnregisterTokenAsync(int userId, string token)
        {
            try
            {
                var pushToken = await _context.PushTokens
                    .FirstOrDefaultAsync(t => t.UserId == userId && t.Token == token);

                if (pushToken != null)
                {
                    pushToken.IsActive = false;
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unregistering push token for user {UserId}", userId);
                throw;
            }
        }

        public async Task<IEnumerable<PushToken>> GetUserTokensAsync(int userId)
        {
            try
            {
                return await _context.PushTokens
                    .Where(t => t.UserId == userId && t.IsActive)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving push tokens for user {UserId}", userId);
                throw;
            }
        }

        public async Task<IDictionary<int, IEnumerable<PushToken>>> GetUsersTokensAsync(IEnumerable<int> userIds)
        {
            try
            {
                var tokens = await _context.PushTokens
                    .Where(t => userIds.Contains(t.UserId) && t.IsActive)
                    .ToListAsync();

                return tokens
                    .GroupBy(t => t.UserId)
                    .ToDictionary(g => g.Key, g => g.AsEnumerable());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving push tokens for users");
                throw;
            }
        }

        public async Task DeactivateAllTokensAsync(int userId)
        {
            try
            {
                var tokens = await _context.PushTokens
                    .Where(t => t.UserId == userId && t.IsActive)
                    .ToListAsync();

                foreach (var token in tokens)
                {
                    token.IsActive = false;
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deactivating tokens for user {UserId}", userId);
                throw;
            }
        }

        public async Task UpdateTokenLastUsedAsync(string token)
        {
            try
            {
                var pushToken = await _context.PushTokens
                    .FirstOrDefaultAsync(t => t.Token == token);

                if (pushToken != null)
                {
                    pushToken.LastUsedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating last used timestamp for token");
                throw;
            }
        }
    }
} 