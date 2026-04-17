using MediatR;
using TDFAPI.CQRS.Core;
using TDFAPI.Services;
using TDFAPI.Repositories;
using TDFShared.DTOs.Requests;
using TDFShared.Utilities;
using TDFShared.Services;
using TDFAPI.Extensions;
using INotificationService = TDFAPI.Services.INotificationService;

namespace TDFAPI.CQRS.Commands
{
    public class DeleteRequestCommand : ICommand<bool>
    {
        public int RequestId { get; set; }
        public int UserId { get; set; }
    }

    public class DeleteRequestCommandHandler : IRequestHandler<DeleteRequestCommand, bool>
    {
        private readonly IRequestRepository _requestRepository;
        private readonly IUserRepository _userRepository;
        private readonly INotificationService _notificationService;
        private readonly ILogger<DeleteRequestCommandHandler> _logger;

        public DeleteRequestCommandHandler(
            IRequestRepository requestRepository,
            IUserRepository userRepository,
            INotificationService notificationService,
            ILogger<DeleteRequestCommandHandler> logger)
        {
            _requestRepository = requestRepository;
            _userRepository = userRepository;
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task<bool> Handle(DeleteRequestCommand request, CancellationToken cancellationToken)
        {
            var existingEntity = await _requestRepository.GetByIdAsync(request.RequestId);
            if (existingEntity == null) throw new TDFAPI.Exceptions.EntityNotFoundException("Request", request.RequestId);

            var currentUserEntity = await _userRepository.GetByIdAsync(request.UserId);
            if (currentUserEntity == null) throw new System.UnauthorizedAccessException("User not found.");
            var currentUser = currentUserEntity.ToDto();

            bool isOwner = existingEntity.RequestUserID == request.UserId;
            var existingDto = existingEntity.ToResponseDto();
            if (!RequestStateManager.CanDelete(existingDto, currentUser.IsAdmin ?? false, isOwner))
            {
                throw new System.UnauthorizedAccessException("You do not have permission to delete this request.");
            }

            if (existingEntity.RequestManagerStatus != TDFShared.Enums.RequestStatus.Pending || existingEntity.RequestHRStatus != TDFShared.Enums.RequestStatus.Pending)
                throw new TDFShared.Exceptions.BusinessRuleException("Only requests that are pending both manager and HR approval can be deleted.");

            bool deleted = await _requestRepository.DeleteAsync(request.RequestId);

            if (deleted)
            {
                await NotifyDepartmentManagers(existingEntity.RequestDepartment, $"{existingEntity.RequestUserFullName} deleted a pending {existingEntity.RequestType} request.", request.UserId);
            }

            return deleted;
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
