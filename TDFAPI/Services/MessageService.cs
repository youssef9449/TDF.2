using System;
using TDFAPI.Repositories;
using System.Transactions;
using TDFShared.DTOs.Messages;
using TDFShared.DTOs.Common;
using TDFShared.Models.Message;
using TDFShared.Enums;
using TDFShared.Services;

namespace TDFAPI.Services
{
    public class MessageService : IMessageService
    {
        private readonly IMessageRepository _messageRepository;
        private readonly INotificationService _notificationService;
        private readonly IUserRepository _userRepository;
        private readonly ILogger<MessageService> _logger;
        private readonly IUnitOfWork _unitOfWork;

        public MessageService(
            IMessageRepository messageRepository,
            INotificationService notificationService,
            IUserRepository userRepository,
            ILogger<MessageService> logger,
            IUnitOfWork unitOfWork)
        {
            _messageRepository = messageRepository;
            _notificationService = notificationService;
            _userRepository = userRepository;
            _logger = logger;
            _unitOfWork = unitOfWork;
        }

        public async Task<IEnumerable<MessageDto>> GetAllAsync()
        {
            try
            {
                var messages = await _messageRepository.GetAllAsync();
                return messages.Select(m => MapToMessageDto(m));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all messages");
                throw;
            }
        }

        public async Task<MessageDto?> GetByIdAsync(int messageId)
        {
            try
            {
                var message = await _messageRepository.GetByIdAsync(messageId);
                return message != null ? MapToMessageDto(message) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving message {MessageId}", messageId);
                throw;
            }
        }

        public async Task<IEnumerable<MessageDto>> GetConversationAsync(int userId1, int userId2)
        {
            try
            {
                // Validate users exist
                await ValidateUserExistsAsync(userId1);
                await ValidateUserExistsAsync(userId2);

                var messages = await _messageRepository.GetConversationAsync(userId1, userId2);
                return messages.Select(m => MapToMessageDto(m));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving conversation between users {UserId1} and {UserId2}",
                    userId1, userId2);
                throw;
            }
        }

        // Consolidate previous redundant methods into a single method with optional pagination
        public async Task<PaginatedResult<MessageDto>> GetByUserIdAsync(int userId, MessagePaginationDto? pagination = null)
        {
            try
            {
                // Validate user exists
                await ValidateUserExistsAsync(userId);

                // Use pagination if provided, otherwise default
                if (pagination != null)
                {
                    var result = await _messageRepository.GetByUserIdAsync(userId, pagination);
                    return new PaginatedResult<MessageDto>
                    {
                        Items = result.Items.Select(m => MapToMessageDto(m)).ToList(),
                        TotalCount = result.TotalCount,
                        PageNumber = result.PageNumber,
                        PageSize = result.PageSize
                    };
                }
                else
                {
                    // Convert IEnumerable to PaginatedResult for consistent return type
                    var messages = await _messageRepository.GetByUserIdAsync(userId);
                    var messageDtos = messages.Select(m => MapToMessageDto(m)).ToList();
                    return new PaginatedResult<MessageDto>
                    {
                        Items = messageDtos,
                        TotalCount = messageDtos.Count,
                        PageNumber = 1,
                        PageSize = messageDtos.Count
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving messages for user {UserId}", userId);
                throw;
            }
        }

        public async Task<MessageDto> CreateAsync(MessageCreateDto messageDto, int senderId, string senderName)
        {
            try
            {
                // Verify receiver exists
                await ValidateUserExistsAsync(messageDto.ReceiverID);

                // Use UnitOfWork for transaction management
                await _unitOfWork.BeginTransactionAsync();

                var message = new MessageEntity
                {
                    SenderID = senderId,
                    ReceiverID = messageDto.ReceiverID,
                    MessageText = messageDto.MessageText,
                    Timestamp = DateTime.UtcNow,
                    IsRead = false,
                    IsDelivered = false,
                    MessageType = messageDto.MessageType,
                    IdempotencyKey = ComputeIdempotencyKey(senderId, messageDto.ReceiverID, messageDto.MessageText)
                };

                // Create the message and get its ID
                var messageId = await _messageRepository.CreateAsync(message);
                message.MessageID = messageId;

                // Create notification for the receiver
                await _notificationService.CreateNotificationAsync(
                    messageDto.ReceiverID,
                    $"New message from {senderName}: {TruncateWithEllipsis(messageDto.MessageText, 50)}");

                await _unitOfWork.CommitAsync();
                return MapToMessageDto(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating message from {SenderId} to {ReceiverId}",
                    senderId, messageDto.ReceiverID);
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }

        public async Task<bool> MarkAsReadAsync(int messageId, int userId)
        {
            try
            {
                return await _messageRepository.MarkMessageAsReadAsync(messageId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking message {MessageId} as read for user {UserId}",
                    messageId, userId);
                throw;
            }
        }

        public async Task<bool> MarkAsDeliveredAsync(int messageId, int userId)
        {
            try
            {
                return await _messageRepository.MarkMessageAsDeliveredAsync(messageId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking message {MessageId} as delivered for user {UserId}",
                    messageId, userId);
                throw;
            }
        }

        public async Task<bool> DeleteAsync(int messageId, int userId)
        {
            try
            {
                await _unitOfWork.BeginTransactionAsync();

                try
                {
                    // Verify message exists and belongs to user
                    var message = await _messageRepository.GetByIdAsync(messageId);
                    if (message == null || (message.SenderID != userId && message.ReceiverID != userId))
                    {
                        await _unitOfWork.RollbackAsync();
                        return false;
                    }

                    bool result = await _messageRepository.DeleteAsync(messageId);

                    if (result)
                    {
                        await _unitOfWork.CommitAsync();
                    }
                    else
                    {
                        await _unitOfWork.RollbackAsync();
                    }

                    return result;
                }
                catch (Exception)
                {
                    await _unitOfWork.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting message {MessageId} for user {UserId}",
                    messageId, userId);
                throw;
            }
        }

        public async Task<IEnumerable<MessageDto>> GetUndeliveredMessagesAsync(int senderId, int receiverId)
        {
            try
            {
                if (senderId <= 0 || receiverId <= 0)
                {
                    _logger.LogWarning("Invalid parameters for GetUndeliveredMessagesAsync: senderId={SenderId}, receiverId={ReceiverId}", senderId, receiverId);
                    return Enumerable.Empty<MessageDto>();
                }

                // Get all messages between these users
                var conversation = await _messageRepository.GetConversationAsync(senderId, receiverId);

                // Filter to get only undelivered messages from sender to receiver
                var undeliveredMessages = conversation
                    .Where(m => m.SenderID == senderId && m.ReceiverID == receiverId && !m.IsDelivered)
                    .ToList();

                return undeliveredMessages.Select(MapToMessageDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting undelivered messages from {SenderId} to {ReceiverId}", senderId, receiverId);
                throw;
            }
        }

        public async Task<List<ChatMessageDto>> GetRecentChatMessagesAsync(int count = 50)
        {
            try
            {
                var messages = await _messageRepository.GetRecentMessagesAsync(count);

                // Map to DTOs
                return messages.Select(m => MapToChatMessageDto(m)).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving recent chat messages");
                throw;
            }
        }

        public async Task<ChatMessageDto> CreateChatMessageAsync(ChatMessageCreateDto messageDto, int senderId, string senderName)
        {
            try
            {
                // Generate idempotency key if not provided
                string idempotencyKey = messageDto.IdempotencyKey ??
                    ComputeIdempotencyKey(senderId, messageDto.ReceiverID, messageDto.Content);

                // Check for existing message with same idempotency key
                var existingMessage = await GetByIdempotencyKeyAsync(idempotencyKey, senderId);
                if (existingMessage != null)
                {
                    _logger.LogInformation("Returning existing message with idempotency key {Key}", idempotencyKey);
                    return existingMessage;
                }

                // Use UnitOfWork for transaction management
                await _unitOfWork.BeginTransactionAsync();

                try
                {
                    // Determine if message is global or direct
                    bool isGlobalMessage = messageDto.ReceiverID == 0;

                    // Create the message entity
                    var message = new MessageEntity
                    {
                        SenderID = senderId,
                        ReceiverID = messageDto.ReceiverID,
                        MessageText = messageDto.Content,
                        Timestamp = DateTime.UtcNow,
                        IsRead = false,
                        IsDelivered = false,
                        MessageType = MessageType.Chat,
                        IsGlobal = isGlobalMessage,
                        Department = messageDto.Department,
                        IdempotencyKey = idempotencyKey
                    };

                    // Save to database and get the ID
                    var messageId = await _messageRepository.CreateAsync(message);
                    message.MessageID = messageId;

                    // Create a notification for the receiver if it's a direct message
                    if (!isGlobalMessage)
                    {
                        await _notificationService.CreateNotificationAsync(
                            messageDto.ReceiverID,
                            $"Chat message from {senderName}: {TruncateWithEllipsis(messageDto.Content, 50)}");
                    }

                    await _unitOfWork.CommitAsync();

                    // Convert to DTO for response
                    return MapToChatMessageDto(message, senderName);
                }
                catch (Exception)
                {
                    await _unitOfWork.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating chat message from {SenderId} to {ReceiverId}",
                    senderId, messageDto.ReceiverID);
                throw;
            }
        }

        public async Task<ChatMessageDto?> GetByIdempotencyKeyAsync(string idempotencyKey, int userId)
        {
            try
            {
                if (string.IsNullOrEmpty(idempotencyKey))
                {
                    return null;
                }

                var message = await _messageRepository.GetByIdempotencyKeyAsync(idempotencyKey, userId);
                if (message == null)
                {
                    return null;
                }

                // Get sender name for mapping
                var sender = await _userRepository.GetByIdAsync(message.SenderID);
                string senderName = sender?.FullName ?? "Unknown";

                return MapToChatMessageDto(message, senderName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving message by idempotency key {Key} for user {UserId}",
                    idempotencyKey, userId);
                return null;
            }
        }

        private async Task ValidateUserExistsAsync(int userId)
        {
            if (userId > 0 && await _userRepository.GetByIdAsync(userId) == null)
            {
                throw new ArgumentException($"User with ID {userId} not found");
            }
        }

        private ChatMessageDto MapToChatMessageDto(MessageEntity message, string? senderName = null)
        {
            // If sender name not provided, use placeholder
            senderName ??= "Unknown";

            return new ChatMessageDto
            {
                Id = message.MessageID,
                SenderId = message.SenderID,
                SenderName = senderName,
                ReceiverId = message.ReceiverID,
                MessageText = message.MessageText,
                SentAt = message.Timestamp,
                IsRead = message.IsRead,
                IsDelivered = message.IsDelivered,
                Department = message.Department,
                MessageType = message.MessageType,
                IsGlobal = message.IsGlobal
            };
        }

        private MessageDto MapToMessageDto(MessageEntity message)
        {
            return new MessageDto
            {
                Id = message.MessageID,
                SenderId = message.SenderID,
                ReceiverId = message.ReceiverID,
                Message = message.MessageText,
                Timestamp = message.Timestamp,
                IsRead = message.IsRead,
                IsDelivered = message.IsDelivered,
                MessageType = message.MessageType,
                FromUserProfileImage = new byte[0] // Initialize with empty array
            };
        }

        private string ComputeIdempotencyKey(int senderId, int receiverId, string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return Guid.NewGuid().ToString();
            }

            // Combine sender, receiver, message Message and timestamp to create a unique key
            string input = $"{senderId}:{receiverId}:{message}:{DateTime.UtcNow.Ticks}";
            return System.Convert.ToBase64String(
                System.Security.Cryptography.SHA256.Create().ComputeHash(
                    System.Text.Encoding.UTF8.GetBytes(input)
                )
            );
        }

        private string TruncateWithEllipsis(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            {
                return text ?? string.Empty;
            }
            return text.Substring(0, maxLength) + "...";
        }
    }
}