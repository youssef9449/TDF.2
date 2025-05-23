using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using TDFAPI.CQRS.Core;
using TDFShared.Exceptions;
using TDFShared.DTOs.Messages;
using TDFAPI.Messaging;
using TDFAPI.Repositories;
using TDFAPI.Services;
using TDFShared.Models.Message;
using TDFShared.Enums;
using TDFAPI.Exceptions;

namespace TDFAPI.CQRS.Commands
{
    /// <summary>
    /// Command to create a new message
    /// </summary>
    public class CreateMessageCommand : ICommand<MessageResponseDto>
    {
        [Required(ErrorMessage = "SenderId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "SenderId must be greater than 0")]
        public int SenderId { get; set; }

        [Required(ErrorMessage = "RecipientId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "RecipientId must be greater than 0")]
        public int RecipientId { get; set; }

        [Required(ErrorMessage = "Content is required")]
        [StringLength(4000, ErrorMessage = "Content must be less than 4000 characters")]
        public string Content { get; set; }

        public bool IsPrivate { get; set; }

        [Required(ErrorMessage = "CorrelationId is required")]
        public string CorrelationId { get; set; }
    }

    /// <summary>
    /// Handler for CreateMessageCommand
    /// </summary>
    public class CreateMessageCommandHandler : IRequestHandler<CreateMessageCommand, MessageResponseDto>
    {
        private readonly IMessageRepository _messageRepository;
        private readonly IUserRepository _userRepository;
        private readonly MessageStore _messageStore;
        private readonly ILogger<CreateMessageCommandHandler> _logger;
        private readonly IUnitOfWork _unitOfWork;

        public CreateMessageCommandHandler(
            IMessageRepository messageRepository,
            IUserRepository userRepository,
            MessageStore messageStore,
            IUnitOfWork unitOfWork,
            ILogger<CreateMessageCommandHandler> logger)
        {
            _messageRepository = messageRepository ?? throw new ArgumentNullException(nameof(messageRepository));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _messageStore = messageStore ?? throw new ArgumentNullException(nameof(messageStore));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<MessageResponseDto> Handle(CreateMessageCommand request, CancellationToken cancellationToken)
        {
            // Validate entities exist
            var sender = await _userRepository.GetByIdAsync(request.SenderId);
            if (sender == null)
            {
                throw new EntityNotFoundException("User", request.SenderId);
            }

            var recipient = await _userRepository.GetByIdAsync(request.RecipientId);
            if (recipient == null)
            {
                throw new EntityNotFoundException("User", request.RecipientId);
            }

            try
            {
                // Start transaction
                await _unitOfWork.BeginTransactionAsync();

                // Create the message entity
                var message = MessageEntity.CreateChatMessage(
                    request.SenderId,
                    request.RecipientId,
                    request.Content,
                    false,
                    request.CorrelationId
                );

                // Save to database
                var messageId = await _messageRepository.CreateAsync(message);

                // Create websocket message for real-time delivery
                var webSocketMessage = new WebSocketMessage
                {
                    Type = "new_message",
                    From = request.SenderId.ToString(),
                    To = request.RecipientId.ToString(),
                    Content = request.Content,
                    CorrelationId = request.CorrelationId ?? Guid.NewGuid().ToString()
                };

                // Store for guaranteed delivery
                _messageStore.StoreMessage(webSocketMessage);

                // Commit transaction
                await _unitOfWork.CommitAsync();

                // Return response
                return new MessageResponseDto
                {
                    MessageID = messageId,
                    Status = "sent",
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating message from {SenderId} to {RecipientId}",
                    request.SenderId, request.RecipientId);

                await _unitOfWork.RollbackAsync();
                throw;
            }
        }
    }
}