using MediatR;
using TDFAPI.CQRS.Core;
using TDFShared.DTOs.Requests;
using TDFAPI.Repositories;
using TDFAPI.Services;
using TDFShared.Utilities;
using TDFShared.Services;

namespace TDFAPI.CQRS.Queries
{
    public class GetRequestByIdQuery : IQuery<RequestResponseDto>
    {
        public int RequestId { get; set; }
        public int CurrentUserId { get; set; }
    }

    public class GetRequestByIdQueryHandler : IRequestHandler<GetRequestByIdQuery, RequestResponseDto>
    {
        private readonly IRequestRepository _requestRepository;
        private readonly IUserRepository _userRepository;
        private readonly ICacheService _cacheService;
        private readonly ILogger<GetRequestByIdQueryHandler> _logger;

        public GetRequestByIdQueryHandler(
            IRequestRepository requestRepository,
            IUserRepository userRepository,
            ICacheService cacheService,
            ILogger<GetRequestByIdQueryHandler> logger)
        {
            _requestRepository = requestRepository;
            _userRepository = userRepository;
            _cacheService = cacheService;
            _logger = logger;
        }

        public async Task<RequestResponseDto> Handle(GetRequestByIdQuery request, CancellationToken cancellationToken)
        {
            var entity = await _requestRepository.GetByIdAsync(request.RequestId);
            if (entity == null) throw new TDFAPI.Exceptions.EntityNotFoundException("Request", request.RequestId);

            var currentUser = await GetCachedUserAsync(request.CurrentUserId);
            if (currentUser == null) throw new System.UnauthorizedAccessException("User not found.");

            var balances = await _requestRepository.GetLeaveBalancesAsync(entity.RequestUserID);
            var balanceKey = TDFShared.Enums.LeaveTypeHelper.GetBalanceKey(entity.RequestType);
            int? remainingBalance = (balanceKey != null && balances.TryGetValue(balanceKey, out int val)) ? val : null;

            var requestDto = new RequestResponseDto
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
                RemainingBalance = remainingBalance,
                RowVersion = entity.RowVersion
            };

            if (!RequestStateManager.CanViewRequest(requestDto, currentUser))
            {
                _logger.LogWarning("User {UserId} tried to access request {RequestId} without permission", request.CurrentUserId, request.RequestId);
                throw new System.UnauthorizedAccessException("You do not have permission to view this request.");
            }

            return requestDto;
        }

        private async Task<TDFShared.DTOs.Users.UserDto?> GetCachedUserAsync(int userId)
        {
            var cacheKey = $"user_{userId}";
            return await _cacheService.GetOrCreateAsync(cacheKey,
                async () => await _userRepository.GetByIdAsync(userId),
                absoluteExpirationMinutes: 15,
                slidingExpirationMinutes: 5);
        }
    }
}
