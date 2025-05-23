using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TDFShared.Constants;
using TDFShared.DTOs.Common;
using TDFShared.DTOs.Messages;
using TDFShared.Models.Message;
using TDFShared.Services;

namespace TDFMAUI.Services
{
    public class MessageService
    {
        private readonly IHttpClientService _httpClientService;
        private readonly ILogger<MessageService> _logger;

        public MessageService(IHttpClientService httpClientService, ILogger<MessageService> logger)
        {
            _httpClientService = httpClientService ?? throw new ArgumentNullException(nameof(httpClientService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<List<MessageDto>> GetAllMessagesAsync()
        {
            try
            {
                _logger.LogInformation("Getting all messages");
                var response = await _httpClientService.GetAsync<ApiResponse<List<MessageDto>>>(ApiRoutes.Messages.Base);
                return response?.Data ?? new List<MessageDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all messages");
                return new List<MessageDto>();
            }
        }

        public async Task<MessageDto> GetMessageByIdAsync(int id)
        {
            try
            {
                _logger.LogInformation("Getting message with ID {MessageId}", id);
                var uri = string.Format(ApiRoutes.Messages.GetById, id);
                var response = await _httpClientService.GetAsync<ApiResponse<MessageDto>>(uri);
                return response?.Data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting message with ID {MessageId}", id);
                return null;
            }
        }

        public async Task<bool> CreateMessageAsync(MessageCreateDto message)
        {
            try
            {
                _logger.LogInformation("Creating new message");
                await _httpClientService.PostAsync(ApiRoutes.Messages.Base, message);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating message");
                return false;
            }
        }

        public async Task<List<ChatMessageDto>> GetRecentChatMessagesAsync(int count = 50)
        {
            try
            {
                _logger.LogInformation("Getting {Count} recent chat messages", count);
                var uri = $"{ApiRoutes.Messages.RecentChat}?count={count}";
                var response = await _httpClientService.GetAsync<ApiResponse<List<ChatMessageDto>>>(uri);
                return response?.Data ?? new List<ChatMessageDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent chat messages");
                return new List<ChatMessageDto>();
            }
        }

        public async Task<bool> MarkMessageAsReadAsync(int messageId)
        {
            try
            {
                _logger.LogInformation("Marking message {MessageId} as read", messageId);
                var uri = string.Format(ApiRoutes.Messages.MarkRead, messageId);
                await _httpClientService.PostAsync<object>(uri, null);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking message {MessageId} as read", messageId);
                return false;
            }
        }

        public async Task<bool> CreateChatMessageAsync(ChatMessageCreateDto message)
        {
            try
            {
                _logger.LogInformation("Creating new chat message");
                await _httpClientService.PostAsync(ApiRoutes.Messages.Chat, message);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating chat message");
                return false;
            }
        }
    }
}