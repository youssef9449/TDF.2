using TDFShared.Constants;
using TDFShared.DTOs.Common;
using TDFShared.DTOs.Messages;
using TDFShared.Services;

namespace TDFMAUI.Services.Api
{
    public class MessageService : IMessageService
    {
        private readonly IHttpClientService _httpClientService;
        private readonly ILogger<MessageService> _logger;

        public MessageService(IHttpClientService httpClientService, ILogger<MessageService> logger)
        {
            _httpClientService = httpClientService;
            _logger = logger;
        }

        public async Task<PaginatedResult<MessageDto>> GetUserMessagesAsync(int userId, MessagePaginationDto pagination)
        {
            string endpoint = $"{ApiRoutes.Messages.Base}?userId={userId}&pageNumber={pagination.PageNumber}&pageSize={pagination.PageSize}";
            var response = await _httpClientService.GetAsync<ApiResponse<PaginatedResult<MessageDto>>>(endpoint);
            return response?.Data ?? new PaginatedResult<MessageDto>();
        }

        public async Task<PaginatedResult<MessageDto>> GetAllMessagesAsync(MessagePaginationDto pagination)
        {
            string endpoint = $"{ApiRoutes.Messages.Base}?pageNumber={pagination.PageNumber}&pageSize={pagination.PageSize}";
            var response = await _httpClientService.GetAsync<ApiResponse<PaginatedResult<MessageDto>>>(endpoint);
            return response?.Data ?? new PaginatedResult<MessageDto>();
        }

        public async Task<MessageDto> CreateMessageAsync(MessageCreateDto createDto)
        {
            var response = await _httpClientService.PostAsync<MessageCreateDto, ApiResponse<MessageDto>>(ApiRoutes.Messages.Base, createDto);
            return response?.Data!;
        }

        public async Task<ChatMessageDto> CreateChatMessageAsync(ChatMessageCreateDto createDto)
        {
            var response = await _httpClientService.PostAsync<ChatMessageCreateDto, ApiResponse<ChatMessageDto>>(ApiRoutes.Messages.Chat, createDto);
            return response?.Data!;
        }

        public async Task<bool> MarkMessageAsReadAsync(int messageId)
        {
            string endpoint = string.Format(ApiRoutes.Messages.MarkRead, messageId);
            var response = await _httpClientService.PostAsync<object, ApiResponse<bool>>(endpoint, new { });
            return response?.Success ?? false;
        }

        public async Task<bool> MarkMessagesAsReadAsync(List<int> messageIds)
        {
            var response = await _httpClientService.PostAsync<List<int>, ApiResponse<bool>>(ApiRoutes.Messages.MarkBulkRead, messageIds);
            return response?.Success ?? false;
        }

        public async Task<List<ChatMessageDto>> GetRecentChatMessagesAsync(int count = 50)
        {
            string endpoint = $"{ApiRoutes.Messages.RecentChat}?count={count}";
            var response = await _httpClientService.GetAsync<ApiResponse<List<ChatMessageDto>>>(endpoint);
            return response?.Data ?? new List<ChatMessageDto>();
        }

        public async Task<PaginatedResult<MessageDto>> GetPrivateMessagesAsync(int userId, MessagePaginationDto pagination)
        {
            string endpoint = $"{ApiRoutes.Messages.Private}?userId={userId}&pageNumber={pagination.PageNumber}&pageSize={pagination.PageSize}";
            var response = await _httpClientService.GetAsync<ApiResponse<PaginatedResult<MessageDto>>>(endpoint);
            return response?.Data ?? new PaginatedResult<MessageDto>();
        }

        public async Task<int> GetUnreadMessagesCountAsync(int userId)
        {
            string endpoint = string.Format(ApiRoutes.Messages.GetUnreadCount, userId);
            var response = await _httpClientService.GetAsync<ApiResponse<int>>(endpoint);
            return response?.Data ?? 0;
        }
    }
}
