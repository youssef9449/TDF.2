using System.Collections.Generic;
using System.Threading.Tasks;
using TDFShared.DTOs.Messages;
using TDFShared.DTOs.Common;

namespace TDFAPI.Services
{
    public interface IMessageService
    {
        Task<PaginatedResult<MessageDto>> GetByUserIdAsync(int userId, MessagePaginationDto? pagination = null);
        Task<MessageDto?> GetByIdAsync(int messageId);
        Task<IEnumerable<MessageDto>> GetConversationAsync(int userId1, int userId2);
        Task<MessageDto> CreateAsync(MessageCreateDto messageDto, int senderId, string senderName);
        Task<bool> MarkAsReadAsync(int messageId, int userId);
        Task<bool> MarkAsDeliveredAsync(int messageId, int userId);
        Task<bool> DeleteAsync(int messageId, int userId);
        
        // Chat messages
        Task<List<ChatMessageDto>> GetRecentChatMessagesAsync(int count = 50);
        Task<ChatMessageDto> CreateChatMessageAsync(ChatMessageCreateDto messageDto, int senderId, string senderName);
        Task<ChatMessageDto?> GetByIdempotencyKeyAsync(string idempotencyKey, int userId);
        Task<IEnumerable<MessageDto>> GetUndeliveredMessagesAsync(int senderId, int receiverId);
        Task<int> GetUnreadMessagesCountAsync(int userId);
    }
}
