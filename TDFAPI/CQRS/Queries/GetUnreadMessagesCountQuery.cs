using MediatR;
using TDFAPI.CQRS.Core;
using TDFAPI.Repositories;

namespace TDFAPI.CQRS.Queries
{
    public class GetUnreadMessagesCountQuery : IQuery<int>
    {
        public int UserId { get; set; }
    }

    public class GetUnreadMessagesCountQueryHandler : IRequestHandler<GetUnreadMessagesCountQuery, int>
    {
        private readonly IMessageRepository _messageRepository;

        public GetUnreadMessagesCountQueryHandler(IMessageRepository messageRepository)
        {
            _messageRepository = messageRepository;
        }

        public async Task<int> Handle(GetUnreadMessagesCountQuery request, CancellationToken cancellationToken)
        {
            // Original logic in MessageService:
            // var messages = await _messageRepository.GetByUserIdAsync(userId);
            // return messages.Count(m => !m.IsRead && m.ReceiverID == userId);

            // The repository has GetUnreadCountAsync(int userId) which should be more efficient
            return await _messageRepository.GetUnreadCountAsync(request.UserId);
        }
    }
}
