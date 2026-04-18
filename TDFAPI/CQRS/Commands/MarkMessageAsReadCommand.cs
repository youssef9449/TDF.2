using MediatR;
using TDFAPI.CQRS.Core;
using TDFAPI.Repositories;

namespace TDFAPI.CQRS.Commands
{
    public class MarkMessageAsReadCommand : ICommand<bool>
    {
        public int MessageId { get; set; }
        public int UserId { get; set; }
    }

    public class MarkMessageAsReadCommandHandler : IRequestHandler<MarkMessageAsReadCommand, bool>
    {
        private readonly IMessageRepository _messageRepository;

        public MarkMessageAsReadCommandHandler(IMessageRepository messageRepository)
        {
            _messageRepository = messageRepository;
        }

        public async Task<bool> Handle(MarkMessageAsReadCommand request, CancellationToken cancellationToken)
        {
            return await _messageRepository.MarkMessageAsReadAsync(request.MessageId, request.UserId);
        }
    }
}
