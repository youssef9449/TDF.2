using System.Collections.Generic;
using System.Threading.Tasks;
using TDFShared.Models.Message;
using TDFShared.DTOs.Messages;
using TDFShared.DTOs.Common;

namespace TDFAPI.Repositories
{
    public interface IMessageRepository
    {
        Task<MessageEntity?> GetByIdAsync(int messageId);
        Task<IEnumerable<MessageEntity>> GetAllAsync();
        Task<IEnumerable<MessageEntity>> GetByUserIdAsync(int userId);
        Task<IEnumerable<MessageEntity>> GetConversationAsync(int userId1, int userId2);
        Task<PaginatedResult<MessageEntity>> GetByUserIdAsync(int userId, MessagePaginationDto pagination);
        Task<int> CreateAsync(MessageEntity message);
        Task<bool> MarkAsReadAsync(int messageId);
        Task<bool> UpdateReadStatusAsync(int messageId, bool isRead);
        Task<bool> DeleteAsync(int messageId);
        Task<int> GetUnreadCountAsync(int userId);
        Task<List<MessageEntity>> GetRecentMessagesAsync(int count = 50);
        Task<IEnumerable<MessageEntity>> GetPendingMessagesAsync(int userId);
        Task<bool> MarkMessageAsReadAsync(int messageId, int userId);
        Task<bool> MarkMessageAsDeliveredAsync(int messageId, int userId);
        Task<bool> MarkMessagesAsReadBulkAsync(IEnumerable<int> messageIds, int userId);
        Task<bool> MarkMessagesAsDeliveredBulkAsync(IEnumerable<int> messageIds, int userId);
        Task<bool> AddNotificationAsync(int userId, string message);

        // Department-based messaging
        Task<IEnumerable<MessageEntity>> GetDepartmentMessagesAsync(string department, int skip = 0, int take = 50);
        Task<int> CreateDepartmentMessageAsync(MessageEntity message, string department);
        
        // Online/Offline status
        Task<bool> UpdateUserConnectionStatusAsync(int userId, bool isConnected, string? machineName = null);
        Task<IEnumerable<int>> GetOnlineUsersAsync();
        Task<bool> IsUserOnlineAsync(int userId);
        
        // Department and Role-based features
        Task<IEnumerable<MessageEntity>> GetMessagesByRoleAsync(string role, int skip = 0, int take = 50);
        Task<bool> SendMessageToRoleAsync(MessageEntity message, string role);
        
        // Multiple device support
        Task<bool> RegisterDeviceAsync(int userId, string deviceId, string deviceName);
        Task<bool> UnregisterDeviceAsync(int userId, string deviceId);
        Task<IEnumerable<string>> GetUserDevicesAsync(int userId);

        Task<MessageEntity?> GetByIdempotencyKeyAsync(string idempotencyKey, int userId);
    }
}