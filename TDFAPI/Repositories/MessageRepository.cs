using System;
using System.Data;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;
using System.Linq;
using System.Transactions;

using TDFAPI.Services;
using Microsoft.Extensions.Logging;
using TDFAPI.Configuration;
using Dapper;
using TDFShared.Models.Message;
using TDFShared.DTOs.Messages;
using TDFShared.DTOs.Common;
using TDFShared.Enums;
using TDFAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace TDFAPI.Repositories
{
    public class MessageRepository : IMessageRepository
    {
        private readonly SqlConnectionFactory _connectionFactory;
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<MessageRepository> _logger;

        public MessageRepository(
            SqlConnectionFactory connectionFactory,
            ApplicationDbContext dbContext,
            ILogger<MessageRepository> logger)
        {
            _connectionFactory = connectionFactory;
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<MessageEntity?> GetByIdAsync(int messageId)
        {
            try
            {
                return await _dbContext.Messages
                    .FirstOrDefaultAsync(m => m.MessageID == messageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving message with ID {MessageId} using EF Core. Falling back to direct SQL.", messageId);

                // Fallback to direct SQL if EF Core fails
                const string sql = "SELECT * FROM Messages WHERE MessageID = @MessageID";

                using var connection = await _connectionFactory.CreateConnectionAsync(_logger);
                using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@MessageID", messageId);

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return MapReaderToMessageEntity(reader);
                }

                return null;
            }
        }

        public async Task<IEnumerable<MessageEntity>> GetAllAsync()
        {
            const string sql = "SELECT * FROM Messages ORDER BY Timestamp DESC";
            var messages = new List<MessageEntity>();

            using var connection = await _connectionFactory.CreateConnectionAsync(_logger);
            using var command = new SqlCommand(sql, connection);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                messages.Add(MapReaderToMessageEntity(reader));
            }

            return messages;
        }

        public async Task<IEnumerable<MessageEntity>> GetByUserIdAsync(int userId)
        {
            const string sql = @"
                SELECT * FROM Messages
                WHERE SenderID = @UserID OR ReceiverID = @UserID
                ORDER BY Timestamp DESC";

            var messages = new List<MessageEntity>();

            using var connection = await _connectionFactory.CreateConnectionAsync(_logger);
            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@UserID", userId);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                messages.Add(MapReaderToMessageEntity(reader));
            }

            return messages;
        }

        public async Task<IEnumerable<MessageEntity>> GetConversationAsync(int userId1, int userId2)
        {
            const string sql = @"
                SELECT * FROM Messages
                WHERE (SenderID = @UserID1 AND ReceiverID = @UserID2)
                   OR (SenderID = @UserID2 AND ReceiverID = @UserID1)
                ORDER BY Timestamp DESC";

            var messages = new List<MessageEntity>();

            using var connection = await _connectionFactory.CreateConnectionAsync(_logger);
            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@UserID1", userId1);
            command.Parameters.AddWithValue("@UserID2", userId2);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                messages.Add(MapReaderToMessageEntity(reader));
            }

            return messages;
        }

        public async Task<PaginatedResult<MessageEntity>> GetByUserIdAsync(int userId, MessagePaginationDto pagination)
        {
            try
            {
                using var connection = await _connectionFactory.CreateConnectionAsync(_logger);
                using var command = connection.CreateCommand();

                var whereClause = "WHERE (m.SenderID = @UserId OR m.ReceiverID = @UserId)";

                if (pagination.UnreadOnly)
                {
                    whereClause += " AND m.IsRead = 0 AND m.ReceiverID = @UserId";
                }

                if (pagination.FromUserId.HasValue)
                {
                    whereClause += " AND ((m.SenderID = @FromUserId AND m.ReceiverID = @UserId) OR (m.SenderID = @UserId AND m.ReceiverID = @FromUserId))";
                }

                if (pagination.StartDate.HasValue)
                {
                    whereClause += " AND m.Timestamp >= @StartDate";
                }

                if (pagination.EndDate.HasValue)
                {
                    whereClause += " AND m.Timestamp <= @EndDate";
                }

                // Get total count first
                command.CommandText = $"SELECT COUNT(*) FROM Messages m {whereClause}";
                command.Parameters.AddWithValue("@UserId", userId);

                if (pagination.FromUserId.HasValue)
                {
                    command.Parameters.AddWithValue("@FromUserId", pagination.FromUserId.Value);
                }

                if (pagination.StartDate.HasValue)
                {
                    command.Parameters.AddWithValue("@StartDate", pagination.StartDate.Value);
                }

                if (pagination.EndDate.HasValue)
                {
                    command.Parameters.AddWithValue("@EndDate", pagination.EndDate.Value);
                }

                var totalCount = (int)await command.ExecuteScalarAsync();

                // Now get paged data
                command.CommandText = $@"
                    SELECT m.*
                    FROM Messages m
                    {whereClause}
                    ORDER BY m.Timestamp DESC
                    OFFSET @Offset ROWS
                    FETCH NEXT @PageSize ROWS ONLY";

                int offset = (pagination.PageNumber - 1) * pagination.PageSize;
                command.Parameters.AddWithValue("@Offset", offset);
                command.Parameters.AddWithValue("@PageSize", pagination.PageSize);

                var reader = await command.ExecuteReaderAsync();
                var messages = new List<MessageEntity>();

                while (await reader.ReadAsync())
                {
                    messages.Add(MapReaderToMessageEntity(reader));
                }

                return new PaginatedResult<MessageEntity>
                {
                    Items = messages,
                    TotalCount = totalCount,
                    PageNumber = pagination.PageNumber,
                    PageSize = pagination.PageSize
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving messages for user {UserId}: {Message}", userId, ex.Message);
                throw;
            }
        }

        public async Task<int> CreateAsync(MessageEntity message)
        {
            try
            {
                _dbContext.Messages.Add(message);
                await _dbContext.SaveChangesAsync();
                return message.MessageID;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating message using EF Core. Falling back to direct SQL.");

                // Fallback to direct SQL if EF Core fails
                const string sql = @"
                    INSERT INTO Messages (SenderID, ReceiverID, MessageText, Timestamp, IsRead, IsDelivered,
                                        Department, MessageType, Status, IsGlobal, IdempotencyKey)
                    VALUES (@SenderID, @ReceiverID, @MessageText, @Timestamp, @IsRead, @IsDelivered,
                           @Department, @MessageType, @Status, @IsGlobal, @IdempotencyKey);
                    SELECT SCOPE_IDENTITY();";

                using var connection = await _connectionFactory.CreateConnectionAsync(_logger);
                using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@SenderID", message.SenderID);
                command.Parameters.AddWithValue("@ReceiverID", message.ReceiverID);
                command.Parameters.AddWithValue("@MessageText", message.MessageText);
                command.Parameters.AddWithValue("@Timestamp", message.Timestamp);
                command.Parameters.AddWithValue("@IsRead", message.IsRead);
                command.Parameters.AddWithValue("@IsDelivered", message.IsDelivered);
                command.Parameters.AddWithValue("@Department", message.Department ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@MessageType", (int)message.MessageType);
                command.Parameters.AddWithValue("@Status", (int)message.Status);
                command.Parameters.AddWithValue("@IsGlobal", message.IsGlobal);
                command.Parameters.AddWithValue("@IdempotencyKey", message.IdempotencyKey ?? (object)DBNull.Value);

                var result = await command.ExecuteScalarAsync();
                return Convert.ToInt32(result);
            }
        }

        public async Task<bool> MarkAsReadAsync(int messageId)
        {
            try
            {
                var message = await _dbContext.Messages.FindAsync(messageId);
                if (message == null)
                    return false;

                message.IsRead = true;
                message.Status = MessageStatus.Read;
                await _dbContext.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking message as read using EF Core. Falling back to direct SQL.");

                // Fallback to direct SQL if EF Core fails
                const string sql = "UPDATE Messages SET IsRead = 1, Status = @Status WHERE MessageID = @MessageID";

                using var connection = await _connectionFactory.CreateConnectionAsync(_logger);
                using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@MessageID", messageId);
                command.Parameters.AddWithValue("@Status", (int)MessageStatus.Read);

                return await command.ExecuteNonQueryAsync() > 0;
            }
        }

        public async Task<bool> UpdateReadStatusAsync(int messageId, bool isRead)
        {
            const string sql = "UPDATE Messages SET IsRead = @IsRead WHERE MessageID = @MessageID";

            using var connection = await _connectionFactory.CreateConnectionAsync(_logger);
            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@MessageID", messageId);
            command.Parameters.AddWithValue("@IsRead", isRead);

            return await command.ExecuteNonQueryAsync() > 0;
        }

        public async Task<bool> DeleteAsync(int messageId)
        {
            try
            {
                var message = await _dbContext.Messages.FindAsync(messageId);
                if (message == null)
                    return false;

                _dbContext.Messages.Remove(message);
                await _dbContext.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting message using EF Core. Falling back to direct SQL.");

                // Fallback to direct SQL if EF Core fails
                const string sql = "DELETE FROM Messages WHERE MessageID = @MessageID";

                using var connection = await _connectionFactory.CreateConnectionAsync(_logger);
                using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@MessageID", messageId);

                return await command.ExecuteNonQueryAsync() > 0;
            }
        }

        public async Task<int> GetUnreadCountAsync(int userId)
        {
            try
            {
                return await _dbContext.Messages
                    .CountAsync(m => m.ReceiverID == userId && !m.IsRead);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting unread count using EF Core. Falling back to direct SQL.");

                // Fallback to direct SQL if EF Core fails
                const string sql = "SELECT COUNT(*) FROM Messages WHERE ReceiverID = @UserID AND IsRead = 0";

                using var connection = await _connectionFactory.CreateConnectionAsync(_logger);
                using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@UserID", userId);

                return Convert.ToInt32(await command.ExecuteScalarAsync());
            }
        }

        public async Task<List<MessageEntity>> GetRecentMessagesAsync(int count = 50)
        {
            try
            {
                return await _dbContext.Messages
                    .OrderByDescending(m => m.Timestamp)
                    .Take(count)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent messages using EF Core. Falling back to direct SQL.");

                // Fallback to direct SQL if EF Core fails
                const string sql = @"
                    SELECT TOP (@Count) * FROM Messages
                    ORDER BY Timestamp DESC";

                var messages = new List<MessageEntity>();

                using var connection = await _connectionFactory.CreateConnectionAsync(_logger);
                using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@Count", count);

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    messages.Add(MapReaderToMessageEntity(reader));
                }

                return messages;
            }
        }

        public async Task<IEnumerable<MessageEntity>> GetPendingMessagesAsync(int userId)
        {
            try
            {
                return await _dbContext.Messages
                    .Where(m => m.ReceiverID == userId && !m.IsDelivered)
                    .OrderBy(m => m.Timestamp)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pending messages using EF Core. Falling back to direct SQL.");

                // Fallback to direct SQL if EF Core fails
                const string sql = @"
                    SELECT * FROM Messages
                    WHERE ReceiverID = @ReceiverID AND IsDelivered = 0
                    ORDER BY Timestamp";

                var messages = new List<MessageEntity>();

                using var connection = await _connectionFactory.CreateConnectionAsync(_logger);
                using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@ReceiverID", userId);

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    messages.Add(MapReaderToMessageEntity(reader));
                }

                return messages;
            }
        }

        public async Task<bool> MarkMessageAsReadAsync(int messageId, int userId)
        {
            const string sql = @"
                UPDATE Messages
                SET IsRead = 1
                WHERE MessageID = @MessageID AND ReceiverID = @UserID";

            using var connection = await _connectionFactory.CreateConnectionAsync(_logger);
            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@MessageID", messageId);
            command.Parameters.AddWithValue("@UserID", userId);

            return await command.ExecuteNonQueryAsync() > 0;
        }

        public async Task<bool> MarkMessageAsDeliveredAsync(int messageId, int userId)
        {
            const string sql = @"
                UPDATE Messages
                SET IsDelivered = 1
                WHERE MessageID = @MessageID AND ReceiverID = @UserID";

            using var connection = await _connectionFactory.CreateConnectionAsync(_logger);
            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@MessageID", messageId);
            command.Parameters.AddWithValue("@UserID", userId);

            return await command.ExecuteNonQueryAsync() > 0;
        }

        public async Task<bool> AddNotificationAsync(int userId, string message)
        {
            const string sql = @"
                INSERT INTO NotificationQueue (UserID, Message, CreatedAt, IsDelivered)
                VALUES (@UserID, @Message, @CreatedAt, 0)";

            using var connection = await _connectionFactory.CreateConnectionAsync(_logger);
            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@UserID", userId);
            command.Parameters.AddWithValue("@Message", message);
            command.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);

            return await command.ExecuteNonQueryAsync() > 0;
        }

        public async Task<IEnumerable<MessageEntity>> GetDepartmentMessagesAsync(string department, int skip = 0, int take = 50)
        {
            const string sql = @"
                SELECT m.* FROM Messages m
                INNER JOIN Users u ON m.SenderID = u.UserID
                WHERE u.Department = @Department
                ORDER BY m.Timestamp DESC
                OFFSET @Skip ROWS
                FETCH NEXT @Take ROWS ONLY";

            var messages = new List<MessageEntity>();
            using (var connection = await _connectionFactory.CreateConnectionAsync(_logger))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Department", department);
                command.Parameters.AddWithValue("@Skip", skip);
                command.Parameters.AddWithValue("@Take", take);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        messages.Add(MapReaderToMessageEntity(reader));
                    }
                }
            }
            return messages;
        }

        public async Task<bool> UpdateUserConnectionStatusAsync(int userId, bool isConnected, string? machineName = null)
        {
            const string sql = @"
                UPDATE Users
                SET isConnected = @IsConnected,
                    MachineName = @MachineName,
                    LastLoginDate = CASE WHEN @IsConnected = 1 THEN GETUTCDATE() ELSE LastLoginDate END
                WHERE UserID = @UserID";

            using (var connection = await _connectionFactory.CreateConnectionAsync(_logger))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@UserID", userId);
                command.Parameters.AddWithValue("@IsConnected", isConnected);
                command.Parameters.AddWithValue("@MachineName", machineName ?? (object)DBNull.Value);

                return await command.ExecuteNonQueryAsync() > 0;
            }
        }

        public async Task<IEnumerable<int>> GetOnlineUsersAsync()
        {
            const string sql = "SELECT UserID FROM Users WHERE isConnected = 1";
            var users = new List<int>();

            using (var connection = await _connectionFactory.CreateConnectionAsync(_logger))
            using (var command = new SqlCommand(sql, connection))
            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    users.Add(reader.GetInt32(0));
                }
            }
            return users;
        }

        public async Task<bool> IsUserOnlineAsync(int userId)
        {
            const string sql = "SELECT isConnected FROM Users WHERE UserID = @UserID";

            using (var connection = await _connectionFactory.CreateConnectionAsync(_logger))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@UserID", userId);
                var result = await command.ExecuteScalarAsync();
                return result != null && (bool)result;
            }
        }

        public async Task<IEnumerable<MessageEntity>> GetMessagesByRoleAsync(string role, int skip = 0, int take = 50)
        {
            const string sql = @"
                SELECT m.* FROM Messages m
                INNER JOIN Users u ON m.SenderID = u.UserID
                WHERE (u.IsAdmin = 1 AND @Role = 'Admin')
                   OR (u.IsManager = 1 AND @Role = 'Manager')
                   OR (u.IsHR = 1 AND @Role = 'HR')
                ORDER BY m.Timestamp DESC
                OFFSET @Skip ROWS
                FETCH NEXT @Take ROWS ONLY";

            var messages = new List<MessageEntity>();
            using (var connection = await _connectionFactory.CreateConnectionAsync(_logger))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Role", role);
                command.Parameters.AddWithValue("@Skip", skip);
                command.Parameters.AddWithValue("@Take", take);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        messages.Add(MapReaderToMessageEntity(reader));
                    }
                }
            }
            return messages;
        }

        public async Task<bool> RegisterDeviceAsync(int userId, string deviceId, string deviceName)
        {
            const string sql = @"
                INSERT INTO UserDevices (UserID, DeviceID, DeviceName, LastSeen)
                VALUES (@UserID, @DeviceID, @DeviceName, GETUTCDATE())
                ON DUPLICATE KEY UPDATE LastSeen = GETUTCDATE(), DeviceName = @DeviceName";

            using (var connection = await _connectionFactory.CreateConnectionAsync(_logger))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@UserID", userId);
                command.Parameters.AddWithValue("@DeviceID", deviceId);
                command.Parameters.AddWithValue("@DeviceName", deviceName);
                return await command.ExecuteNonQueryAsync() > 0;
            }
        }

        public async Task<bool> UnregisterDeviceAsync(int userId, string deviceId)
        {
            const string sql = "DELETE FROM UserDevices WHERE UserID = @UserID AND DeviceID = @DeviceID";

            using (var connection = await _connectionFactory.CreateConnectionAsync(_logger))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@UserID", userId);
                command.Parameters.AddWithValue("@DeviceID", deviceId);
                return await command.ExecuteNonQueryAsync() > 0;
            }
        }

        public async Task<IEnumerable<string>> GetUserDevicesAsync(int userId)
        {
            const string sql = "SELECT DeviceID FROM UserDevices WHERE UserID = @UserID";
            var devices = new List<string>();

            using (var connection = await _connectionFactory.CreateConnectionAsync(_logger))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@UserID", userId);
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        devices.Add(reader.GetString(0));
                    }
                }
            }
            return devices;
        }

        public async Task<bool> SendMessageToRoleAsync(MessageEntity message, string role)
        {
            const string sql = @"
                INSERT INTO Messages (SenderID, ReceiverID, MessageText, Timestamp, IsRead, IsDelivered)
                SELECT @SenderID, u.UserID, @MessageText, @Timestamp, 0, 0
                FROM Users u
                WHERE (u.IsAdmin = 1 AND @Role = 'Admin')
                   OR (u.IsManager = 1 AND @Role = 'Manager')
                   OR (u.IsHR = 1 AND @Role = 'HR')";

            using var connection = await _connectionFactory.CreateConnectionAsync(_logger);
            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@SenderID", message.SenderID);
            command.Parameters.AddWithValue("@ReceiverID", message.ReceiverID);
            command.Parameters.AddWithValue("@MessageText", message.MessageText);
            command.Parameters.AddWithValue("@Timestamp", message.Timestamp);
            command.Parameters.AddWithValue("@Role", role);

            return await command.ExecuteNonQueryAsync() > 0;
        }

        public async Task<int> CreateDepartmentMessageAsync(MessageEntity message, string department)
        {
            const string sql = @"
                INSERT INTO Messages (SenderID, ReceiverID, MessageText, Timestamp, IsRead, IsDelivered)
                SELECT @SenderID, u.UserID, @MessageText, @Timestamp, 0, 0
                FROM Users u
                WHERE u.Department = @Department";

            using var connection = await _connectionFactory.CreateConnectionAsync(_logger);
            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@SenderID", message.SenderID);
            command.Parameters.AddWithValue("@ReceiverID", message.ReceiverID);
            command.Parameters.AddWithValue("@MessageText", message.MessageText);
            command.Parameters.AddWithValue("@Timestamp", message.Timestamp);
            command.Parameters.AddWithValue("@Department", message.Department ?? (object)DBNull.Value);

            return await command.ExecuteNonQueryAsync();
        }

        public async Task<MessageEntity?> GetByIdempotencyKeyAsync(string idempotencyKey, int userId)
        {
            if (string.IsNullOrEmpty(idempotencyKey))
                return null;

            try
            {
                using var connection = await _connectionFactory.CreateConnectionAsync(_logger);

                const string sql = @"
                    SELECT TOP 1 *
                    FROM Messages
                    WHERE IdempotencyKey = @IdempotencyKey
                    AND SenderID = @UserId";

                var message = await connection.QueryFirstOrDefaultAsync<MessageEntity>(
                    sql,
                    new { IdempotencyKey = idempotencyKey, UserId = userId });

                return message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving message by idempotency key {Key}", idempotencyKey);
                return null;
            }
        }

        public async Task<int> UpsertMessageAsync(MessageEntity message)
        {
            try
            {
                using var connection = await _connectionFactory.CreateConnectionAsync(_logger);
                using var command = connection.CreateCommand();

                // Check if the message already exists
                command.CommandText = @"
                    SELECT MessageID FROM Messages
                    WHERE IdempotencyKey = @IdempotencyKey";
                command.Parameters.AddWithValue("@IdempotencyKey", message.IdempotencyKey ?? string.Empty);

                var existingId = await command.ExecuteScalarAsync();

                if (existingId != null && existingId != DBNull.Value)
                {
                    // Update existing message
                    int messageId = Convert.ToInt32(existingId);
                    command.Parameters.Clear();
                    command.CommandText = @"
                        UPDATE Messages SET
                            MessageText = @MessageText,
                            IsRead = @IsRead,
                            IsDelivered = @IsDelivered,
                            Department = @Department,
                            MessageType = @MessageType,
                            Status = @Status,
                            IsGlobal = @IsGlobal
                        WHERE MessageID = @MessageID";

                    command.Parameters.AddWithValue("@MessageID", messageId);
                    command.Parameters.AddWithValue("@MessageText", message.MessageText);
                    command.Parameters.AddWithValue("@IsRead", message.IsRead);
                    command.Parameters.AddWithValue("@IsDelivered", message.IsDelivered);
                    command.Parameters.AddWithValue("@Department", message.Department ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@MessageType", message.MessageType.ToString());
                    command.Parameters.AddWithValue("@Status", (int)message.Status);
                    command.Parameters.AddWithValue("@IsGlobal", message.IsGlobal);

                    await command.ExecuteNonQueryAsync();
                    return messageId;
                }
                else
                {
                    // Insert new message
                    command.Parameters.Clear();
                    command.CommandText = @"
                        INSERT INTO Messages (
                            SenderID, ReceiverID, MessageText, Timestamp,
                            IsRead, IsDelivered, Department, MessageType, Status, IsGlobal, IdempotencyKey
                        ) VALUES (
                            @SenderID, @ReceiverID, @MessageText, @Timestamp,
                            @IsRead, @IsDelivered, @Department, @MessageType, @Status, @IsGlobal, @IdempotencyKey
                        );
                        SELECT SCOPE_IDENTITY();";

                    command.Parameters.AddWithValue("@SenderID", message.SenderID);
                    command.Parameters.AddWithValue("@ReceiverID", message.ReceiverID);
                    command.Parameters.AddWithValue("@MessageText", message.MessageText);
                    command.Parameters.AddWithValue("@Timestamp", message.Timestamp);
                    command.Parameters.AddWithValue("@IsRead", message.IsRead);
                    command.Parameters.AddWithValue("@IsDelivered", message.IsDelivered);
                    command.Parameters.AddWithValue("@Department", message.Department ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@MessageType", message.MessageType.ToString());
                    command.Parameters.AddWithValue("@Status", (int)message.Status);
                    command.Parameters.AddWithValue("@IsGlobal", message.IsGlobal);
                    command.Parameters.AddWithValue("@IdempotencyKey", message.IdempotencyKey ?? (object)DBNull.Value);

                    var result = await command.ExecuteScalarAsync();
                    return Convert.ToInt32(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error upserting message: {Message}", ex.Message);
                throw;
            }
        }

        public async Task<bool> MarkMessagesAsReadBulkAsync(IEnumerable<int> messageIds, int userId)
        {
            try
            {
                if (messageIds == null || !messageIds.Any())
                    return false;

                using var connection = await _connectionFactory.CreateConnectionAsync(_logger);
                using var command = connection.CreateCommand();

                command.CommandText = @"
                    UPDATE Messages
                    SET IsRead = 1, IsDelivered = 1, Status = @Status
                    WHERE MessageID IN @MessageIds AND ReceiverID = @UserId";

                // Convert the IEnumerable to a comma-separated string for SQL
                string messageIdsStr = string.Join(",", messageIds);

                command.Parameters.AddWithValue("@MessageIds", messageIdsStr);
                command.Parameters.AddWithValue("@UserId", userId);
                command.Parameters.AddWithValue("@Status", (int)MessageStatus.Read);

                int rowsAffected = await command.ExecuteNonQueryAsync();
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking messages as read in bulk for user {UserId}: {Message}",
                    userId, ex.Message);
                return false;
            }
        }

        public async Task<bool> MarkMessagesAsDeliveredBulkAsync(IEnumerable<int> messageIds, int userId)
        {
            try
            {
                if (messageIds == null || !messageIds.Any())
                    return false;

                using var connection = await _connectionFactory.CreateConnectionAsync(_logger);
                using var command = connection.CreateCommand();

                command.CommandText = @"
                    UPDATE Messages
                    SET IsDelivered = 1, Status = CASE WHEN IsRead = 1 THEN @ReadStatus ELSE @DeliveredStatus END
                    WHERE MessageID IN @MessageIds AND ReceiverID = @UserId AND IsDelivered = 0";

                // Convert the IEnumerable to a comma-separated string for SQL
                string messageIdsStr = string.Join(",", messageIds);

                command.Parameters.AddWithValue("@MessageIds", messageIdsStr);
                command.Parameters.AddWithValue("@UserId", userId);
                command.Parameters.AddWithValue("@DeliveredStatus", (int)MessageStatus.Delivered);
                command.Parameters.AddWithValue("@ReadStatus", (int)MessageStatus.Read);

                int rowsAffected = await command.ExecuteNonQueryAsync();
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking messages as delivered in bulk for user {UserId}: {Message}",
                    userId, ex.Message);
                return false;
            }
        }

        private MessageEntity MapReaderToMessageEntity(SqlDataReader reader)
        {
            var entity = new MessageEntity
            {
                MessageID = reader.GetInt32(reader.GetOrdinal("MessageID")),
                SenderID = reader.GetInt32(reader.GetOrdinal("SenderID")),
                ReceiverID = reader.GetInt32(reader.GetOrdinal("ReceiverID")),
                MessageText = reader.GetString(reader.GetOrdinal("MessageText")),
                Timestamp = reader.GetDateTime(reader.GetOrdinal("Timestamp")),
                IsRead = reader.GetBoolean(reader.GetOrdinal("IsRead")),
                IsDelivered = reader.GetBoolean(reader.GetOrdinal("IsDelivered"))
            };

            // Check for optional/nullable columns
            if (HasColumn(reader, "Department") && !reader.IsDBNull(reader.GetOrdinal("Department")))
                entity.Department = reader.GetString(reader.GetOrdinal("Department"));

            if (HasColumn(reader, "MessageType"))
                entity.MessageType = (MessageType)reader.GetInt32(reader.GetOrdinal("MessageType"));

            if (HasColumn(reader, "Status"))
                entity.Status = (MessageStatus)reader.GetInt32(reader.GetOrdinal("Status"));

            if (HasColumn(reader, "IsGlobal"))
                entity.IsGlobal = reader.GetBoolean(reader.GetOrdinal("IsGlobal"));

            if (HasColumn(reader, "IdempotencyKey") && !reader.IsDBNull(reader.GetOrdinal("IdempotencyKey")))
                entity.IdempotencyKey = reader.GetString(reader.GetOrdinal("IdempotencyKey"));

            return entity;
        }

        private bool HasColumn(SqlDataReader reader, string columnName)
        {
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (reader.GetName(i).Equals(columnName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}