using MediatR;
using TDFAPI.CQRS.Core;
using TDFAPI.Extensions;
using TDFAPI.Repositories;
using TDFShared.DTOs.Messages;

namespace TDFAPI.CQRS.Queries
{
    public class GetRecentChatMessagesQuery : IQuery<List<ChatMessageDto>>
    {
        public int Count { get; set; } = 50;
    }

    public class GetRecentChatMessagesQueryHandler : IRequestHandler<GetRecentChatMessagesQuery, List<ChatMessageDto>>
    {
        private readonly IMessageRepository _messageRepository;
        private readonly IUserRepository _userRepository;

        public GetRecentChatMessagesQueryHandler(IMessageRepository messageRepository, IUserRepository userRepository)
        {
            _messageRepository = messageRepository;
            _userRepository = userRepository;
        }

        public async Task<List<ChatMessageDto>> Handle(GetRecentChatMessagesQuery request, CancellationToken cancellationToken)
        {
            var messages = await _messageRepository.GetRecentMessagesAsync(request.Count);

            // Collect unique sender IDs
            var senderIds = messages.Select(m => m.SenderID).Distinct().ToList();

            // Batch fetch users
            var usersList = await _userRepository.GetUsersByIdsAsync(senderIds);
            var users = usersList.ToDictionary(u => u.UserID, u => u.FullName);

            return messages.Select(m => m.ToChatDto(users.GetValueOrDefault(m.SenderID))).ToList();
        }
    }
}
