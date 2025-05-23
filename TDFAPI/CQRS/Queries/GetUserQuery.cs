using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.ComponentModel.DataAnnotations;
using TDFAPI.Exceptions;
using TDFAPI.CQRS.Core;
using TDFShared.DTOs.Users;
using TDFAPI.Repositories;
using TDFAPI.Services;
using TDFAPI.Utilities;

namespace TDFAPI.CQRS.Queries
{
    /// <summary>
    /// Query to get a user by ID
    /// </summary>
    public class GetUserQuery : IQuery<UserDto>
    {
        [Required(ErrorMessage = "UserId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "UserId must be greater than 0")]
        public int UserId { get; set; }
    }

    /// <summary>
    /// Handler for GetUserQuery
    /// </summary>
    public class GetUserQueryHandler : IRequestHandler<GetUserQuery, UserDto>
    {
        private readonly IUserRepository _userRepository;
        private readonly ICacheService _cacheService;
        private readonly ILogger<GetUserQueryHandler> _logger;

        public GetUserQueryHandler(
            IUserRepository userRepository,
            ICacheService cacheService,
            ILogger<GetUserQueryHandler> logger)
        {
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<UserDto> Handle(GetUserQuery request, CancellationToken cancellationToken)
        {
            // Try to get from cache first
            string cacheKey = $"user:{request.UserId}";

            return await _cacheService.GetOrCreateAsync(cacheKey, async () =>
            {
                _logger.LogInformation("Cache miss for user {UserId}, loading from database", request.UserId);

                var user = await _userRepository.GetByIdAsync(request.UserId);
                if (user == null)
                {
                    throw new EntityNotFoundException("User", request.UserId);
                }

                return user; // UserDto is already returned from the repository
            }, 30, 10); // Cache for 30 minutes with 10 minute sliding expiration
        }
    }
}