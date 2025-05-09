using System.Collections.Generic;
using System.Threading.Tasks;
using TDFShared.DTOs.Messages;
using TDFShared.DTOs.Common;
using TDFShared.Models.Message;

namespace TDFAPI.Services
{
    public interface IMessageService
    {
        Task<IEnumerable<MessageDto>> GetAllAsync();
        Task<MessageDto?> GetByIdAsync(int messageId);
        Task<IEnumerable<MessageDto>> GetConversationAsync(int userId1, int userId2);
        
        // Consolidated method with optional pagination
        Task<PaginatedResult<MessageDto>> GetByUserIdAsync(int userId, MessagePaginationDto? pagination = null);
        Task<MessageDto> CreateAsync(MessageCreateDto messageDto, int senderId, string senderName);
        Task<bool> MarkAsReadAsync(int messageId, int userId);
        Task<bool> MarkAsDeliveredAsync(int messageId, int userId);
        Task<bool> DeleteAsync(int messageId, int userId);
        
        // Get undelivered messages from a specific sender to a recipient
        Task<IEnumerable<MessageDto>> GetUndeliveredMessagesAsync(int senderId, int receiverId);

        // Chat messages
        Task<List<ChatMessageDto>> GetRecentChatMessagesAsync(int count = 50);
        Task<ChatMessageDto> CreateChatMessageAsync(ChatMessageCreateDto messageDto, int senderId, string senderName);
        Task<ChatMessageDto?> GetByIdempotencyKeyAsync(string idempotencyKey, int userId);
    }
}
