using System;
using System.Collections.Generic;
using TDFShared.Enums;

namespace TDFShared.Models.Message
{
    /// <summary>
    /// Value object representing a batch of messages with the same status update
    /// </summary>
    public class MessageStatusBatch
    {
        public IReadOnlyList<int> MessageIds { get; }
        public int ReceiverId { get; }
        public MessageStatus Status { get; }
        
        public MessageStatusBatch(IReadOnlyList<int> messageIds, int receiverId, MessageStatus status)
        {
            if (messageIds == null || messageIds.Count == 0)
                throw new ArgumentException("MessageIds must not be empty", nameof(messageIds));
                
            if (receiverId <= 0)
                throw new ArgumentException("ReceiverId must be positive", nameof(receiverId));
                
            MessageIds = messageIds;
            ReceiverId = receiverId;
            Status = status;
        }
    }
} 