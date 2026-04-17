using System.Data;
using Microsoft.Data.SqlClient;
using TDFShared.Models.Notification;
using TDFAPI.Services;
using TDFAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace TDFAPI.Repositories
{
    public class NotificationRepository : INotificationRepository
    {
        private readonly SqlConnectionFactory _connectionFactory;
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<NotificationRepository> _logger;

        public NotificationRepository(
            SqlConnectionFactory connectionFactory,
            ApplicationDbContext dbContext,
            ILogger<NotificationRepository> logger)
        {
            _connectionFactory = connectionFactory;
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<IEnumerable<NotificationEntity>> GetUnreadNotificationsAsync(int userId)
        {
            try
            {
                return await _dbContext.Notifications
                    .Where(n => n.ReceiverID == userId && !n.IsSeen)
                    .OrderByDescending(n => n.Timestamp)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving unread notifications for user {UserId}", userId);

                const string sql = @"
                    SELECT * FROM Notifications
                    WHERE ReceiverID = @UserID AND IsSeen = 0
                    ORDER BY Timestamp DESC;";

                var notifications = new List<NotificationEntity>();

                using (var connection = await _connectionFactory.CreateConnectionAsync(_logger))
                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@UserID", userId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            notifications.Add(MapReaderToNotification(reader));
                        }
                    }
                }

                return notifications;
            }
        }

        public async Task<int> CreateNotificationAsync(NotificationEntity notification)
        {
            try
            {
                _dbContext.Notifications.Add(notification);
                await _dbContext.SaveChangesAsync();
                return notification.NotificationID;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating notification using EF Core. Falling back to direct SQL.");

                const string sql = @"
                    INSERT INTO Notifications (ReceiverID, SenderID, MessageID, IsSeen, Timestamp, MessageText)
                    VALUES (@ReceiverID, @SenderID, @MessageID, @IsSeen, @Timestamp, @MessageText);
                    SELECT SCOPE_IDENTITY();";

                using (var connection = await _connectionFactory.CreateConnectionAsync(_logger))
                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@ReceiverID", notification.ReceiverID);
                    command.Parameters.AddWithValue("@SenderID", notification.SenderID != null ? notification.SenderID : (object)DBNull.Value);
                    command.Parameters.AddWithValue("@MessageID", notification.MessageID.HasValue ? (object)notification.MessageID.Value : DBNull.Value);
                    command.Parameters.AddWithValue("@IsSeen", notification.IsSeen);
                    command.Parameters.AddWithValue("@Timestamp", notification.Timestamp);
                    command.Parameters.AddWithValue("@MessageText", (object?)notification.Message ?? DBNull.Value);

                    var result = await command.ExecuteScalarAsync();
                    return Convert.ToInt32(result);
                }
            }
        }

        public async Task<bool> MarkNotificationAsSeenAsync(int notificationId, int userId)
        {
            try
            {
                var notification = await _dbContext.Notifications
                    .FirstOrDefaultAsync(n => n.NotificationID == notificationId && n.ReceiverID == userId);

                if (notification == null)
                    return false;

                notification.IsSeen = true;
                await _dbContext.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking notification as seen using EF Core. Falling back to direct SQL.");

                const string sql = @"
                    UPDATE Notifications
                    SET IsSeen = 1
                    WHERE NotificationID = @NotificationID AND ReceiverID = @UserID;";

                using (var connection = await _connectionFactory.CreateConnectionAsync(_logger))
                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@NotificationID", notificationId);
                    command.Parameters.AddWithValue("@UserID", userId);

                    return await command.ExecuteNonQueryAsync() > 0;
                }
            }
        }

        public async Task<bool> DeleteNotificationAsync(int notificationId, int userId)
        {
            try
            {
                var notification = await _dbContext.Notifications
                    .FirstOrDefaultAsync(n => n.NotificationID == notificationId && n.ReceiverID == userId);
                if (notification == null)
                    return false;

                _dbContext.Notifications.Remove(notification);
                await _dbContext.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting notification using EF Core. Falling back to direct SQL.");

                const string sql = "DELETE FROM Notifications WHERE NotificationID = @NotificationID AND ReceiverID = @UserID";

                using (var connection = await _connectionFactory.CreateConnectionAsync(_logger))
                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@NotificationID", notificationId);
                    command.Parameters.AddWithValue("@UserID", userId);
                    var rowsAffected = await command.ExecuteNonQueryAsync();
                    return rowsAffected > 0;
                }
            }
        }

        private NotificationEntity MapReaderToNotification(SqlDataReader reader)
        {
            return new NotificationEntity
            {
                NotificationID = reader.GetInt32(reader.GetOrdinal("NotificationID")),
                ReceiverID = reader.GetInt32(reader.GetOrdinal("ReceiverID")),
                SenderID = reader.IsDBNull(reader.GetOrdinal("SenderID")) ? null : reader.GetInt32(reader.GetOrdinal("SenderID")),
                MessageID = reader.IsDBNull(reader.GetOrdinal("MessageID")) ? null : reader.GetInt32(reader.GetOrdinal("MessageID")),
                IsSeen = reader.GetBoolean(reader.GetOrdinal("IsSeen")),
                Timestamp = reader.GetDateTime(reader.GetOrdinal("Timestamp")),
                MessageText = reader.IsDBNull(reader.GetOrdinal("MessageText")) ? null : reader.GetString(reader.GetOrdinal("MessageText"))
            };
        }
    }
}
