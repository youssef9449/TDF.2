using MediatR;
using TDFAPI.CQRS.Core;
using TDFShared.DTOs.Requests;
using TDFShared.DTOs.Common;
using TDFAPI.Services;
using TDFAPI.Repositories;
using TDFShared.Services;
using TDFAPI.Extensions;

namespace TDFAPI.CQRS.Queries
{
    public class GetRequestsForApprovalQuery : IQuery<PaginatedResult<RequestResponseDto>>
    {
        public int UserId { get; set; }
        public RequestPaginationDto Pagination { get; set; } = new();
    }

    public class GetRequestsForApprovalQueryHandler : IRequestHandler<GetRequestsForApprovalQuery, PaginatedResult<RequestResponseDto>>
    {
        private readonly IRequestRepository _requestRepository;
        private readonly IUserRepository _userRepository;
        private readonly ICacheService _cacheService;

        public GetRequestsForApprovalQueryHandler(
            IRequestRepository requestRepository,
            IUserRepository userRepository,
            ICacheService cacheService)
        {
            _requestRepository = requestRepository;
            _userRepository = userRepository;
            _cacheService = cacheService;
        }

        public async Task<PaginatedResult<RequestResponseDto>> Handle(GetRequestsForApprovalQuery request, CancellationToken cancellationToken)
        {
            var currentUser = await GetCachedUserAsync(request.UserId);
            if (currentUser == null) throw new System.UnauthorizedAccessException("User not found.");

            request.Pagination ??= new RequestPaginationDto { Page = 1, PageSize = 20, SortBy = "CreatedDate", Ascending = false };

            PaginatedResult<TDFShared.Models.Request.RequestEntity> result;

            if (currentUser.IsHR ?? false)
            {
                request.Pagination.FilterStatus = TDFShared.Enums.RequestStatus.ManagerApproved;
                var rawResult = await _requestRepository.GetAllAsync(request.Pagination);
                var filteredItems = rawResult.Items.Where(r => r.RequestHRStatus == TDFShared.Enums.RequestStatus.Pending).ToList();
                result = new PaginatedResult<TDFShared.Models.Request.RequestEntity> { Items = filteredItems, PageNumber = request.Pagination.Page, PageSize = request.Pagination.PageSize, TotalCount = filteredItems.Count };
            }
            else if (currentUser.IsAdmin ?? false)
            {
                request.Pagination.FilterStatus = TDFShared.Enums.RequestStatus.Pending;
                result = await _requestRepository.GetAllAsync(request.Pagination);
            }
            else if (currentUser.IsManager ?? false)
            {
                var rawResult = await _requestRepository.GetRequestsForManagerAsync(request.UserId, currentUser.Department, request.Pagination);
                var filteredItems = rawResult.Items.Where(r => r.RequestManagerStatus == TDFShared.Enums.RequestStatus.Pending).ToList();
                result = new PaginatedResult<TDFShared.Models.Request.RequestEntity> { Items = filteredItems, PageNumber = request.Pagination.Page, PageSize = request.Pagination.PageSize, TotalCount = filteredItems.Count };
            }
            else
            {
                throw new System.UnauthorizedAccessException("You do not have permission to access approval requests.");
            }

            return await MapToPaginatedResponseDto(result);
        }

        private async Task<PaginatedResult<RequestResponseDto>> MapToPaginatedResponseDto(PaginatedResult<TDFShared.Models.Request.RequestEntity> paginatedRequests)
        {
            var mappedItems = new List<RequestResponseDto>();

            // Batch fetch leave balances to avoid N+1 queries
            var uniqueUserIds = paginatedRequests.Items.Select(i => i.RequestUserID).Distinct().ToList();
            var userBalances = new Dictionary<int, Dictionary<string, int>>();

            foreach (var uid in uniqueUserIds)
            {
                userBalances[uid] = await _requestRepository.GetLeaveBalancesAsync(uid);
            }

            foreach (var item in paginatedRequests.Items)
            {
                var balances = userBalances.TryGetValue(item.RequestUserID, out var b) ? b : new Dictionary<string, int>();
                var balanceKey = TDFShared.Enums.LeaveTypeHelper.GetBalanceKey(item.RequestType);
                int? remainingBalance = (balanceKey != null && balances.TryGetValue(balanceKey, out int val)) ? val : null;

                mappedItems.Add(item.ToResponseDto(remainingBalance));
            }

            return new PaginatedResult<RequestResponseDto>
            {
                Items = mappedItems,
                TotalCount = paginatedRequests.TotalCount,
                PageNumber = paginatedRequests.PageNumber,
                PageSize = paginatedRequests.PageSize
            };
        }

        private async Task<TDFShared.DTOs.Users.UserDto?> GetCachedUserAsync(int userId)
        {
            var cacheKey = $"user_{userId}";
            return await _cacheService.GetOrCreateAsync(cacheKey,
                async () => (await _userRepository.GetByIdAsync(userId))?.ToDto(),
                absoluteExpirationMinutes: 15,
                slidingExpirationMinutes: 5);
        }
    }
}
