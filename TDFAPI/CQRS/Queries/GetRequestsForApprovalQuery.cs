using MediatR;
using TDFAPI.CQRS.Core;
using TDFShared.DTOs.Requests;
using TDFShared.DTOs.Common;
using TDFAPI.Repositories;
using TDFAPI.Services;
using TDFShared.Services;
using TDFAPI.Extensions;
using TDFShared.Utilities;

namespace TDFAPI.CQRS.Queries
{
    public class GetRequestsForApprovalQuery : IQuery<PaginatedResult<RequestResponseDto>>
    {
        public int UserId { get; set; }
        public RequestPaginationDto Pagination { get; set; } = null!;
    }

    public class GetRequestsForApprovalQueryHandler : IRequestHandler<GetRequestsForApprovalQuery, PaginatedResult<RequestResponseDto>>
    {
        private readonly IRequestRepository _requestRepository;
        private readonly IUserRepository _userRepository;
        private readonly ICacheService _cacheService;
        private readonly IRoleService _roleService;

        public GetRequestsForApprovalQueryHandler(
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

        public async Task<PaginatedResult<RequestResponseDto>> Handle(GetRequestsForApprovalQuery request, CancellationToken cancellationToken)
        {
            var currentUser = await GetCachedUserAsync(request.UserId);
            if (currentUser == null) throw new System.UnauthorizedAccessException("User not found.");

            var accessLevel = AuthorizationUtilities.GetRequestAccessLevel(currentUser);
            if (accessLevel == RequestAccessLevel.Own)
                throw new System.UnauthorizedAccessException("You do not have permission to approve requests.");

            if (accessLevel == RequestAccessLevel.Department)
            {
                request.Pagination.Department = currentUser.Department;
            }

            var result = await _requestRepository.GetRequestsForApprovalAsync(request.Pagination);

            return new PaginatedResult<RequestResponseDto>
            {
                Items = result.Items.Select(r => r.ToResponseDto()).ToList(),
                TotalCount = result.TotalCount,
                PageNumber = result.PageNumber,
                PageSize = result.PageSize
            };
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
