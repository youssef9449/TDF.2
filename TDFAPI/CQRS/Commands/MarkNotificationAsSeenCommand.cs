using MediatR;
using TDFAPI.CQRS.Core;
using TDFAPI.Repositories;

namespace TDFAPI.CQRS.Commands
{
    public class MarkNotificationAsSeenCommand : ICommand<bool>
    {
        public int NotificationId { get; set; }
        public int UserId { get; set; }
    }

    public class MarkNotificationAsSeenCommandHandler : IRequestHandler<MarkNotificationAsSeenCommand, bool>
    {
        private readonly INotificationRepository _notificationRepository;

        public MarkNotificationAsSeenCommandHandler(INotificationRepository notificationRepository)
        {
            _notificationRepository = notificationRepository;
        }

        public async Task<bool> Handle(MarkNotificationAsSeenCommand request, CancellationToken cancellationToken)
        {
            return await _notificationRepository.MarkNotificationAsSeenAsync(request.NotificationId, request.UserId);
        }
    }
}
