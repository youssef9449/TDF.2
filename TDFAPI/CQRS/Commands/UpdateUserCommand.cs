using MediatR;
using TDFAPI.CQRS.Core;
using TDFShared.DTOs.Users;
using TDFAPI.Repositories;
using TDFShared.Services;
using TDFShared.Models.User;
using TDFShared.Exceptions;
using TDFAPI.Extensions;

namespace TDFAPI.CQRS.Commands
{
    public class UpdateUserCommand : ICommand<bool>
    {
        public int UserId { get; set; }
        public UpdateUserRequest UserRequest { get; set; }
    }

    public class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, bool>
    {
        private readonly IUserRepository _userRepository;
        private readonly ILogger<UpdateUserCommandHandler> _logger;

        public UpdateUserCommandHandler(IUserRepository userRepository, ILogger<UpdateUserCommandHandler> logger)
        {
            _userRepository = userRepository;
            _logger = logger;
        }

        public async Task<bool> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
        {
            var userDto = request.UserRequest;

            // Check if full name is already taken
            if (!string.IsNullOrWhiteSpace(userDto.FullName))
            {
                var isFullNameTaken = await _userRepository.IsFullNameTakenAsync(userDto.FullName, request.UserId);
                if (isFullNameTaken)
                {
                    throw new ValidationException($"Full name '{userDto.FullName}' is already taken.");
                }
            }

            var existingEntity = await _userRepository.GetByIdAsync(request.UserId);
            if (existingEntity == null) return false;

            existingEntity.FullName = userDto.FullName ?? existingEntity.FullName;
            existingEntity.Department = userDto.Department ?? existingEntity.Department;
            existingEntity.Title = userDto.Title ?? existingEntity.Title;
            existingEntity.IsAdmin = userDto.IsAdmin;
            existingEntity.IsManager = userDto.IsManager;
            existingEntity.IsHR = userDto.IsHR;
            existingEntity.UpdatedAt = DateTime.UtcNow;

            return await _userRepository.UpdateAsync(existingEntity);
        }
    }
}
