using MediatR;
using TDFAPI.CQRS.Core;
using TDFShared.DTOs.Messages;
using TDFAPI.Repositories;
using TDFAPI.Extensions;
using TDFShared.Models.User;

namespace TDFAPI.CQRS.Queries
{
    public class GetConversationQuery : IQuery<IEnumerable<MessageDto>>
    {
        public int UserId1 { get; set; }
        public int UserId2 { get; set; }
    }

    public class GetConversationQueryHandler : IRequestHandler<GetConversationQuery, IEnumerable<MessageDto>>
    {
        private readonly IMessageRepository _messageRepository;
        private readonly IUserRepository _userRepository;

        public GetConversationQueryHandler(IMessageRepository messageRepository, IUserRepository userRepository)
        {
            _messageRepository = messageRepository;
            _userRepository = userRepository;
        }

        public async Task<IEnumerable<MessageDto>> Handle(GetConversationQuery request, CancellationToken cancellationToken)
        {
            var messages = await _messageRepository.GetConversationAsync(request.UserId1, request.UserId2);
            var items = messages.ToList();

            var uniqueSenderIds = items.Select(m => m.SenderID).Distinct().ToList();
            var senders = await _userRepository.GetUsersByIdsAsync(uniqueSenderIds);
            var senderMap = senders.ToDictionary(s => s.UserID);

            return items.Select(m => {
                senderMap.TryGetValue(m.SenderID, out var sender);
                return m.ToDto(sender);
            }).ToList();
        }
    }
}
