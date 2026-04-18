using MediatR;
using TDFAPI.CQRS.Core;
using TDFAPI.Extensions;
using TDFAPI.Repositories;
using TDFShared.DTOs.Messages;

namespace TDFAPI.CQRS.Queries
{
    public class GetMessageByIdempotencyKeyQuery : IQuery<ChatMessageDto?>
    {
        public string IdempotencyKey { get; set; } = string.Empty;
        public int UserId { get; set; }
    }

    public class GetMessageByIdempotencyKeyQueryHandler : IRequestHandler<GetMessageByIdempotencyKeyQuery, ChatMessageDto?>
    {
        private readonly IMessageRepository _messageRepository;
        private readonly IUserRepository _userRepository;

        public GetMessageByIdempotencyKeyQueryHandler(IMessageRepository messageRepository, IUserRepository userRepository)
        {
            _messageRepository = messageRepository;
            _userRepository = userRepository;
        }

        public async Task<ChatMessageDto?> Handle(GetMessageByIdempotencyKeyQuery request, CancellationToken cancellationToken)
        {
            var message = await _messageRepository.GetByIdempotencyKeyAsync(request.IdempotencyKey, request.UserId);
            if (message == null) return null;

            var sender = await _userRepository.GetByIdAsync(message.SenderID);
            return message.ToChatDto(sender?.FullName);
        }
    }
}
