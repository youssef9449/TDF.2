using MediatR;
using TDFAPI.CQRS.Core;
using TDFShared.DTOs.Requests;
using TDFShared.DTOs.Common;
using System.ComponentModel.DataAnnotations;
using TDFAPI.Repositories;
using TDFAPI.Services;
using TDFShared.Utilities;
using TDFShared.Enums;
using TDFShared.Services;

namespace TDFAPI.CQRS.Queries
{
    public class GetRequestsQuery : IQuery<PaginatedResult<RequestResponseDto>>
    {
        [Required]
        public int CurrentUserId { get; set; }

        public RequestPaginationDto Pagination { get; set; } = new();
    }

    public class GetRequestsQueryHandler : IRequestHandler<GetRequestsQuery, PaginatedResult<RequestResponseDto>>
    {
        private readonly IRequestRepository _requestRepository;
        private readonly IUserRepository _userRepository;
        private readonly ICacheService _cacheService;
        private readonly ILogger<GetRequestsQueryHandler> _logger;

        public GetRequestsQueryHandler(
            IRequestRepository requestRepository,
            IUserRepository userRepository,
            ICacheService cacheService,
            ILogger<GetRequestsQueryHandler> logger)
        {
            _requestRepository = requestRepository;
            _userRepository = userRepository;
            _cacheService = cacheService;
            _logger = logger;
        }

        public async Task<PaginatedResult<RequestResponseDto>> Handle(GetRequestsQuery request, CancellationToken cancellationToken)
        {
            var currentUser = await GetCachedUserAsync(request.CurrentUserId);
            if (currentUser == null) throw new System.UnauthorizedAccessException("User not found.");

            _logger.LogInformation("User {UserId} (Admin: {IsAdmin}, HR: {IsHR}, Manager: {IsManager}, Dept: {Department}) getting requests",
                request.CurrentUserId, currentUser.IsAdmin, currentUser.IsHR, currentUser.IsManager, currentUser.Department);

            var accessLevel = AuthorizationUtilities.GetRequestAccessLevel(currentUser);
            PaginatedResult<TDFShared.Models.Request.RequestEntity> result;

            switch (accessLevel)
            {
                case RequestAccessLevel.All:
                    result = await _requestRepository.GetAllAsync(request.Pagination);
                    break;

                case RequestAccessLevel.Department:
                    result = await _requestRepository.GetRequestsForManagerAsync(request.CurrentUserId, currentUser.Department, request.Pagination);
                    break;

                case RequestAccessLevel.Own:
                    result = await _requestRepository.GetByUserIdAsync(request.CurrentUserId, request.Pagination);
                    break;

                default:
                    throw new System.UnauthorizedAccessException("You do not have permission to view requests.");
            }

            return await MapToPaginatedResponseDto(result);
        }

        private async Task<PaginatedResult<RequestResponseDto>> MapToPaginatedResponseDto(PaginatedResult<TDFShared.Models.Request.RequestEntity> paginatedRequests)
        {
            var mappedItems = new List<RequestResponseDto>();
            foreach (var item in paginatedRequests.Items)
            {
                mappedItems.Add(await MapToResponseDto(item));
            }

            return new PaginatedResult<RequestResponseDto>
            {
                Items = mappedItems,
                TotalCount = paginatedRequests.TotalCount,
                PageNumber = paginatedRequests.PageNumber,
                PageSize = paginatedRequests.PageSize
            };
        }

        private async Task<RequestResponseDto> MapToResponseDto(TDFShared.Models.Request.RequestEntity entity)
        {
            var balances = await _requestRepository.GetLeaveBalancesAsync(entity.RequestUserID);
            var balanceKey = TDFShared.Enums.LeaveTypeHelper.GetBalanceKey(entity.RequestType);
            int? remainingBalance = (balanceKey != null && balances.TryGetValue(balanceKey, out int val)) ? val : null;

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
                RemainingBalance = remainingBalance,
                RowVersion = entity.RowVersion
            };
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
