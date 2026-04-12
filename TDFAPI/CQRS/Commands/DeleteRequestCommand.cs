using MediatR;
using TDFAPI.CQRS.Core;
using TDFAPI.Services;
using TDFAPI.Repositories;
using TDFShared.DTOs.Requests;
using TDFShared.Utilities;
using TDFShared.Services;
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

            var currentUser = await _userRepository.GetByIdAsync(request.UserId);
            if (currentUser == null) throw new System.UnauthorizedAccessException("User not found.");

            bool isOwner = existingEntity.RequestUserID == request.UserId;
            var existingDto = MapToResponseDto(existingEntity);
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

        private RequestResponseDto MapToResponseDto(TDFShared.Models.Request.RequestEntity entity)
        {
            if (entity == null) return null!;
            return new RequestResponseDto
            {
                RequestID = entity.RequestID,
                RequestUserID = entity.RequestUserID,
                UserName = entity.RequestUserFullName,
                LeaveType = entity.RequestType,
                RequestReason = entity.RequestReason,
                RequestStartDate = entity.RequestFromDay,
                RequestEndDate = entity.RequestToDay,
                RequestBeginningTime = entity.RequestBeginningTime,
                RequestEndingTime = entity.RequestEndingTime,
                RequestDepartment = entity.RequestDepartment,
                Status = entity.RequestManagerStatus,
                HRStatus = entity.RequestHRStatus,
                CreatedDate = entity.CreatedAt.GetValueOrDefault(DateTime.MinValue),
                LastModifiedDate = entity.UpdatedAt,
                RequestNumberOfDays = entity.RequestNumberOfDays,
                RowVersion = entity.RowVersion
            };
        }
    }
}
