using MediatR;
using TDFAPI.CQRS.Core;
using TDFAPI.Repositories;

namespace TDFAPI.CQRS.Commands
{
    public class MarkMessageAsDeliveredCommand : ICommand<bool>
    {
        public int MessageId { get; set; }
        public int UserId { get; set; }
    }

    public class MarkMessageAsDeliveredCommandHandler : IRequestHandler<MarkMessageAsDeliveredCommand, bool>
    {
        private readonly IMessageRepository _messageRepository;

        public MarkMessageAsDeliveredCommandHandler(IMessageRepository messageRepository)
        {
            _messageRepository = messageRepository;
        }

        public async Task<bool> Handle(MarkMessageAsDeliveredCommand request, CancellationToken cancellationToken)
        {
            return await _messageRepository.MarkMessageAsDeliveredAsync(request.MessageId, request.UserId);
        }
    }
}
