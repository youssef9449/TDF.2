using MediatR;
using TDFAPI.CQRS.Core;
using TDFShared.DTOs.Requests;
using TDFAPI.Services;
using TDFAPI.Repositories;
using TDFShared.Services;
using System.ComponentModel.DataAnnotations;
using TDFAPI.Extensions;

namespace TDFAPI.CQRS.Commands
{
    public class CreateRequestCommand : ICommand<RequestResponseDto>
    {
        [Required]
        public RequestCreateDto CreateDto { get; set; }

        [Required]
        public int UserId { get; set; }
    }

    public class CreateRequestCommandHandler : IRequestHandler<CreateRequestCommand, RequestResponseDto>
    {
        private readonly IRequestRepository _requestRepository;
        private readonly IUserRepository _userRepository;
        private readonly INotificationDispatchService _notificationService;
        private readonly TDFShared.Validation.IValidationService _validationService;
        private readonly TDFShared.Validation.IBusinessRulesService _businessRulesService;
        private readonly ILogger<CreateRequestCommandHandler> _logger;

        public CreateRequestCommandHandler(
            IRequestRepository requestRepository,
            IUserRepository userRepository,
            INotificationDispatchService notificationService,
            TDFShared.Validation.IValidationService validationService,
            TDFShared.Validation.IBusinessRulesService businessRulesService,
            ILogger<CreateRequestCommandHandler> logger)
        {
            _requestRepository = requestRepository;
            _userRepository = userRepository;
            _notificationService = notificationService;
            _validationService = validationService;
            _businessRulesService = businessRulesService;
            _logger = logger;
        }

        public async Task<RequestResponseDto> Handle(CreateRequestCommand request, CancellationToken cancellationToken)
        {
            var createDto = request.CreateDto;
            var userId = request.UserId;

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) throw new TDFAPI.Exceptions.EntityNotFoundException("User", userId);

            if (_validationService.ContainsDangerousPatterns(createDto.RequestReason))
            {
                _logger.LogWarning("User {UserId} attempted to create request with potentially dangerous input", userId);
                throw new TDFShared.Exceptions.ValidationException("Invalid input detected.");
            }

            createDto.RequestReason = _validationService.SanitizeInput(createDto.RequestReason);

            // Business Rule Validation
            var context = new TDFShared.Validation.BusinessRuleContext
            {
                GetLeaveBalanceAsync = async (uid, leaveType) =>
                {
                    var balances = await _requestRepository.GetLeaveBalancesAsync(uid);
                    var key = TDFShared.Enums.LeaveTypeHelper.GetBalanceKey(leaveType);
                    return key != null && balances.TryGetValue(key, out var balance) ? balance : 0;
                },
                HasConflictingRequestsAsync = _requestRepository.HasConflictingRequestsAsync,
                MinAdvanceNoticeDays = 0,
                MaxRequestDurationDays = 30
            };

            var businessRuleResult = await _businessRulesService.ValidateLeaveRequestAsync(createDto, userId, context);
            if (!businessRuleResult.IsValid)
            {
                throw new TDFShared.Exceptions.BusinessRuleException(string.Join("; ", businessRuleResult.Errors));
            }

            int numberOfDays = TDFShared.Utilities.DateUtils.CalculateBusinessDays(
                createDto.RequestStartDate,
                createDto.RequestEndDate.HasValue ? createDto.RequestEndDate.Value : createDto.RequestStartDate);

            var requestEntity = new TDFShared.Models.Request.RequestEntity
            {
                RequestUserID = userId,
                RequestUserFullName = user.FullName ?? string.Empty,
                RequestDepartment = user.Department,
                RequestType = createDto.LeaveType,
                RequestReason = createDto.RequestReason ?? string.Empty,
                RequestFromDay = createDto.RequestStartDate,
                RequestToDay = createDto.RequestEndDate,
                RequestBeginningTime = createDto.RequestBeginningTime,
                RequestEndingTime = createDto.RequestEndingTime,
                RequestManagerStatus = TDFShared.Enums.RequestStatus.Pending,
                RequestHRStatus = TDFShared.Enums.RequestStatus.Pending,
                RequestNumberOfDays = numberOfDays,
                CreatedAt = DateTime.UtcNow
            };

            int requestId = await _requestRepository.CreateAsync(requestEntity);
            var createdEntity = await _requestRepository.GetByIdAsync(requestId);

            await NotifyApprovers(requestEntity.RequestDepartment,
                $"New {requestEntity.RequestType} request from {user.FullName}", userId);

            return createdEntity.ToResponseDto();
        }

        private async Task NotifyApprovers(string requestDepartment, string message, int excludedUserId = 0)
        {
            // Managers of the requester's department (including multi-department managers
            // whose Department field is like "dep1-dep2" and thus covers both constituents).
            var managers = await _userRepository.GetUsersByRoleAsync("Manager");
            var departmentManagers = managers.Where(m =>
                !string.IsNullOrEmpty(m.Department) &&
                RequestStateManager.CanAccessDepartment(m.Department, requestDepartment) &&
                m.UserID != excludedUserId);

            foreach (var manager in departmentManagers)
            {
                await _notificationService.CreateNotificationAsync(manager.UserID, message);
            }

            // HR users see every request regardless of department.
            var hrUsers = await _userRepository.GetUsersByRoleAsync("HR");
            foreach (var hr in hrUsers.Where(h => h.UserID != excludedUserId))
            {
                await _notificationService.CreateNotificationAsync(hr.UserID, message);
            }
        }
    }
}
