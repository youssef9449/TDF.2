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
    public class GetRequestsQuery : IQuery<PaginatedResult<RequestResponseDto>>
    {
        public int CurrentUserId { get; set; }
        public RequestPaginationDto Pagination { get; set; } = null!;
    }

    public class GetRequestsQueryHandler : IRequestHandler<GetRequestsQuery, PaginatedResult<RequestResponseDto>>
    {
        private readonly IRequestRepository _requestRepository;
        private readonly IUserRepository _userRepository;
        private readonly ICacheService _cacheService;
        private readonly IRoleService _roleService;

        public GetRequestsQueryHandler(
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

        public async Task<PaginatedResult<RequestResponseDto>> Handle(GetRequestsQuery request, CancellationToken cancellationToken)
        {
            var currentUser = await GetCachedUserAsync(request.CurrentUserId);
            if (currentUser == null) throw new System.UnauthorizedAccessException("User not found.");

            // Determine access level based on role
            var accessLevel = AuthorizationUtilities.GetRequestAccessLevel(currentUser);

            // Apply filters based on access level
            if (accessLevel == RequestAccessLevel.Own)
            {
                request.Pagination.UserId = request.CurrentUserId;
            }
            else if (accessLevel == RequestAccessLevel.Department)
            {
                // For managers, we can allow them to see their own requests even if their department differs from the managed department
                // (Though usually they manage their own department)
                if (request.Pagination.UserId != request.CurrentUserId)
                {
                    request.Pagination.Department = currentUser.Department;
                }
            }

            var result = await _requestRepository.GetRequestsAsync(request.Pagination);

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
