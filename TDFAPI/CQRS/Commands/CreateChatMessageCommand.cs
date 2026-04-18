using MediatR;
using TDFAPI.CQRS.Core;
using TDFAPI.Extensions;
using TDFAPI.Repositories;
using TDFAPI.Services;
using TDFShared.DTOs.Messages;
using TDFShared.Enums;
using TDFShared.Models.Message;

namespace TDFAPI.CQRS.Commands
{
    public class CreateChatMessageCommand : ICommand<ChatMessageDto>
    {
        public ChatMessageCreateDto MessageDto { get; set; } = null!;
        public int SenderId { get; set; }
        public string SenderName { get; set; } = null!;
    }

    public class CreateChatMessageCommandHandler : IRequestHandler<CreateChatMessageCommand, ChatMessageDto>
    {
        private readonly IMessageRepository _messageRepository;
        private readonly INotificationService _notificationService;
        private readonly IUnitOfWork _unitOfWork;

        public CreateChatMessageCommandHandler(
            IMessageRepository messageRepository,
            INotificationService notificationService,
            IUnitOfWork unitOfWork)
        {
            _messageRepository = messageRepository;
            _notificationService = notificationService;
            _unitOfWork = unitOfWork;
        }

        public async Task<ChatMessageDto> Handle(CreateChatMessageCommand request, CancellationToken cancellationToken)
        {
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var message = new MessageEntity
                {
                    SenderID = request.SenderId,
                    ReceiverID = request.MessageDto.ReceiverId,
                    MessageText = request.MessageDto.Content,
                    Timestamp = DateTime.UtcNow,
                    IsRead = false,
                    IsDelivered = false,
                    MessageType = request.MessageDto.MessageType,
                    IsGlobal = request.MessageDto.ReceiverId == 0,
                    Department = request.MessageDto.Department,
                    IdempotencyKey = request.MessageDto.IdempotencyKey ?? Guid.NewGuid().ToString()
                };

                var messageId = await _messageRepository.CreateAsync(message);
                message.MessageID = messageId;

                if (request.MessageDto.ReceiverId != 0)
                {
                    await _notificationService.SendNotificationAsync(
                        request.MessageDto.ReceiverId,
                        "New Chat Message",
                        $"Message from {request.SenderName}",
                        NotificationType.Info);
                }

                await _unitOfWork.CommitAsync();
                return message.ToChatDto(request.SenderName);
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }
    }
}
