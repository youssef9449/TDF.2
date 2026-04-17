using System;
using TDFAPI.Repositories;
using TDFShared.DTOs.Messages;
using TDFShared.DTOs.Common;
using TDFShared.Models.Message;
using TDFAPI.Extensions;
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

        public async Task<MessageDto?> GetByIdAsync(int messageId)
        {
            try
            {
                var message = await _messageRepository.GetByIdAsync(messageId);
                if (message == null) return null;
                var sender = await _userRepository.GetByIdAsync(message.SenderID);
                return message.ToDto(sender);
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
                var messages = await _messageRepository.GetConversationAsync(userId1, userId2);
                var dtos = new List<MessageDto>();
                foreach (var m in messages)
                {
                    var sender = await _userRepository.GetByIdAsync(m.SenderID);
                    dtos.Add(m.ToDto(sender));
                }
                return dtos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving conversation between users {UserId1} and {UserId2}", userId1, userId2);
                throw;
            }
        }

        public async Task<PaginatedResult<MessageDto>> GetByUserIdAsync(int userId, MessagePaginationDto? pagination = null)
        {
            try
            {
                if (pagination != null)
                {
                    var result = await _messageRepository.GetByUserIdAsync(userId, pagination);
                    var dtos = new List<MessageDto>();
                    foreach (var m in result.Items)
                    {
                        var sender = await _userRepository.GetByIdAsync(m.SenderID);
                        dtos.Add(m.ToDto(sender));
                    }
                    return new PaginatedResult<MessageDto>
                    {
                        Items = dtos,
                        TotalCount = result.TotalCount,
                        PageNumber = result.PageNumber,
                        PageSize = result.PageSize
                    };
                }
                else
                {
                    var messages = await _messageRepository.GetByUserIdAsync(userId);
                    var dtos = new List<MessageDto>();
                    foreach (var m in messages)
                    {
                        var sender = await _userRepository.GetByIdAsync(m.SenderID);
                        dtos.Add(m.ToDto(sender));
                    }
                    return new PaginatedResult<MessageDto>
                    {
                        Items = dtos,
                        TotalCount = dtos.Count,
                        PageNumber = 1,
                        PageSize = dtos.Count == 0 ? 50 : dtos.Count
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
                await _unitOfWork.BeginTransactionAsync();

                var message = new MessageEntity
                {
                    SenderID = senderId,
                    ReceiverID = messageDto.ReceiverId,
                    MessageText = messageDto.Content,
                    Timestamp = DateTime.UtcNow,
                    IsRead = false,
                    IsDelivered = false,
                    MessageType = messageDto.MessageType,
                    IdempotencyKey = messageDto.IdempotencyKey ?? Guid.NewGuid().ToString()
                };

                var messageId = await _messageRepository.CreateAsync(message);
                message.MessageID = messageId;

                await _notificationService.SendNotificationAsync(
                    messageDto.ReceiverId,
                    "New Message",
                    $"New message from {senderName}",
                    NotificationType.Info);

                await _unitOfWork.CommitAsync();
                var sender = await _userRepository.GetByIdAsync(message.SenderID);
                return message.ToDto(sender);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating message from {SenderId}", senderId);
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }

        public async Task<bool> MarkAsReadAsync(int messageId, int userId)
        {
            return await _messageRepository.MarkMessageAsReadAsync(messageId, userId);
        }

        public async Task<bool> MarkAsDeliveredAsync(int messageId, int userId)
        {
            return await _messageRepository.MarkMessageAsDeliveredAsync(messageId, userId);
        }

        public async Task<bool> DeleteAsync(int messageId, int userId)
        {
            return await _messageRepository.DeleteAsync(messageId);
        }

        public async Task<IEnumerable<MessageDto>> GetUndeliveredMessagesAsync(int senderId, int receiverId)
        {
            var conversation = await _messageRepository.GetConversationAsync(senderId, receiverId);
            var undelivered = conversation.Where(m => m.SenderID == senderId && m.ReceiverID == receiverId && !m.IsDelivered);
            var dtos = new List<MessageDto>();
            foreach (var m in undelivered)
            {
                var sender = await _userRepository.GetByIdAsync(m.SenderID);
                dtos.Add(m.ToDto(sender));
            }
            return dtos;
        }

        public async Task<int> GetUnreadMessagesCountAsync(int userId)
        {
            var messages = await _messageRepository.GetByUserIdAsync(userId);
            return messages.Count(m => !m.IsRead && m.ReceiverID == userId);
        }

        public async Task<List<ChatMessageDto>> GetRecentChatMessagesAsync(int count = 50)
        {
            var messages = await _messageRepository.GetRecentMessagesAsync(count);
            var dtos = new List<ChatMessageDto>();
            foreach (var m in messages)
            {
                var sender = await _userRepository.GetByIdAsync(m.SenderID);
                dtos.Add(m.ToChatDto(sender?.FullName));
            }
            return dtos;
        }

        public async Task<ChatMessageDto> CreateChatMessageAsync(ChatMessageCreateDto messageDto, int senderId, string senderName)
        {
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var message = new MessageEntity
                {
                    SenderID = senderId,
                    ReceiverID = messageDto.ReceiverId,
                    MessageText = messageDto.Content,
                    Timestamp = DateTime.UtcNow,
                    IsRead = false,
                    IsDelivered = false,
                    MessageType = messageDto.MessageType,
                    IsGlobal = messageDto.ReceiverId == 0,
                    Department = messageDto.Department,
                    IdempotencyKey = messageDto.IdempotencyKey ?? Guid.NewGuid().ToString()
                };

                var messageId = await _messageRepository.CreateAsync(message);
                message.MessageID = messageId;

                if (messageDto.ReceiverId != 0)
                {
                    await _notificationService.SendNotificationAsync(messageDto.ReceiverId, "New Chat Message", $"Message from {senderName}", NotificationType.Info);
                }

                await _unitOfWork.CommitAsync();
                return message.ToChatDto(senderName);
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }

        public async Task<ChatMessageDto?> GetByIdempotencyKeyAsync(string idempotencyKey, int userId)
        {
            var message = await _messageRepository.GetByIdempotencyKeyAsync(idempotencyKey, userId);
            if (message == null) return null;
            var sender = await _userRepository.GetByIdAsync(message.SenderID);
            return message.ToChatDto(sender?.FullName);
        }
    }
}
