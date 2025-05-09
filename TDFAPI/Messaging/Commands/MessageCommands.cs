using System;
using System.Collections.Generic;
using TDFShared.Enums;
using MediatR;

namespace TDFAPI.Messaging.Commands
{
    /// <summary>
    /// Command to send a message from one user to another
    /// </summary>
    public class SendMessageCommand : IRequest<bool>
    {
        public int SenderId { get; }
        public int ReceiverId { get; }
        public string Content { get; }
        public bool QueueIfOffline { get; }
        public MessageType Type { get; }
        
        public SendMessageCommand(int senderId, int receiverId, string content, MessageType type = MessageType.Chat, bool queueIfOffline = true)
        {
            if (senderId <= 0) throw new ArgumentException("SenderId must be positive", nameof(senderId));
            if (receiverId <= 0) throw new ArgumentException("ReceiverId must be positive", nameof(receiverId));
            if (string.IsNullOrWhiteSpace(content)) throw new ArgumentException("Content cannot be empty", nameof(content));
            
            SenderId = senderId;
            ReceiverId = receiverId;
            Content = content;
            Type = type;
            QueueIfOffline = queueIfOffline;
        }
    }
    
    /// <summary>
    /// Command to mark messages as delivered
    /// </summary>
    public record MarkMessagesAsDeliveredCommand
    {
        public int SenderId { get; }
        public int ReceiverId { get; }
        public IReadOnlyList<int> MessageIds { get; }
        
        public MarkMessagesAsDeliveredCommand(int senderId, int receiverId, IReadOnlyList<int> messageIds)
        {
            if (senderId <= 0) throw new ArgumentException("SenderId must be positive", nameof(senderId));
            if (receiverId <= 0) throw new ArgumentException("ReceiverId must be positive", nameof(receiverId));
            if (messageIds == null || messageIds.Count == 0) throw new ArgumentException("MessageIds cannot be empty", nameof(messageIds));
            
            SenderId = senderId;
            ReceiverId = receiverId;
            MessageIds = messageIds;
        }
    }
    
    /// <summary>
    /// Command to mark messages as read
    /// </summary>
    public record MarkMessagesAsReadCommand
    {
        public int SenderId { get; }
        public int ReceiverId { get; }
        public IReadOnlyList<int> MessageIds { get; }
        
        public MarkMessagesAsReadCommand(int senderId, int receiverId, IReadOnlyList<int> messageIds)
        {
            if (senderId <= 0) throw new ArgumentException("SenderId must be positive", nameof(senderId));
            if (receiverId <= 0) throw new ArgumentException("ReceiverId must be positive", nameof(receiverId));
            if (messageIds == null || messageIds.Count == 0) throw new ArgumentException("MessageIds cannot be empty", nameof(messageIds));
            
            SenderId = senderId;
            ReceiverId = receiverId;
            MessageIds = messageIds;
        }
    }
    
    /// <summary>
    /// Command to update user connection status
    /// </summary>
    public record UpdateUserConnectionCommand
    {
        public int UserId { get; }
        public bool IsOnline { get; }
        public string? DeviceInfo { get; }
        
        public UpdateUserConnectionCommand(int userId, bool isOnline, string? deviceInfo = null)
        {
            if (userId <= 0) throw new ArgumentException("UserId must be positive", nameof(userId));
            
            UserId = userId;
            IsOnline = isOnline;
            DeviceInfo = deviceInfo;
        }
    }
    
    /// <summary>
    /// Command to broadcast a message to multiple users
    /// </summary>
    public record BroadcastMessageCommand
    {
        public int SenderId { get; }
        public IReadOnlyList<int> RecipientIds { get; }
        public string Content { get; }
        public MessageType Type { get; }
        
        public BroadcastMessageCommand(int senderId, IReadOnlyList<int> recipientIds, string content, MessageType type = MessageType.Chat)
        {
            if (senderId <= 0) throw new ArgumentException("SenderId must be positive", nameof(senderId));
            if (recipientIds == null || recipientIds.Count == 0) throw new ArgumentException("RecipientIds cannot be empty", nameof(recipientIds));
            if (string.IsNullOrWhiteSpace(content)) throw new ArgumentException("Content cannot be empty", nameof(content));
            
            SenderId = senderId;
            RecipientIds = recipientIds;
            Content = content;
            Type = type;
        }
    }
} 