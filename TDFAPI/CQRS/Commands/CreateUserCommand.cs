using MediatR;
using TDFAPI.CQRS.Core;
using TDFShared.DTOs.Users;
using TDFAPI.Repositories;
using TDFShared.Services;
using TDFShared.Validation;
using TDFShared.Models.User;
using TDFShared.Models.Request;
using TDFShared.Enums;
using TDFAPI.Extensions;
using TDFShared.Exceptions;

namespace TDFAPI.CQRS.Commands
{
    public class CreateUserCommand : ICommand<UserDto>
    {
        public CreateUserRequest UserRequest { get; set; }
    }

    public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, UserDto>
    {
        private readonly IUserRepository _userRepository;
        private readonly IAuthService _authService;
        private readonly IBusinessRulesService _businessRulesService;
        private readonly ILogger<CreateUserCommandHandler> _logger;

        public CreateUserCommandHandler(
            IUserRepository userRepository,
            IAuthService authService,
            IBusinessRulesService businessRulesService,
            ILogger<CreateUserCommandHandler> logger)
        {
            _userRepository = userRepository;
            _authService = authService;
            _businessRulesService = businessRulesService;
            _logger = logger;
        }

        public async Task<UserDto> Handle(CreateUserCommand request, CancellationToken cancellationToken)
        {
            var userDto = request.UserRequest;

            // Business Rule Validation
            var context = new BusinessRuleContext
            {
                UsernameExistsAsync = async (uname) => await _userRepository.GetByUsernameAsync(uname) != null,
                FullNameExistsAsync = async (fname) => await _userRepository.IsFullNameTakenAsync(fname)
            };

            var validationResult = await _businessRulesService.ValidateUserCreationAsync(userDto, context);
            if (!validationResult.IsValid)
            {
                throw new BusinessRuleException(string.Join("; ", validationResult.Errors));
            }

            // Hash password
            var passwordHash = _authService.HashPassword(userDto.Password, out string salt);

            var newUser = new UserEntity
            {
                UserName = userDto.Username,
                FullName = userDto.FullName,
                Department = userDto.Department,
                Title = userDto.Title,
                PasswordHash = passwordHash,
                Salt = salt,
                IsAdmin = userDto.IsAdmin,
                IsManager = userDto.IsManager,
                IsHR = false,
                CreatedAt = DateTime.UtcNow,
                IsActive = false,
                IsConnected = false,
                PresenceStatus = UserPresenceStatus.Offline,
                IsAvailableForChat = false,
                FailedLoginAttempts = 0,
                IsLocked = false
            };

            var annualLeave = new AnnualLeaveEntity
            {
                FullName = newUser.FullName,
                Annual = 15,
                EmergencyLeave = 6,
                Permissions = 24,
                AnnualUsed = 0,
                EmergencyUsed = 0,
                PermissionsUsed = 0,
                UnpaidUsed = 0,
                WorkFromHomeUsed = 0
            };
            newUser.AnnualLeave = annualLeave;

            int userId = await _userRepository.AddAsync(newUser) ? newUser.UserID : 0;
            if (userId == 0) throw new InvalidOperationException("Failed to create user.");

            var createdUser = await _userRepository.GetByIdAsync(userId);
            return createdUser!.ToDto();
        }
    }
}
