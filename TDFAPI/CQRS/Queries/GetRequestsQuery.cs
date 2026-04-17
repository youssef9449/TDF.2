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
using TDFAPI.Extensions;

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

            // Unified filtering approach:
            // 1. Determine the "Security Scope" based on the user's role.
            // 2. Allow user to further filter within that scope (via Pagination DTO).
            // 3. Ensure they don't exceed their scope.

            switch (accessLevel)
            {
                case RequestAccessLevel.All:
                    // Admin/HR can see anything. They can filter by userId or department if they want.
                    break;

                case RequestAccessLevel.Department:
                    // Managers can see their department's requests or their own requests.

                    if (request.Pagination.UserId.HasValue)
                    {
                        if (request.Pagination.UserId.Value == currentUser.UserID)
                        {
                            // If they are filtering for their own requests, we don't need to force the department filter
                            // as they might be in a sub-department of the one they manage.
                            request.Pagination.Department = null;
                        }
                        else
                        {
                            // If they want to see another specific user, force their managed department scope.
                            request.Pagination.Department = currentUser.Department;
                        }
                    }
                    else if (string.IsNullOrEmpty(request.Pagination.Department))
                    {
                        // Default to their managed department if no specific user/dept is requested
                        request.Pagination.Department = currentUser.Department;
                    }
                    else if (!RequestStateManager.CanAccessDepartment(currentUser.Department, request.Pagination.Department))
                    {
                         // They are trying to access a department they don't have access to
                         throw new System.UnauthorizedAccessException("You do not have permission to view requests for this department.");
                    }
                    break;

                case RequestAccessLevel.Own:
                    // Regular users can ONLY see their own requests. Force the filter.
                    request.Pagination.UserId = currentUser.UserID;
                    request.Pagination.Department = null; // Clear department filter if set
                    break;

                default:
                    throw new System.UnauthorizedAccessException("You do not have permission to view requests.");
            }

            var result = await _requestRepository.GetAllAsync(request.Pagination);
            return await MapToPaginatedResponseDto(result);
        }

        private async Task<PaginatedResult<RequestResponseDto>> MapToPaginatedResponseDto(PaginatedResult<TDFShared.Models.Request.RequestEntity> paginatedRequests)
        {
            var mappedItems = new List<RequestResponseDto>();

            // Batch fetch leave balances for all unique users in the result set to avoid N+1 issues
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
