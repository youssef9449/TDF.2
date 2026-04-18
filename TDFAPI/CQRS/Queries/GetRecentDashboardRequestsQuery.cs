using MediatR;
using TDFAPI.CQRS.Core;
using TDFShared.DTOs.Requests;
using TDFAPI.Repositories;
using TDFAPI.Services;
using TDFShared.Services;
using TDFAPI.Extensions;
using TDFShared.Utilities;

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
        private readonly IRoleService _roleService;

        public GetRecentDashboardRequestsQueryHandler(
            IRequestRepository requestRepository,
            IUserRepository userRepository,
            ICacheService cacheService,
            IRoleService roleService)
        {
            _requestRepository = requestRepository;
            _userRepository = userRepository;
            _cacheService = cacheService;
            _roleService = roleService;
        }

        public async Task<List<RequestResponseDto>> Handle(GetRecentDashboardRequestsQuery request, CancellationToken cancellationToken)
        {
            var currentUser = await GetCachedUserAsync(request.UserId);
            if (currentUser == null) throw new System.UnauthorizedAccessException("User not found.");

            var accessLevel = AuthorizationUtilities.GetRequestAccessLevel(currentUser);

            var pagination = new RequestPaginationDto { Page = 1, PageSize = 5, SortBy = "CreatedDate", Ascending = false };

            if (accessLevel == RequestAccessLevel.Own)
            {
                pagination.UserId = request.UserId;
            }
            else if (accessLevel == RequestAccessLevel.Department)
            {
                pagination.Department = currentUser.Department;
            }

            var result = await _requestRepository.GetRequestsAsync(pagination);
            return result.Items.Select(r => r.ToResponseDto()).ToList();
        }

        private async Task<TDFShared.DTOs.Users.UserDto?> GetCachedUserAsync(int userId)
        {
            var cacheKey = $"user_{userId}";
            return await _cacheService.GetOrCreateAsync(cacheKey,
                async () => {
                    var user = await _userRepository.GetByIdAsync(userId);
                    return user?.ToDtoWithRoles(_roleService);
                },
                absoluteExpirationMinutes: 15,
                slidingExpirationMinutes: 5);
        }
    }
}
