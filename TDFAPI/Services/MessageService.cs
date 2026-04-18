using System;
using TDFShared.DTOs.Messages;
using TDFShared.DTOs.Common;
using TDFShared.Services;
using MediatR;
using TDFAPI.CQRS.Commands;
using TDFAPI.CQRS.Queries;

namespace TDFAPI.Services
{
    public class MessageService : IMessageService
    {
        private readonly IMediator _mediator;

        public MessageService(IMediator mediator)
        {
            _mediator = mediator;
        }

        public async Task<MessageDto?> GetByIdAsync(int messageId)
        {
            return await _mediator.Send(new GetMessageByIdQuery { MessageId = messageId });
        }

        public async Task<IEnumerable<MessageDto>> GetConversationAsync(int userId1, int userId2)
        {
            return await _mediator.Send(new GetConversationQuery { UserId1 = userId1, UserId2 = userId2 });
        }

        public async Task<PaginatedResult<MessageDto>> GetByUserIdAsync(int userId, MessagePaginationDto? pagination = null)
        {
            return await _mediator.Send(new GetMessagesQuery { UserId = userId, Pagination = pagination });
        }

        public async Task<MessageDto> CreateAsync(MessageCreateDto messageDto, int senderId, string senderName)
        {
            return await _mediator.Send(new CreateMessageCommand
            {
                MessageDto = messageDto,
                SenderId = senderId,
                SenderName = senderName
            });
        }

        public async Task<bool> MarkAsReadAsync(int messageId, int userId)
        {
            return await _mediator.Send(new MarkMessageAsReadCommand { MessageId = messageId, UserId = userId });
        }

        public async Task<bool> MarkAsDeliveredAsync(int messageId, int userId)
        {
            return await _mediator.Send(new MarkMessageAsDeliveredCommand { MessageId = messageId, UserId = userId });
        }

        public async Task<bool> DeleteAsync(int messageId, int userId)
        {
            return await _mediator.Send(new DeleteMessageCommand { MessageId = messageId, UserId = userId });
        }

        public async Task<IEnumerable<MessageDto>> GetUndeliveredMessagesAsync(int senderId, int receiverId)
        {
            // This was a specific helper method, we can keep it as is or move to a query
            var conversation = await GetConversationAsync(senderId, receiverId);
            return conversation.Where(m => m.SenderId == senderId && m.ReceiverId == receiverId && !m.IsDelivered);
        }

        public async Task<int> GetUnreadMessagesCountAsync(int userId)
        {
            return await _mediator.Send(new GetUnreadMessagesCountQuery { UserId = userId });
        }

        public async Task<List<ChatMessageDto>> GetRecentChatMessagesAsync(int count = 50)
        {
            return await _mediator.Send(new GetRecentChatMessagesQuery { Count = count });
        }

        public async Task<ChatMessageDto> CreateChatMessageAsync(ChatMessageCreateDto messageDto, int senderId, string senderName)
        {
            // We can add a property to CreateMessageCommand to handle ChatMessageDto response or create a new command
            // For now, let's reuse CreateMessageCommand but we might need a separate one if logic differs significantly
            // Actually, CreateMessageCommand returns MessageDto. Let's create CreateChatMessageCommand.
            return await _mediator.Send(new CreateChatMessageCommand
            {
                MessageDto = messageDto,
                SenderId = senderId,
                SenderName = senderName
            });
        }

        public async Task<ChatMessageDto?> GetByIdempotencyKeyAsync(string idempotencyKey, int userId)
        {
            return await _mediator.Send(new GetMessageByIdempotencyKeyQuery { IdempotencyKey = idempotencyKey, UserId = userId });
        }
    }
}
