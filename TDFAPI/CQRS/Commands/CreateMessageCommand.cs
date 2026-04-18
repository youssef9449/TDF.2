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
    public class CreateMessageCommand : ICommand<MessageDto>
    {
        public MessageCreateDto MessageDto { get; set; } = null!;
        public int SenderId { get; set; }
        public string SenderName { get; set; } = null!;
    }

    public class CreateMessageCommandHandler : IRequestHandler<CreateMessageCommand, MessageDto>
    {
        private readonly IMessageRepository _messageRepository;
        private readonly INotificationDispatchService _notificationService;
        private readonly IUserRepository _userRepository;
        private readonly IUnitOfWork _unitOfWork;

        public CreateMessageCommandHandler(
            IMessageRepository messageRepository,
            INotificationDispatchService notificationService,
            IUserRepository userRepository,
            IUnitOfWork unitOfWork)
        {
            _messageRepository = messageRepository;
            _notificationService = notificationService;
            _userRepository = userRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<MessageDto> Handle(CreateMessageCommand request, CancellationToken cancellationToken)
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
                    IdempotencyKey = request.MessageDto.IdempotencyKey ?? Guid.NewGuid().ToString()
                };

                var messageId = await _messageRepository.CreateAsync(message);
                message.MessageID = messageId;

                await _notificationService.SendNotificationAsync(
                    request.MessageDto.ReceiverId,
                    "New Message",
                    $"New message from {request.SenderName}",
                    NotificationType.Info);

                await _unitOfWork.CommitAsync();

                var sender = await _userRepository.GetByIdAsync(request.SenderId);
                return message.ToDto(sender);
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }
    }
}
