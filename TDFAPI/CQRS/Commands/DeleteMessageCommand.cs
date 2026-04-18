using MediatR;
using TDFAPI.CQRS.Core;
using TDFAPI.Repositories;

namespace TDFAPI.CQRS.Commands
{
    public class DeleteMessageCommand : ICommand<bool>
    {
        public int MessageId { get; set; }
        public int UserId { get; set; }
    }

    public class DeleteMessageCommandHandler : IRequestHandler<DeleteMessageCommand, bool>
    {
        private readonly IMessageRepository _messageRepository;

        public DeleteMessageCommandHandler(IMessageRepository messageRepository)
        {
            _messageRepository = messageRepository;
        }

        public async Task<bool> Handle(DeleteMessageCommand request, CancellationToken cancellationToken)
        {
            // The repository currently doesn't check UserId for deletion, but we include it in the command for future-proofing and security.
            return await _messageRepository.DeleteAsync(request.MessageId);
        }
    }
}
