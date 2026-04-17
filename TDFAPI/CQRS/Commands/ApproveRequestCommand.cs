using MediatR;
using TDFAPI.CQRS.Core;
using TDFShared.DTOs.Requests;
using TDFAPI.Services;
using TDFAPI.Repositories;
using TDFShared.Utilities;
using TDFShared.Services;
using TDFAPI.Extensions;
using INotificationService = TDFAPI.Services.INotificationService;

namespace TDFAPI.CQRS.Commands
{
    public class ApproveRequestCommand : ICommand<bool>
    {
        public int RequestId { get; set; }
        public int ApproverId { get; set; }
        public bool IsHR { get; set; }
        public string? Remarks { get; set; }
    }

    public class ApproveRequestCommandHandler : IRequestHandler<ApproveRequestCommand, bool>
    {
        private readonly IRequestRepository _requestRepository;
        private readonly IUserRepository _userRepository;
        private readonly INotificationService _notificationService;
        private readonly ILogger<ApproveRequestCommandHandler> _logger;

        public ApproveRequestCommandHandler(
            IRequestRepository requestRepository,
            IUserRepository userRepository,
            INotificationService notificationService,
            ILogger<ApproveRequestCommandHandler> logger)
        {
            _requestRepository = requestRepository;
            _userRepository = userRepository;
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task<bool> Handle(ApproveRequestCommand request, CancellationToken cancellationToken)
        {
            var currentUserEntity = await _userRepository.GetByIdAsync(request.ApproverId);
            if (currentUserEntity == null) throw new System.UnauthorizedAccessException("User not found.");
            var currentUser = currentUserEntity.ToDto();

            var requestEntity = await _requestRepository.GetByIdAsync(request.RequestId);
            if (requestEntity == null) throw new TDFAPI.Exceptions.EntityNotFoundException("Request", request.RequestId);
            var requestDto = requestEntity.ToResponseDto();

            if (!RequestStateManager.CanApproveRequest(requestDto, currentUser))
            {
                throw new System.UnauthorizedAccessException("You do not have permission to approve this request.");
            }

            if (request.IsHR)
            {
                if (requestEntity.RequestManagerStatus != TDFShared.Enums.RequestStatus.ManagerApproved)
                    throw new TDFShared.Exceptions.BusinessRuleException("Request must be manager approved before HR approval.");

                requestEntity.RequestManagerStatus = TDFShared.Enums.RequestStatus.HRApproved;
                requestEntity.RequestHRStatus = TDFShared.Enums.RequestStatus.HRApproved;
                requestEntity.HRApproverId = request.ApproverId;
                requestEntity.HRRemarks = request.Remarks;
                requestEntity.UpdatedAt = DateTime.UtcNow;

                // Deduct leave balance upon final HR approval
                bool balanceUpdated = await _requestRepository.UpdateLeaveBalanceAsync(
                    requestEntity.RequestUserID,
                    requestEntity.RequestType,
                    requestEntity.RequestNumberOfDays ?? 0,
                    false);

                if (!balanceUpdated)
                {
                    _logger.LogWarning("Failed to update leave balance for user {UserId} after request {RequestId} approval.",
                        requestEntity.RequestUserID, requestEntity.RequestID);
                }

                await _requestRepository.UpdateAsync(requestEntity);
                await _notificationService.CreateNotificationAsync(requestEntity.RequestUserID, $"Your {requestEntity.RequestType} request has been fully approved.");
                return true;
            }
            else
            {
                if (requestEntity.RequestManagerStatus != TDFShared.Enums.RequestStatus.Pending)
                    throw new TDFShared.Exceptions.BusinessRuleException("Request is not pending manager approval.");

                requestEntity.RequestManagerStatus = TDFShared.Enums.RequestStatus.ManagerApproved;
                requestEntity.RequestHRStatus = TDFShared.Enums.RequestStatus.Pending;
                requestEntity.ManagerApproverId = request.ApproverId;
                requestEntity.ManagerRemarks = request.Remarks;
                requestEntity.UpdatedAt = DateTime.UtcNow;

                await _requestRepository.UpdateAsync(requestEntity);
                await NotifyHR($"Request from {requestEntity.RequestUserFullName} approved by manager.", request.ApproverId);
                return true;
            }
        }

        private async Task NotifyHR(string message, int excludedUserId = 0)
        {
            var hrUsers = await _userRepository.GetUsersByRoleAsync("HR");
            foreach (var hr in hrUsers.Where(h => h.UserID != excludedUserId))
            {
                await _notificationService.CreateNotificationAsync(hr.UserID, message);
            }
        }
    }
}
