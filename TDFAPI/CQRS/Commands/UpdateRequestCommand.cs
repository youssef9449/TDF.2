using MediatR;
using TDFAPI.CQRS.Core;
using TDFShared.DTOs.Requests;
using TDFAPI.Services;
using TDFAPI.Repositories;
using TDFShared.Utilities;
using TDFShared.Services;
using System.ComponentModel.DataAnnotations;
using TDFAPI.Extensions;
using INotificationService = TDFAPI.Services.INotificationService;

namespace TDFAPI.CQRS.Commands
{
    public class UpdateRequestCommand : ICommand<RequestResponseDto>
    {
        public int RequestId { get; set; }
        public RequestUpdateDto UpdateDto { get; set; }
        public int UserId { get; set; }
    }

    public class UpdateRequestCommandHandler : IRequestHandler<UpdateRequestCommand, RequestResponseDto>
    {
        private readonly IRequestRepository _requestRepository;
        private readonly IUserRepository _userRepository;
        private readonly INotificationService _notificationService;
        private readonly TDFShared.Validation.IValidationService _validationService;
        private readonly TDFShared.Validation.IBusinessRulesService _businessRulesService;
        private readonly ILogger<UpdateRequestCommandHandler> _logger;

        public UpdateRequestCommandHandler(
            IRequestRepository requestRepository,
            IUserRepository userRepository,
            INotificationService notificationService,
            TDFShared.Validation.IValidationService validationService,
            TDFShared.Validation.IBusinessRulesService businessRulesService,
            ILogger<UpdateRequestCommandHandler> logger)
        {
            _requestRepository = requestRepository;
            _userRepository = userRepository;
            _notificationService = notificationService;
            _validationService = validationService;
            _businessRulesService = businessRulesService;
            _logger = logger;
        }

        public async Task<RequestResponseDto> Handle(UpdateRequestCommand request, CancellationToken cancellationToken)
        {
            var existingEntity = await _requestRepository.GetByIdAsync(request.RequestId);
            if (existingEntity == null) throw new TDFAPI.Exceptions.EntityNotFoundException("Request", request.RequestId);

            var currentUser = await _userRepository.GetByIdAsync(request.UserId);
            if (currentUser == null) throw new System.UnauthorizedAccessException("User not found.");

            bool isOwner = existingEntity.RequestUserID == request.UserId;
            // Map entity to DTO for RequestStateManager check
            var existingDto = existingEntity.ToResponseDto();
            if (!RequestStateManager.CanEdit(existingDto, currentUser.IsAdmin ?? false, isOwner))
            {
                throw new System.UnauthorizedAccessException("You do not have permission to edit this request.");
            }

            request.UpdateDto.RequestReason = _validationService.SanitizeInput(request.UpdateDto.RequestReason);

            // Business rule validation
            var context = new TDFShared.Validation.BusinessRuleContext
            {
                GetLeaveBalanceAsync = async (uid, leaveType) =>
                {
                    var balances = await _requestRepository.GetLeaveBalancesAsync(uid);
                    var key = TDFShared.Enums.LeaveTypeHelper.GetBalanceKey(leaveType);
                    return key != null && balances.TryGetValue(key, out var balance) ? balance : 0;
                },
                HasConflictingRequestsAsync = _requestRepository.HasConflictingRequestsAsync,
                GetRequestAsync = async (rid) => (await _requestRepository.GetByIdAsync(rid)).ToResponseDto(),
                MinAdvanceNoticeDays = 0,
                MaxRequestDurationDays = 30
            };

            var businessRuleResult = await _businessRulesService.ValidateLeaveRequestUpdateAsync(request.UpdateDto, request.RequestId, request.UserId, context);
            if (!businessRuleResult.IsValid)
            {
                throw new TDFShared.Exceptions.BusinessRuleException(string.Join("; ", businessRuleResult.Errors));
            }

            int numberOfDays = TDFShared.Utils.DateUtils.CalculateBusinessDays(
                request.UpdateDto.RequestStartDate,
                request.UpdateDto.RequestEndDate.HasValue ? request.UpdateDto.RequestEndDate.Value : request.UpdateDto.RequestStartDate);

            existingEntity.RequestType = request.UpdateDto.LeaveType;
            existingEntity.RequestReason = request.UpdateDto.RequestReason ?? string.Empty;
            existingEntity.RequestFromDay = request.UpdateDto.RequestStartDate;
            existingEntity.RequestToDay = request.UpdateDto.RequestEndDate;
            existingEntity.RequestBeginningTime = request.UpdateDto.RequestBeginningTime;
            existingEntity.RequestEndingTime = request.UpdateDto.RequestEndingTime;
            existingEntity.RequestNumberOfDays = numberOfDays;
            existingEntity.UpdatedAt = DateTime.UtcNow;

            await _requestRepository.UpdateAsync(existingEntity);
            var updatedEntity = await _requestRepository.GetByIdAsync(request.RequestId);

            await NotifyDepartmentManagers(updatedEntity.RequestDepartment, $"Request from {updatedEntity.RequestUserFullName} was updated", request.UserId);

            return updatedEntity.ToResponseDto();
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
