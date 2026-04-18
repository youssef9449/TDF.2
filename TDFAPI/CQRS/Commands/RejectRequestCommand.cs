using MediatR;
using TDFAPI.CQRS.Core;
using TDFShared.DTOs.Requests;
using TDFAPI.Services;
using TDFAPI.Repositories;
using TDFShared.Utilities;
using TDFShared.Services;
using TDFAPI.Extensions;

namespace TDFAPI.CQRS.Commands
{
    public class RejectRequestCommand : ICommand<bool>
    {
        public int RequestId { get; set; }
        public int RejecterId { get; set; }
        public bool IsHR { get; set; }
        public string? Remarks { get; set; }
    }

    public class RejectRequestCommandHandler : IRequestHandler<RejectRequestCommand, bool>
    {
        private readonly IRequestRepository _requestRepository;
        private readonly IUserRepository _userRepository;
        private readonly INotificationDispatchService _notificationService;
        private readonly ILogger<RejectRequestCommandHandler> _logger;
        private readonly IRoleService _roleService;

        public RejectRequestCommandHandler(
            IRequestRepository requestRepository,
            IUserRepository userRepository,
            INotificationDispatchService notificationService,
            ILogger<RejectRequestCommandHandler> logger,
            IRoleService roleService)
        {
            _requestRepository = requestRepository;
            _userRepository = userRepository;
            _notificationService = notificationService;
            _logger = logger;
            _roleService = roleService;
        }

        public async Task<bool> Handle(RejectRequestCommand request, CancellationToken cancellationToken)
        {
            var currentUserEntity = await _userRepository.GetByIdAsync(request.RejecterId);
            if (currentUserEntity == null) throw new System.UnauthorizedAccessException("User not found.");
            var currentUser = currentUserEntity.ToDtoWithRoles(_roleService);

            var requestEntity = await _requestRepository.GetByIdAsync(request.RequestId);
            if (requestEntity == null) throw new TDFAPI.Exceptions.EntityNotFoundException("Request", request.RequestId);
            var requestDto = requestEntity.ToResponseDto();

            if (!RequestStateManager.CanRejectRequest(requestDto, currentUser))
            {
                throw new System.UnauthorizedAccessException("You do not have permission to reject this request.");
            }

            if (request.IsHR)
            {
                requestEntity.RequestHRStatus = TDFShared.Enums.RequestStatus.Rejected;
                requestEntity.HRApproverId = request.RejecterId;
                requestEntity.HRRemarks = request.Remarks;
                requestEntity.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                requestEntity.RequestManagerStatus = TDFShared.Enums.RequestStatus.ManagerRejected;
                requestEntity.ManagerApproverId = request.RejecterId;
                requestEntity.ManagerRemarks = request.Remarks;
                requestEntity.UpdatedAt = DateTime.UtcNow;
            }

            await _requestRepository.UpdateAsync(requestEntity);
            await _notificationService.CreateNotificationAsync(requestEntity.RequestUserID, $"Your {requestEntity.RequestType} request has been rejected.");

            return true;
        }
    }
}
