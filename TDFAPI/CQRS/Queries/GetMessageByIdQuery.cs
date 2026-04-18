using MediatR;
using TDFAPI.CQRS.Core;
using TDFAPI.Extensions;
using TDFAPI.Repositories;
using TDFShared.DTOs.Messages;

namespace TDFAPI.CQRS.Queries
{
    public class GetMessageByIdQuery : IQuery<MessageDto?>
    {
        public int MessageId { get; set; }
    }

    public class GetMessageByIdQueryHandler : IRequestHandler<GetMessageByIdQuery, MessageDto?>
    {
        private readonly IMessageRepository _messageRepository;
        private readonly IUserRepository _userRepository;

        public GetMessageByIdQueryHandler(IMessageRepository messageRepository, IUserRepository userRepository)
        {
            _messageRepository = messageRepository;
            _userRepository = userRepository;
        }

        public async Task<MessageDto?> Handle(GetMessageByIdQuery request, CancellationToken cancellationToken)
        {
            var message = await _messageRepository.GetByIdAsync(request.MessageId);
            if (message == null) return null;

            var sender = await _userRepository.GetByIdAsync(message.SenderID);
            return message.ToDto(sender);
        }
    }
}
