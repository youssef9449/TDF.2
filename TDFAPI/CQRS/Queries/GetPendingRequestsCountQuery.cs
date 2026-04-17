using MediatR;
using TDFAPI.CQRS.Core;
using TDFAPI.Services;
using TDFAPI.Repositories;
using TDFShared.DTOs.Requests;
using TDFShared.Services;
using TDFAPI.Extensions;

namespace TDFAPI.CQRS.Queries
{
    public class GetPendingRequestsCountQuery : IQuery<int>
    {
        public int UserId { get; set; }
    }

    public class GetPendingRequestsCountQueryHandler : IRequestHandler<GetPendingRequestsCountQuery, int>
    {
        private readonly IRequestRepository _requestRepository;
        private readonly IUserRepository _userRepository;
        private readonly ICacheService _cacheService;

        public GetPendingRequestsCountQueryHandler(
            IRequestRepository requestRepository,
            IUserRepository userRepository,
            ICacheService cacheService)
        {
            _requestRepository = requestRepository;
            _userRepository = userRepository;
            _cacheService = cacheService;
        }

        public async Task<int> Handle(GetPendingRequestsCountQuery request, CancellationToken cancellationToken)
        {
            var currentUser = await GetCachedUserAsync(request.UserId);
            if (currentUser == null) throw new System.UnauthorizedAccessException("User not found.");

            var pagination = new RequestPaginationDto { Page = 1, PageSize = 1000, SortBy = "CreatedDate", Ascending = false, CountOnly = true };

            if (currentUser.IsHR ?? false)
            {
                var result = await _requestRepository.GetAllAsync(pagination);
                return result?.Items?.Count(r => r.RequestHRStatus == TDFShared.Enums.RequestStatus.Pending) ?? 0;
            }
            else if (currentUser.IsAdmin ?? false)
            {
                var result = await _requestRepository.GetAllAsync(pagination);
                return result?.Items?.Count(r => r.RequestManagerStatus == TDFShared.Enums.RequestStatus.Pending && r.RequestHRStatus == TDFShared.Enums.RequestStatus.Pending) ?? 0;
            }
            else if (currentUser.IsManager ?? false)
            {
                var result = await _requestRepository.GetRequestsForManagerAsync(request.UserId, currentUser.Department, pagination);
                return result?.Items?.Count(r => r.RequestManagerStatus == TDFShared.Enums.RequestStatus.Pending) ?? 0;
            }
            else
            {
                var result = await _requestRepository.GetByUserIdAsync(request.UserId, pagination);
                return result?.Items?.Count(r => r.RequestManagerStatus == TDFShared.Enums.RequestStatus.Pending || r.RequestHRStatus == TDFShared.Enums.RequestStatus.Pending) ?? 0;
            }
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
