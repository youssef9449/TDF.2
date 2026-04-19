using MediatR;
using TDFAPI.CQRS.Core;
using TDFShared.DTOs.Requests;
using TDFAPI.Services;
using TDFAPI.Repositories;
using TDFShared.Utilities;
using TDFShared.Services;
using System.ComponentModel.DataAnnotations;
using TDFAPI.Extensions;

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
        private readonly INotificationDispatchService _notificationService;
        private readonly TDFShared.Validation.IValidationService _validationService;
        private readonly TDFShared.Validation.IBusinessRulesService _businessRulesService;
        private readonly ILogger<UpdateRequestCommandHandler> _logger;

        public UpdateRequestCommandHandler(
            IRequestRepository requestRepository,
            IUserRepository userRepository,
            INotificationDispatchService notificationService,
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

            int numberOfDays = TDFShared.Utilities.DateUtils.CalculateBusinessDays(
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

            // Any edit invalidates previous approvals/rejections: the manager and HR
            // must review the new content from scratch.
            existingEntity.RequestManagerStatus = TDFShared.Enums.RequestStatus.Pending;
            existingEntity.RequestHRStatus = TDFShared.Enums.RequestStatus.Pending;
            existingEntity.ManagerApproverId = null;
            existingEntity.ManagerRemarks = null;
            existingEntity.HRApproverId = null;
            existingEntity.HRRemarks = null;

            await _requestRepository.UpdateAsync(existingEntity);
            var updatedEntity = await _requestRepository.GetByIdAsync(request.RequestId);

            await NotifyApprovers(updatedEntity.RequestDepartment,
                $"Request from {updatedEntity.RequestUserFullName} was updated and needs re-review",
                request.UserId);

            return updatedEntity.ToResponseDto();
        }

        private async Task NotifyApprovers(string requestDepartment, string message, int excludedUserId = 0)
        {
            // Managers whose department covers the request's department (including
            // hyphenated "dep1-dep2" managers who should cover both constituents).
            var managers = await _userRepository.GetUsersByRoleAsync("Manager");
            var departmentManagers = managers.Where(m =>
                !string.IsNullOrEmpty(m.Department) &&
                RequestStateManager.CanAccessDepartment(m.Department, requestDepartment) &&
                m.UserID != excludedUserId);

            foreach (var manager in departmentManagers)
            {
                await _notificationService.CreateNotificationAsync(manager.UserID, message);
            }

            var hrUsers = await _userRepository.GetUsersByRoleAsync("HR");
            foreach (var hr in hrUsers.Where(h => h.UserID != excludedUserId))
            {
                await _notificationService.CreateNotificationAsync(hr.UserID, message);
            }
        }
    }
}
