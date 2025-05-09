using System.Data;
using Microsoft.Data.SqlClient;
using TDFShared.Models.Notification;
using TDFAPI.Services;

namespace TDFAPI.Repositories
{
    public class NotificationRepository : INotificationRepository
    {
        private readonly SqlConnectionFactory _connectionFactory;
        private readonly ILogger<NotificationRepository> _logger;

        public NotificationRepository(SqlConnectionFactory connectionFactory, ILogger<NotificationRepository> logger)
        {
            _connectionFactory = connectionFactory;
            _logger = logger;
        }

        public async Task<IEnumerable<NotificationEntity>> GetAllNotificationsAsync()
        {
            var notifications = new List<NotificationEntity>();
            const string sql = "SELECT * FROM Notifications ORDER BY Timestamp DESC";

            using (var connection = await _connectionFactory.CreateConnectionAsync(_logger))
            using (var command = new SqlCommand(sql, connection))
            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    notifications.Add(MapReaderToNotification(reader));
                }
            }

            return notifications;
        }

        public async Task<IEnumerable<NotificationEntity>> GetUnreadNotificationsAsync(int userId)
        {
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

        public async Task<NotificationEntity?> GetNotificationByIdAsync(int id)
        {
            const string sql = "SELECT * FROM Notifications WHERE NotificationID = @NotificationID";

            using (var connection = await _connectionFactory.CreateConnectionAsync(_logger))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@NotificationID", id);
                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return MapReaderToNotification(reader);
                    }
                }
            }

            return null;
        }

        public async Task<int> CreateNotificationAsync(NotificationEntity notification)
        {
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

        public async Task<bool> UpdateNotificationAsync(NotificationEntity notification)
        {
            const string sql = @"
                UPDATE Notifications
                SET ReceiverID = @ReceiverID,
                    SenderID = @SenderID,
                    MessageID = @MessageID,
                    IsSeen = @IsSeen,
                    Timestamp = @Timestamp,
                    MessageText = @MessageText
                WHERE NotificationID = @NotificationID";

            using (var connection = await _connectionFactory.CreateConnectionAsync(_logger))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@NotificationID", notification.NotificationID);
                command.Parameters.AddWithValue("@ReceiverID", notification.ReceiverID);
                command.Parameters.AddWithValue("@SenderID", notification.SenderID);
                command.Parameters.AddWithValue("@MessageID", notification.MessageID);
                command.Parameters.AddWithValue("@IsSeen", notification.IsSeen);
                command.Parameters.AddWithValue("@Timestamp", notification.Timestamp);
                command.Parameters.AddWithValue("@MessageText", notification.Message);

                var rowsAffected = await command.ExecuteNonQueryAsync();
                return rowsAffected > 0;
            }
        }

        public async Task<bool> MarkNotificationAsSeenAsync(int notificationId, int userId)
        {
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

        public async Task<bool> DeleteNotificationAsync(int id)
        {
            const string sql = "DELETE FROM Notifications WHERE NotificationID = @NotificationID";

            using (var connection = await _connectionFactory.CreateConnectionAsync(_logger))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@NotificationID", id);
                var rowsAffected = await command.ExecuteNonQueryAsync();
                return rowsAffected > 0;
            }
        }

        public async Task AddNotificationAsync(int receiverId, string notificationText)
        {
            var notification = new NotificationEntity
            {
                ReceiverID = receiverId,
                MessageText = notificationText,
                Timestamp = DateTime.UtcNow,
                IsSeen = false
            };

            await CreateNotificationAsync(notification);
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