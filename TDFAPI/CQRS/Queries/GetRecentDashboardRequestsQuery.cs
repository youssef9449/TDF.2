using MediatR;
using TDFAPI.CQRS.Core;
using TDFShared.DTOs.Requests;
using TDFAPI.Services;
using TDFAPI.Repositories;
using TDFShared.Services;
using TDFAPI.Extensions;

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

            // Batch fetch leave balances
            var uniqueUserIds = entities.Select(i => i.RequestUserID).Distinct().ToList();
            var userBalances = new Dictionary<int, Dictionary<string, int>>();

            foreach (var uid in uniqueUserIds)
            {
                userBalances[uid] = await _requestRepository.GetLeaveBalancesAsync(uid);
            }

            foreach (var entity in entities)
            {
                var balances = userBalances.TryGetValue(entity.RequestUserID, out var b) ? b : new Dictionary<string, int>();
                var balanceKey = TDFShared.Enums.LeaveTypeHelper.GetBalanceKey(entity.RequestType);
                int? remainingBalance = (balanceKey != null && balances.TryGetValue(balanceKey, out int val)) ? val : null;

                list.Add(entity.ToResponseDto(remainingBalance));
            }
            return list;
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
