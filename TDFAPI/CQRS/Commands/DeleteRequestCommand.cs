using MediatR;
using TDFAPI.CQRS.Core;
using TDFAPI.Repositories;
using TDFShared.Services;
using TDFAPI.Extensions;

namespace TDFAPI.CQRS.Commands
{
    public class DeleteRequestCommand : ICommand<bool>
    {
        public int RequestId { get; set; }
        public int UserId { get; set; }
    }

    public class DeleteRequestCommandHandler : IRequestHandler<DeleteRequestCommand, bool>
    {
        private readonly IRequestRepository _requestRepository;
        private readonly IUserRepository _userRepository;
        private readonly IRoleService _roleService;

        public DeleteRequestCommandHandler(IRequestRepository requestRepository, IUserRepository userRepository, IRoleService roleService)
        {
            _requestRepository = requestRepository;
            _userRepository = userRepository;
            _roleService = roleService;
        }

        public async Task<bool> Handle(DeleteRequestCommand request, CancellationToken cancellationToken)
        {
            var currentUserEntity = await _userRepository.GetByIdAsync(request.UserId);
            if (currentUserEntity == null) throw new System.UnauthorizedAccessException("User not found.");
            var currentUser = currentUserEntity.ToDtoWithRoles(_roleService);

            var requestEntity = await _requestRepository.GetByIdAsync(request.RequestId);
            if (requestEntity == null) return false;

            var requestDto = requestEntity.ToResponseDto();

            // Use the RequestStateManager from TDFShared.Services
            bool isOwner = requestDto.RequestUserID == currentUser.UserID;
            bool isAdmin = currentUser.IsAdmin ?? false;

            if (!RequestStateManager.CanDelete(requestDto, isAdmin, isOwner))
            {
                throw new System.UnauthorizedAccessException("You do not have permission to delete this request.");
            }

            return await _requestRepository.DeleteAsync(requestEntity.RequestID);
        }
    }
}
