using MediatR;
using TDFAPI.CQRS.Core;
using TDFShared.DTOs.Messages;
using TDFShared.DTOs.Common;
using TDFAPI.Repositories;
using TDFAPI.Extensions;
using TDFShared.Models.User;

namespace TDFAPI.CQRS.Queries
{
    public class GetMessagesQuery : IQuery<PaginatedResult<MessageDto>>
    {
        public int UserId { get; set; }
        public MessagePaginationDto? Pagination { get; set; }
    }

    public class GetMessagesQueryHandler : IRequestHandler<GetMessagesQuery, PaginatedResult<MessageDto>>
    {
        private readonly IMessageRepository _messageRepository;
        private readonly IUserRepository _userRepository;

        public GetMessagesQueryHandler(IMessageRepository messageRepository, IUserRepository userRepository)
        {
            _messageRepository = messageRepository;
            _userRepository = userRepository;
        }

        public async Task<PaginatedResult<MessageDto>> Handle(GetMessagesQuery request, CancellationToken cancellationToken)
        {
            PaginatedResult<TDFShared.Models.Message.MessageEntity> result;
            if (request.Pagination != null)
            {
                result = await _messageRepository.GetByUserIdAsync(request.UserId, request.Pagination);
            }
            else
            {
                var messages = await _messageRepository.GetByUserIdAsync(request.UserId);
                var items = messages.ToList();
                result = new PaginatedResult<TDFShared.Models.Message.MessageEntity>
                {
                    Items = items,
                    TotalCount = items.Count,
                    PageNumber = 1,
                    PageSize = items.Count == 0 ? 50 : items.Count
                };
            }

            var uniqueSenderIds = result.Items.Select(m => m.SenderID).Distinct().ToList();
            var senders = await _userRepository.GetUsersByIdsAsync(uniqueSenderIds);
            var senderMap = senders.ToDictionary(s => s.UserID);

            var mappedItems = result.Items.Select(m => {
                senderMap.TryGetValue(m.SenderID, out var sender);
                return m.ToDto(sender);
            }).ToList();

            return new PaginatedResult<MessageDto>
            {
                Items = mappedItems,
                TotalCount = result.TotalCount,
                PageNumber = result.PageNumber,
                PageSize = result.PageSize
            };
        }
    }
}
