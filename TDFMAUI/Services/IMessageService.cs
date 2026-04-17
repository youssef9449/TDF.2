using System.Collections.Generic;
using System.Threading.Tasks;
using TDFShared.DTOs.Common;
using TDFShared.DTOs.Messages;

namespace TDFMAUI.Services
{
    public interface IMessageService
    {
        Task<PaginatedResult<MessageDto>> GetUserMessagesAsync(int userId, MessagePaginationDto pagination);
        Task<PaginatedResult<MessageDto>> GetAllMessagesAsync(MessagePaginationDto pagination);
        Task<MessageDto> CreateMessageAsync(MessageCreateDto createDto);
        Task<ChatMessageDto> CreateChatMessageAsync(ChatMessageCreateDto createDto);
        Task<bool> MarkMessageAsReadAsync(int messageId);
        Task<bool> MarkMessagesAsReadAsync(List<int> messageIds);
        Task<List<ChatMessageDto>> GetRecentChatMessagesAsync(int count = 50);
        Task<PaginatedResult<MessageDto>> GetPrivateMessagesAsync(int userId, MessagePaginationDto pagination);
        Task<int> GetUnreadMessagesCountAsync(int userId);
    }
}
