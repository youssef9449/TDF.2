using Microsoft.Data.SqlClient;
using TDFShared.DTOs.Common;
using TDFShared.DTOs.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using TDFShared.Enums;
using Microsoft.EntityFrameworkCore;
using TDFAPI.Data;
using TDFShared.Models.User;
using TDFShared.Models.Request;
using TDFAPI.Services;
using TDFShared.Services;

namespace TDFAPI.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly ILogger<UserRepository> _logger;
        private readonly ApplicationDbContext _context;
        private readonly IRoleService _roleService;
        
        public UserRepository(ILogger<UserRepository> logger, ApplicationDbContext context, IRoleService roleService)
        {
            _logger = logger;
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _roleService = roleService ?? throw new ArgumentNullException(nameof(roleService));
        }
        
        public async Task<UserDto?> GetByIdAsync(int userId)
        {
            try
            {
                var user = await _context.Users
                                       .Include(u => u.AnnualLeave)
                                       .AsNoTracking()
                                       .FirstOrDefaultAsync(u => u.UserID == userId);
                return user != null ? MapUserDtoFromEntity(user) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user by ID {UserId}: {Message}", userId, ex.Message);
                throw;
            }
        }
        
        public async Task<UserDto?> GetByUsernameAsync(string username)
        {
            try
            {
                var user = await _context.Users
                                       .Include(u => u.AnnualLeave)
                                       .AsNoTracking()
                                       .FirstOrDefaultAsync(u => u.UserName == username);
                return user != null ? MapUserDtoFromEntity(user) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user by username {Username}: {Message}", username, ex.Message);
                throw;
            }
        }

        public async Task<List<UserDto>> GetAllAsync()
        {
            try
            {
                var users = await _context.Users
                                        .Include(u => u.AnnualLeave)
                                        .AsNoTracking()
                                        .OrderBy(u => u.UserName)
                                        .ToListAsync();
                return users.Select(MapUserDtoFromEntity).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all users: {Message}", ex.Message);
                throw;
            }
        }

        public async Task<PaginatedResult<UserDto>> GetPaginatedAsync(int page, int pageSize)
        {
            try
            {
                var totalCount = await _context.Users.CountAsync();
                var users = await _context.Users
                                        .Include(u => u.AnnualLeave)
                                        .AsNoTracking()
                                        .OrderBy(u => u.UserName)
                                        .Skip((page - 1) * pageSize)
                                        .Take(pageSize)
                                        .ToListAsync();
                var userDtos = users.Select(MapUserDtoFromEntity).ToList();
                return new PaginatedResult<UserDto>(userDtos, page, pageSize, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting paginated users: {Message}", ex.Message);
                throw;
            }
        }

        public async Task<int> CreateAsync(CreateUserRequest userDto, string passwordHash, string salt)
        {
            var newUser = new UserEntity
            {
                UserName = userDto.Username,
                FullName = userDto.FullName,
                Department = userDto.Department,
                Title = userDto.Title,
                PasswordHash = passwordHash,
                Salt = salt,
                IsAdmin = userDto.IsAdmin,
                IsManager = userDto.IsManager,
                IsHR = false,
                CreatedAt = DateTime.UtcNow,
                IsActive = false,
                IsConnected = false,
                PresenceStatus = UserPresenceStatus.Offline,
                IsAvailableForChat = false,
                FailedLoginAttempts = 0,
                IsLocked = false
            };

            var annualLeave = new AnnualLeaveEntity
            {
                FullName = newUser.FullName,
                Annual = 15,
                EmergencyLeave = 6,
                Permissions = 24,
                AnnualUsed = 0,
                EmergencyUsed = 0,
                PermissionsUsed = 0,
                UnpaidUsed = 0,
                WorkFromHomeUsed = 0  // Explicitly set this property
            };
            newUser.AnnualLeave = annualLeave;

            try
            {
                _context.Users.Add(newUser);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Created new user with ID {UserId}", newUser.UserID);
                return newUser.UserID;
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error creating user {Username}: {Message}", userDto.Username, dbEx.InnerException?.Message ?? dbEx.Message);
                if (dbEx.InnerException is SqlException sqlEx && (sqlEx.Number == 2627 || sqlEx.Number == 2601))
                {
                    throw new InvalidOperationException($"Username '{userDto.Username}' already exists.", dbEx);
                }
                throw new InvalidOperationException("Failed to save user data to the database.", dbEx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "General error creating user {Username}: {Message}", userDto.Username, ex.Message);
                throw;
            }
        }

        public async Task<bool> UpdateAsync(int userId, UpdateUserRequest userDto)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User not found for update: ID {UserId}", userId);
                    return false;
                }
                
                user.FullName = userDto.FullName ?? user.FullName;
                user.Department = userDto.Department ?? user.Department;
                user.Title = userDto.Title ?? user.Title;
                user.IsAdmin = userDto.IsAdmin;
                user.IsManager = userDto.IsManager;
                user.IsHR = userDto.IsHR;
                user.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                _logger.LogInformation("User updated: ID {UserId}", userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user with ID {UserId}: {Message}", userId, ex.Message);
                throw;
            }
        }

        public async Task<UserAuthData?> GetUserAuthDataAsync(int userId)
        {
            try
            {
                return await _context.Users
                    .Where(u => u.UserID == userId)
                    .Select(u => new UserAuthData
                    {
                        UserId = u.UserID,
                        PasswordHash = u.PasswordHash,
                        PasswordSalt = u.Salt,
                        IsLocked = u.IsLocked ?? false,
                        LockoutEnd = u.LockoutEndTime,
                        RefreshToken = u.RefreshToken ?? string.Empty,
                        RefreshTokenExpiryTime = u.RefreshTokenExpiryTime
                    })
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user auth data for ID {UserId}: {Message}", userId, ex.Message);
                throw;
            }
        }

        public async Task<bool> DeleteAsync(int userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User not found for deletion: ID {UserId}", userId);
                        return false;
                    }
                    
                _context.Users.Remove(user);
                await _context.SaveChangesAsync();
                _logger.LogInformation("User deleted: ID {UserId}", userId);
                    return true;
                }
            catch (DbUpdateException dbEx) when (dbEx.InnerException is SqlException sqlEx && sqlEx.Number == 547)
            {
                _logger.LogError(dbEx, "Foreign key constraint prevented deletion of user {UserId}", userId);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user with ID {UserId}: {Message}", userId, ex.Message);
                throw;
            }
        }
        
        public async Task<bool> ChangePasswordAsync(int userId, string passwordHash, string salt)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User not found for password change: ID {UserId}", userId);
                    return false;
                }

                user.PasswordHash = passwordHash;
                user.Salt = salt;
                user.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                _logger.LogInformation("User password changed: ID {UserId}", userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password for user with ID {UserId}: {Message}", userId, ex.Message);
                throw;
            }
        }

        public async Task<bool> UpdateRefreshTokenAsync(int userId, string? refreshToken, DateTime refreshTokenExpiryTime)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User not found for refresh token update: ID {UserId}", userId);
                    return false;
                }
                
                user.RefreshToken = refreshToken;
                user.RefreshTokenExpiryTime = refreshTokenExpiryTime;
                user.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                _logger.LogInformation("User refresh token updated: ID {UserId}", userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating refresh token for user ID {UserId}: {Message}", userId, ex.Message);
                throw;
            }
        }
        
        public async Task<bool> UpdateLoginAttemptsAsync(int userId, int failedAttempts, bool isLocked, DateTime? lockoutEnd)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User not found for login attempt update: ID {UserId}", userId);
                    return false;
                }

                user.FailedLoginAttempts = failedAttempts;
                user.IsLocked = isLocked;
                user.LockoutEndTime = lockoutEnd;
                user.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating login attempts for user {UserId}: {Message}", userId, ex.Message);
                throw;
            }
        }

        public async Task<bool> UpdateAfterLoginAsync(int userId, string refreshToken, DateTime refreshTokenExpiryTime, DateTime lastLoginDate, string lastLoginIp)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User not found for post-login update: ID {UserId}", userId);
                    return false;
                }

                user.RefreshToken = refreshToken;
                user.RefreshTokenExpiryTime = refreshTokenExpiryTime;
                user.LastLoginDate = lastLoginDate;
                user.LastLoginIp = lastLoginIp;
                user.FailedLoginAttempts = 0;
                user.IsLocked = false;
                user.LockoutEndTime = null;
                user.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating after login for user {UserId}: {Message}", userId, ex.Message);
                throw;
            }
        }
        
        public async Task<bool> UpdateSelfAsync(int userId, UpdateMyProfileRequest dto)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User not found for self-update: ID {UserId}", userId);
                    return false;
                }

                user.FullName = dto.FullName ?? user.FullName;
                user.StatusMessage = dto.StatusMessage ?? user.StatusMessage;
                user.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                _logger.LogInformation("User self-updated: ID {UserId}", userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during self-update for user ID {UserId}: {Message}", userId, ex.Message);
                throw;
            }
        }

        public async Task<bool> UpdateProfilePictureAsync(int userId, byte[] pictureData)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User not found for profile picture update: ID {UserId}", userId);
            return false;
        }

                user.Picture = pictureData;
                user.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                _logger.LogInformation("User profile picture updated: ID {UserId}", userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile image for user {UserId}: {Message}", userId, ex.Message);
                throw;
            }
        }

        public async Task<bool> UpdatePresenceStatusAsync(int userId, UserPresenceStatus status, string? statusMessage = null)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null) return false;

                user.PresenceStatus = status;
                user.IsConnected = (status != UserPresenceStatus.Offline);
                if (statusMessage != null)
                {
                    user.StatusMessage = statusMessage.Length > 255 ? statusMessage.Substring(0, 255) : statusMessage;
                }
                user.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating presence status for user {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> UpdateLastActivityAsync(int userId, DateTime activityTime)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null) return false;

                user.LastActivityTime = activityTime;
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating last activity for user {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> UpdateCurrentDeviceAsync(int userId, string device, string machineName)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null) return false;

                user.CurrentDevice = device?.Length > 100 ? device.Substring(0, 100) : device;
                user.MachineName = machineName?.Length > 100 ? machineName.Substring(0, 100) : machineName;
                user.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating current device for user {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> SetAvailabilityForChatAsync(int userId, bool isAvailable)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null) return false;

                user.IsAvailableForChat = isAvailable;
                user.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting chat availability for user {UserId}", userId);
                return false;
            }
        }

        public async Task<List<UserDto>> GetUsersByDepartmentAsync(string department)
        {
            try
            {
                var users = await _context.Users
                                        .Include(u => u.AnnualLeave)
                                        .Where(u => u.Department == department)
                                        .AsNoTracking()
                                        .OrderBy(u => u.UserName)
                                        .ToListAsync();
                return users.Select(MapUserDtoFromEntity).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users for department {Department}", department);
                throw;
            }
        }

        public async Task<List<UserDto>> GetUsersByIdsAsync(IEnumerable<int> userIds)
        {
            try
            {
                if (userIds == null || !userIds.Any()) return new List<UserDto>();

                var users = await _context.Users
                                        .Include(u => u.AnnualLeave)
                                        .Where(u => userIds.Contains(u.UserID))
                                        .AsNoTracking()
                                        .ToListAsync();
                return users.Select(MapUserDtoFromEntity).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users by IDs");
                throw;
            }
        }

        public async Task<List<UserDto>> GetOnlineUsersAsync()
        {
            try
            {
                var users = await _context.Users
                                        .Include(u => u.AnnualLeave)
                                        .Where(u => u.IsConnected == true)
                                        .AsNoTracking()
                                        .OrderBy(u => u.UserName)
                                        .ToListAsync();

                return users.Select(MapUserDtoFromEntity).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting online users");
                throw;
            }
        }

        public async Task<List<UserDto>> GetByDepartmentAndRoleAsync(string department)
        {
            try
            {
                var users = await _context.Users
                                        .Include(u => u.AnnualLeave)
                                        .Where(u => u.Department == department && (u.IsAdmin == true || u.IsManager == true || u.IsHR == true))
                                        .AsNoTracking()
                                        .OrderBy(u => u.UserName)
                                        .ToListAsync();
                return users.Select(MapUserDtoFromEntity).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users for department {Department} by role flags", department);
                throw;
            }
        }

        public async Task<List<UserDto>> GetUsersByRoleAsync(string role)
        {
            try
            {
                IQueryable<UserEntity> query = _context.Users.Include(u => u.AnnualLeave).AsNoTracking();

                if (role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(u => u.IsAdmin == true);
                else if (role.Equals("Manager", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(u => u.IsManager == true);
                else if (role.Equals("HR", StringComparison.OrdinalIgnoreCase))
                     query = query.Where(u => u.IsHR == true);
                else
                    query = query.Where(u => u.IsAdmin != true && u.IsManager != true && u.IsHR != true);

                var users = await query.OrderBy(u => u.UserName).ToListAsync();
                return users.Select(MapUserDtoFromEntity).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users by role flag {Role}", role);
                throw;
            }
        }

        public async Task<bool> IsKnownIpAddressAsync(int userId, string ipAddress)
        {
            _logger.LogWarning("IsKnownIpAddressAsync requires UserLoginHistory table/entity to be mapped and queried.");
            return await Task.FromResult(false);
        }

        public async Task<bool> UpdateStatusAsync(int userId, UpdateUserStatusRequest request)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User not found for status update: ID {UserId}", userId);
                    return false;
                }
                
                if (request.PresenceStatus.HasValue)
                {
                    user.PresenceStatus = request.PresenceStatus.Value;
                }
                
                if (request.StatusMessage != null)
                {
                    user.StatusMessage = request.StatusMessage;
                }
                
                user.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                _logger.LogInformation("User status updated: ID {UserId}, Status {Status}", userId, request.PresenceStatus);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating status for user with ID {UserId}: {Message}", userId, ex.Message);
                throw;
            }
        }

        public async Task<bool> UpdateAvailabilityAsync(int userId, UpdateUserStatusRequest request)
        {
            try
            {
                if (!request.IsAvailableForChat.HasValue)
                {
                    _logger.LogWarning("IsAvailableForChat value is required for availability update: ID {UserId}", userId);
                    return false;
                }
                
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User not found for availability update: ID {UserId}", userId);
                    return false;
                }
                
                user.IsAvailableForChat = request.IsAvailableForChat.Value;
                user.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                _logger.LogInformation("User availability updated: ID {UserId}, Available {IsAvailable}", userId, request.IsAvailableForChat.Value);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating availability for user with ID {UserId}: {Message}", userId, ex.Message);
                throw;
            }
        }

        public async Task UpdateUserDeviceInfoAsync(int userId, string deviceId, string userAgent)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("Attempted to update device info for non-existent user {UserId}", userId);
                    return;
                }

                // Update device info
                user.CurrentDevice = deviceId;
                user.MachineName = userAgent;
                user.LastLoginDate = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                _logger.LogInformation("Updated device info for user {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating device info for user {UserId}", ex.Message);
                throw;
            }
        }

        public async Task<bool> IsFullNameTakenAsync(string fullName, int? excludeUserId = null)
        {
            try
            {
                var query = _context.Users.Where(u => u.FullName == fullName);
                
                if (excludeUserId.HasValue)
                {
                    query = query.Where(u => u.UserID != excludeUserId.Value);
                }
                
                return await query.AnyAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if full name {FullName} is taken", fullName);
                throw;
            }
        }

        private UserDto MapUserDtoFromEntity(UserEntity entity)
        {
            var dto = new UserDto
            {
                UserID = entity.UserID,
                UserName = entity.UserName,
                FullName = entity.FullName,
                Department = entity.Department,
                Title = entity.Title,
                IsActive = entity.IsActive,
                IsAdmin = entity.IsAdmin,
                IsManager = entity.IsManager,
                IsHR = entity.IsHR,
                LastLoginDate = entity.LastLoginDate,
                LastLoginIp = entity.LastLoginIp,
                IsLocked = entity.IsLocked,
                FailedLoginAttempts = entity.FailedLoginAttempts,
                Roles = new List<string>()
            };

            // Assign roles using RoleService
            _roleService.AssignRoles(dto);
            
            return dto;
        }
    }
} 