using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TDFAPI.Data;
using TDFAPI.Services;
using TDFShared.DTOs.Common;
using TDFShared.DTOs.Messages;
using TDFShared.Enums;
using TDFShared.Models.Message;

namespace TDFAPI.Repositories
{
    /// <summary>
    /// Data access for the Message aggregate. EF Core is the single stack for the
    /// <c>Messages</c> and <c>Users</c> tables; Dapper is used only for a handful
    /// of leaf tables (<c>NotificationQueue</c>, <c>UserDevices</c>) that have no
    /// mapped entity. There is no silent EF→ADO.NET fallback: EF exceptions
    /// propagate so the caller can react instead of seeing a different code path
    /// execute the same operation against raw SQL.
    /// </summary>
    public sealed class MessageRepository : IMessageRepository
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly SqlConnectionFactory _connectionFactory;
        private readonly ILogger<MessageRepository> _logger;

        public MessageRepository(
            ApplicationDbContext dbContext,
            SqlConnectionFactory connectionFactory,
            ILogger<MessageRepository> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<MessageEntity?> GetByIdAsync(int messageId) =>
            _dbContext.Messages.FirstOrDefaultAsync(m => m.MessageID == messageId);

        public async Task<IEnumerable<MessageEntity>> GetAllAsync() =>
            await _dbContext.Messages
                .OrderByDescending(m => m.Timestamp)
                .ToListAsync();

        public async Task<IEnumerable<MessageEntity>> GetByUserIdAsync(int userId) =>
            await _dbContext.Messages
                .Where(m => m.SenderID == userId || m.ReceiverID == userId)
                .OrderByDescending(m => m.Timestamp)
                .ToListAsync();

        public async Task<IEnumerable<MessageEntity>> GetConversationAsync(int userId1, int userId2) =>
            await _dbContext.Messages
                .Where(m =>
                    (m.SenderID == userId1 && m.ReceiverID == userId2) ||
                    (m.SenderID == userId2 && m.ReceiverID == userId1))
                .OrderByDescending(m => m.Timestamp)
                .ToListAsync();

        public async Task<PaginatedResult<MessageEntity>> GetByUserIdAsync(int userId, MessagePaginationDto pagination)
        {
            var query = _dbContext.Messages
                .Where(m => m.SenderID == userId || m.ReceiverID == userId);

            if (pagination.UnreadOnly)
            {
                query = query.Where(m => !m.IsRead && m.ReceiverID == userId);
            }

            if (pagination.FromUserId.HasValue)
            {
                var fromUserId = pagination.FromUserId.Value;
                query = query.Where(m =>
                    (m.SenderID == fromUserId && m.ReceiverID == userId) ||
                    (m.SenderID == userId && m.ReceiverID == fromUserId));
            }

            if (pagination.StartDate.HasValue)
            {
                var startDate = pagination.StartDate.Value;
                query = query.Where(m => m.Timestamp >= startDate);
            }

            if (pagination.EndDate.HasValue)
            {
                var endDate = pagination.EndDate.Value;
                query = query.Where(m => m.Timestamp <= endDate);
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(m => m.Timestamp)
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToListAsync();

            return new PaginatedResult<MessageEntity>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pagination.PageNumber,
                PageSize = pagination.PageSize
            };
        }

        public async Task<int> CreateAsync(MessageEntity message)
        {
            _dbContext.Messages.Add(message);
            await _dbContext.SaveChangesAsync();
            return message.MessageID;
        }

        public async Task<bool> MarkAsReadAsync(int messageId)
        {
            var message = await _dbContext.Messages.FindAsync(messageId);
            if (message == null)
            {
                return false;
            }

            message.IsRead = true;
            message.Status = MessageStatus.Read;
            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdateReadStatusAsync(int messageId, bool isRead)
        {
            var message = await _dbContext.Messages.FindAsync(messageId);
            if (message == null)
            {
                return false;
            }

            message.IsRead = isRead;
            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int messageId)
        {
            var message = await _dbContext.Messages.FindAsync(messageId);
            if (message == null)
            {
                return false;
            }

            _dbContext.Messages.Remove(message);
            await _dbContext.SaveChangesAsync();
            return true;
        }

        public Task<int> GetUnreadCountAsync(int userId) =>
            _dbContext.Messages.CountAsync(m => m.ReceiverID == userId && !m.IsRead);

        public async Task<List<MessageEntity>> GetRecentMessagesAsync(int count = 50) =>
            await _dbContext.Messages
                .OrderByDescending(m => m.Timestamp)
                .Take(count)
                .ToListAsync();

        public async Task<IEnumerable<MessageEntity>> GetPendingMessagesAsync(int userId) =>
            await _dbContext.Messages
                .Where(m => m.ReceiverID == userId && !m.IsDelivered)
                .OrderBy(m => m.Timestamp)
                .ToListAsync();

        public async Task<bool> MarkMessageAsReadAsync(int messageId, int userId)
        {
            var rows = await _dbContext.Messages
                .Where(m => m.MessageID == messageId && m.ReceiverID == userId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(m => m.IsRead, true));
            return rows > 0;
        }

        public async Task<bool> MarkMessageAsDeliveredAsync(int messageId, int userId)
        {
            var rows = await _dbContext.Messages
                .Where(m => m.MessageID == messageId && m.ReceiverID == userId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(m => m.IsDelivered, true));
            return rows > 0;
        }

        public async Task<bool> MarkMessagesAsReadBulkAsync(IEnumerable<int> messageIds, int userId)
        {
            var idList = messageIds?.ToList();
            if (idList == null || idList.Count == 0)
            {
                return false;
            }

            try
            {
                var rows = await _dbContext.Messages
                    .Where(m => idList.Contains(m.MessageID) && m.ReceiverID == userId)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(m => m.IsRead, true)
                        .SetProperty(m => m.IsDelivered, true)
                        .SetProperty(m => m.Status, MessageStatus.Read));
                return rows > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking messages as read in bulk for user {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> MarkMessagesAsDeliveredBulkAsync(IEnumerable<int> messageIds, int userId)
        {
            var idList = messageIds?.ToList();
            if (idList == null || idList.Count == 0)
            {
                return false;
            }

            try
            {
                var rows = await _dbContext.Messages
                    .Where(m => idList.Contains(m.MessageID) && m.ReceiverID == userId && !m.IsDelivered)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(m => m.IsDelivered, true)
                        .SetProperty(m => m.Status, entity => entity.IsRead ? MessageStatus.Read : MessageStatus.Delivered));
                return rows > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking messages as delivered in bulk for user {UserId}", userId);
                return false;
            }
        }

        public async Task<IEnumerable<MessageEntity>> GetDepartmentMessagesAsync(string department, int skip = 0, int take = 50) =>
            await (from m in _dbContext.Messages
                   join u in _dbContext.Users on m.SenderID equals u.UserID
                   where u.Department == department
                   orderby m.Timestamp descending
                   select m)
                .Skip(skip)
                .Take(take)
                .ToListAsync();

        public async Task<int> CreateDepartmentMessageAsync(MessageEntity message, string department)
        {
            var recipients = await _dbContext.Users
                .Where(u => u.Department == department)
                .Select(u => u.UserID)
                .ToListAsync();

            if (recipients.Count == 0)
            {
                return 0;
            }

            var rows = recipients.Select(receiverId => new MessageEntity
            {
                SenderID = message.SenderID,
                ReceiverID = receiverId,
                MessageText = message.MessageText,
                Timestamp = message.Timestamp,
                IsRead = false,
                IsDelivered = false,
                Department = department,
                MessageType = message.MessageType,
                Status = MessageStatus.Sent,
                IsGlobal = message.IsGlobal,
                IdempotencyKey = message.IdempotencyKey,
            }).ToList();

            _dbContext.Messages.AddRange(rows);
            return await _dbContext.SaveChangesAsync();
        }

        public async Task<IEnumerable<MessageEntity>> GetMessagesByRoleAsync(string role, int skip = 0, int take = 50)
        {
            var query = from m in _dbContext.Messages
                        join u in _dbContext.Users on m.SenderID equals u.UserID
                        where (role == "Admin" && u.IsAdmin == true)
                           || (role == "Manager" && u.IsManager == true)
                           || (role == "HR" && u.IsHR == true)
                        orderby m.Timestamp descending
                        select m;

            return await query.Skip(skip).Take(take).ToListAsync();
        }

        public async Task<bool> SendMessageToRoleAsync(MessageEntity message, string role)
        {
            var recipients = await _dbContext.Users
                .Where(u =>
                    (role == "Admin" && u.IsAdmin == true) ||
                    (role == "Manager" && u.IsManager == true) ||
                    (role == "HR" && u.IsHR == true))
                .Select(u => u.UserID)
                .ToListAsync();

            if (recipients.Count == 0)
            {
                return false;
            }

            _dbContext.Messages.AddRange(recipients.Select(receiverId => new MessageEntity
            {
                SenderID = message.SenderID,
                ReceiverID = receiverId,
                MessageText = message.MessageText,
                Timestamp = message.Timestamp,
                IsRead = false,
                IsDelivered = false,
                MessageType = message.MessageType,
                Status = MessageStatus.Sent,
            }));

            return await _dbContext.SaveChangesAsync() > 0;
        }

        public async Task<bool> UpdateUserConnectionStatusAsync(int userId, bool isConnected, string? machineName = null)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.UserID == userId);
            if (user == null)
            {
                return false;
            }

            user.IsConnected = isConnected;
            user.MachineName = machineName;
            if (isConnected)
            {
                user.LastLoginDate = DateTime.UtcNow;
            }

            return await _dbContext.SaveChangesAsync() > 0;
        }

        public async Task<IEnumerable<int>> GetOnlineUsersAsync() =>
            await _dbContext.Users
                .Where(u => u.IsConnected == true)
                .Select(u => u.UserID)
                .ToListAsync();

        public Task<bool> IsUserOnlineAsync(int userId) =>
            _dbContext.Users.AnyAsync(u => u.UserID == userId && u.IsConnected == true);

        public async Task<MessageEntity?> GetByIdempotencyKeyAsync(string idempotencyKey, int userId)
        {
            if (string.IsNullOrEmpty(idempotencyKey))
            {
                return null;
            }

            return await _dbContext.Messages
                .FirstOrDefaultAsync(m => m.IdempotencyKey == idempotencyKey && m.SenderID == userId);
        }

        public async Task<bool> AddNotificationAsync(int userId, string message)
        {
            const string sql = @"
                INSERT INTO NotificationQueue (UserID, Message, CreatedAt, IsDelivered)
                VALUES (@UserId, @Message, @CreatedAt, 0)";

            using var connection = await _connectionFactory.CreateConnectionAsync(_logger);
            var rows = await connection.ExecuteAsync(sql, new
            {
                UserId = userId,
                Message = message,
                CreatedAt = DateTime.UtcNow
            });

            return rows > 0;
        }

        public async Task<bool> RegisterDeviceAsync(int userId, string deviceId, string deviceName)
        {
            const string sql = @"
                MERGE UserDevices AS target
                USING (SELECT @UserId AS UserID, @DeviceId AS DeviceID) AS source
                ON target.UserID = source.UserID AND target.DeviceID = source.DeviceID
                WHEN MATCHED THEN
                    UPDATE SET DeviceName = @DeviceName, LastSeen = SYSUTCDATETIME()
                WHEN NOT MATCHED THEN
                    INSERT (UserID, DeviceID, DeviceName, LastSeen)
                    VALUES (@UserId, @DeviceId, @DeviceName, SYSUTCDATETIME());";

            using var connection = await _connectionFactory.CreateConnectionAsync(_logger);
            var rows = await connection.ExecuteAsync(sql, new
            {
                UserId = userId,
                DeviceId = deviceId,
                DeviceName = deviceName
            });

            return rows > 0;
        }

        public async Task<bool> UnregisterDeviceAsync(int userId, string deviceId)
        {
            const string sql = "DELETE FROM UserDevices WHERE UserID = @UserId AND DeviceID = @DeviceId";

            using var connection = await _connectionFactory.CreateConnectionAsync(_logger);
            var rows = await connection.ExecuteAsync(sql, new { UserId = userId, DeviceId = deviceId });
            return rows > 0;
        }

        public async Task<IEnumerable<string>> GetUserDevicesAsync(int userId)
        {
            const string sql = "SELECT DeviceID FROM UserDevices WHERE UserID = @UserId";

            using var connection = await _connectionFactory.CreateConnectionAsync(_logger);
            return await connection.QueryAsync<string>(sql, new { UserId = userId });
        }
    }
}
