using MediatR;
using TDFAPI.CQRS.Core;
using TDFShared.DTOs.Requests;
using TDFAPI.Services;
using TDFAPI.Repositories;
using TDFShared.Utilities;
using TDFShared.Services;
using INotificationService = TDFAPI.Services.INotificationService;

namespace TDFAPI.CQRS.Commands
{
    public class RejectRequestCommand : ICommand<bool>
    {
        public int RequestId { get; set; }
        public int RejecterId { get; set; }
        public bool IsHR { get; set; }
        public string Remarks { get; set; } = string.Empty;
    }

    public class RejectRequestCommandHandler : IRequestHandler<RejectRequestCommand, bool>
    {
        private readonly IRequestRepository _requestRepository;
        private readonly IUserRepository _userRepository;
        private readonly INotificationService _notificationService;
        private readonly ILogger<RejectRequestCommandHandler> _logger;

        public RejectRequestCommandHandler(
            IRequestRepository requestRepository,
            IUserRepository userRepository,
            INotificationService notificationService,
            ILogger<RejectRequestCommandHandler> logger)
        {
            _requestRepository = requestRepository;
            _userRepository = userRepository;
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task<bool> Handle(RejectRequestCommand request, CancellationToken cancellationToken)
        {
            var currentUser = await _userRepository.GetByIdAsync(request.RejecterId);
            if (currentUser == null) throw new System.UnauthorizedAccessException("User not found.");

            var requestEntity = await _requestRepository.GetByIdAsync(request.RequestId);
            if (requestEntity == null) throw new TDFAPI.Exceptions.EntityNotFoundException("Request", request.RequestId);

            string rejecterName = currentUser.FullName ?? "Unknown";

            if (request.IsHR)
            {
                if (!(currentUser.IsHR ?? false) && !(currentUser.IsAdmin ?? false))
                    throw new System.UnauthorizedAccessException("You do not have HR permissions.");

                if (requestEntity.RequestManagerStatus != TDFShared.Enums.RequestStatus.ManagerApproved)
                    throw new TDFShared.Exceptions.BusinessRuleException("Request must be manager approved before HR rejection.");

                requestEntity.RequestManagerStatus = TDFShared.Enums.RequestStatus.Rejected;
                requestEntity.RequestHRStatus = TDFShared.Enums.RequestStatus.Rejected;
                requestEntity.HRApproverId = request.RejecterId;
                requestEntity.HRRemarks = request.Remarks;
                requestEntity.UpdatedAt = DateTime.UtcNow;

                await _requestRepository.UpdateAsync(requestEntity);
                await _notificationService.CreateNotificationAsync(requestEntity.RequestUserID, $"Your {requestEntity.RequestType} request was rejected by HR. Reason: {request.Remarks}");
                await NotifyDepartmentManagers(requestEntity.RequestDepartment, $"Request from {requestEntity.RequestUserFullName} rejected by HR.", request.RejecterId);
                return true;
            }
            else
            {
                if (!(currentUser.IsManager ?? false) && !(currentUser.IsAdmin ?? false))
                    throw new System.UnauthorizedAccessException("You do not have manager permissions.");

                if (!(currentUser.IsAdmin ?? false) && !RequestStateManager.CanManageDepartment(currentUser, requestEntity.RequestDepartment))
                    throw new System.UnauthorizedAccessException("You can only reject requests from your department.");

                if (requestEntity.RequestManagerStatus != TDFShared.Enums.RequestStatus.Pending)
                    throw new TDFShared.Exceptions.BusinessRuleException("Request is not pending manager approval.");

                requestEntity.RequestManagerStatus = TDFShared.Enums.RequestStatus.Rejected;
                requestEntity.RequestHRStatus = TDFShared.Enums.RequestStatus.Rejected;
                requestEntity.ManagerApproverId = request.RejecterId;
                requestEntity.ManagerRemarks = request.Remarks;
                requestEntity.UpdatedAt = DateTime.UtcNow;

                await _requestRepository.UpdateAsync(requestEntity);
                await NotifyHR($"Request from {requestEntity.RequestUserFullName} rejected by manager.", request.RejecterId);
                await _notificationService.CreateNotificationAsync(requestEntity.RequestUserID, $"Your {requestEntity.RequestType} request was rejected by your manager. Reason: {request.Remarks}");
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

        private async Task NotifyDepartmentManagers(string department, string message, int excludedUserId = 0)
        {
            var usersInDepartment = await _userRepository.GetUsersByDepartmentAsync(department);
            var managers = await _userRepository.GetUsersByRoleAsync("Manager");
            var departmentManagers = managers.Where(m => usersInDepartment.Any(u => u.UserID == m.UserID) && m.UserID != excludedUserId);

            foreach (var manager in departmentManagers)
            {
                await _notificationService.CreateNotificationAsync(manager.UserID, message);
            }
        }
    }
}
