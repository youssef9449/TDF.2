using MediatR;
using TDFAPI.CQRS.Core;
using TDFShared.DTOs.Requests;
using TDFAPI.Services;
using TDFAPI.Repositories;
using TDFShared.Services;

namespace TDFAPI.CQRS.Queries
{
    public class GetRecentDashboardRequestsQuery : IQuery<List<RequestResponseDto>>
    {
        public int UserId { get; set; }
    }

    public class GetRecentDashboardRequestsQueryHandler : IRequestHandler<GetRecentDashboardRequestsQuery, List<RequestResponseDto>>
    {
        private readonly IRequestRepository _requestRepository;
        private readonly IUserRepository _userRepository;
        private readonly ICacheService _cacheService;

        public GetRecentDashboardRequestsQueryHandler(
            IRequestRepository requestRepository,
            IUserRepository userRepository,
            ICacheService cacheService)
        {
            _requestRepository = requestRepository;
            _userRepository = userRepository;
            _cacheService = cacheService;
        }

        public async Task<List<RequestResponseDto>> Handle(GetRecentDashboardRequestsQuery request, CancellationToken cancellationToken)
        {
            var currentUser = await GetCachedUserAsync(request.UserId);
            if (currentUser == null) throw new System.UnauthorizedAccessException("User not found.");

            // Port logic from RequestController.GetRecentRequestsForDashboard
            var pagination = new RequestPaginationDto { Page = 1, PageSize = 20, SortBy = "CreatedDate", Ascending = false };
            TDFShared.DTOs.Common.PaginatedResult<TDFShared.Models.Request.RequestEntity> result;

            if (currentUser.IsHR ?? false)
            {
                result = await _requestRepository.GetAllAsync(pagination);
                var items = result?.Items?.Where(r => r.RequestHRStatus == TDFShared.Enums.RequestStatus.Pending).OrderByDescending(r => r.CreatedAt).Take(5).ToList() ?? new List<TDFShared.Models.Request.RequestEntity>();
                return await MapToResponseDtoList(items);
            }
            else if (currentUser.IsAdmin ?? false)
            {
                result = await _requestRepository.GetAllAsync(pagination);
                var items = result?.Items?.Where(r => r.RequestManagerStatus == TDFShared.Enums.RequestStatus.Pending && r.RequestHRStatus == TDFShared.Enums.RequestStatus.Pending).OrderByDescending(r => r.CreatedAt).Take(5).ToList() ?? new List<TDFShared.Models.Request.RequestEntity>();
                return await MapToResponseDtoList(items);
            }
            else if (currentUser.IsManager ?? false)
            {
                result = await _requestRepository.GetRequestsForManagerAsync(request.UserId, currentUser.Department, pagination);
                var items = result?.Items?.Where(r => r.RequestManagerStatus == TDFShared.Enums.RequestStatus.Pending).OrderByDescending(r => r.CreatedAt).Take(5).ToList() ?? new List<TDFShared.Models.Request.RequestEntity>();
                return await MapToResponseDtoList(items);
            }
            else
            {
                result = await _requestRepository.GetByUserIdAsync(request.UserId, pagination);
                var items = result?.Items?.Where(r => r.RequestManagerStatus == TDFShared.Enums.RequestStatus.Pending || r.RequestHRStatus == TDFShared.Enums.RequestStatus.Pending).OrderByDescending(r => r.CreatedAt).Take(5).ToList() ?? new List<TDFShared.Models.Request.RequestEntity>();
                return await MapToResponseDtoList(items);
            }
        }

        private async Task<List<RequestResponseDto>> MapToResponseDtoList(IEnumerable<TDFShared.Models.Request.RequestEntity> entities)
        {
            var list = new List<RequestResponseDto>();
            foreach (var entity in entities)
            {
                list.Add(await MapToResponseDto(entity));
            }
            return list;
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
