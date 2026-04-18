using MediatR;
using TDFAPI.CQRS.Core;
using TDFAPI.Extensions;
using TDFAPI.Repositories;
using TDFShared.DTOs.Users;
using TDFShared.Services;

namespace TDFAPI.CQRS.Commands
{
    public class CreateUserCommand : ICommand<UserDto>
    {
        public CreateUserRequest UserRequest { get; set; } = null!;
    }

    public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, UserDto>
    {
        private readonly IUserRepository _userRepository;
        private readonly ISecurityService _securityService;
        private readonly IRoleService _roleService;
        private readonly IUnitOfWork _unitOfWork;

        public CreateUserCommandHandler(
            IUserRepository userRepository,
            ISecurityService securityService,
            IRoleService roleService,
            IUnitOfWork unitOfWork)
        {
            _userRepository = userRepository;
            _securityService = securityService;
            _roleService = roleService;
            _unitOfWork = unitOfWork;
        }

        public async Task<UserDto> Handle(CreateUserCommand request, CancellationToken cancellationToken)
        {
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var passwordHash = _securityService.HashPassword(request.UserRequest.Password, out var passwordSalt);

                var user = new TDFShared.Models.User.UserEntity
                {
                    UserName = request.UserRequest.Username,
                    FullName = request.UserRequest.FullName,
                    Department = request.UserRequest.Department,
                    Title = request.UserRequest.Title,
                    IsAdmin = request.UserRequest.IsAdmin,
                    IsManager = request.UserRequest.IsManager,
                    IsHR = request.UserRequest.IsHR,
                    IsActive = false // Default from original logic
                };

                var userId = await _userRepository.CreateAsync(user, passwordHash, passwordSalt);
                user.UserID = userId;

                await _unitOfWork.CommitAsync();

                var dto = user.ToDto();
                dto.Roles = _roleService.GetRoles(dto).ToList();
                return dto;
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }
    }
}
