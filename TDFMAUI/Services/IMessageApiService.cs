using System.Collections.Generic;
using System.Threading.Tasks;
using TDFShared.DTOs.Common;
using TDFShared.DTOs.Messages;

namespace TDFMAUI.Services
{
    public interface IMessageApiService
    {
        Task<PaginatedResult<ChatMessageDto>> GetUserMessagesAsync(int userId, MessagePaginationDto pagination);
        Task<PaginatedResult<ChatMessageDto>> GetAllMessagesAsync(MessagePaginationDto pagination);
        Task<ChatMessageDto> CreateMessageAsync(MessageCreateDto createDto);
        Task<ChatMessageDto> CreatePrivateMessageAsync(MessageCreateDto createDto);
        Task<bool> MarkMessageAsReadAsync(int messageId);
        Task<bool> MarkMessagesAsReadAsync(List<int> messageIds);
        Task<List<ChatMessageDto>> GetRecentChatMessagesAsync(int count = 50);
        Task<PaginatedResult<ChatMessageDto>> GetPrivateMessagesAsync(int userId, MessagePaginationDto pagination);
        Task<int> GetUnreadMessagesCountAsync(int userId);
    }
}
