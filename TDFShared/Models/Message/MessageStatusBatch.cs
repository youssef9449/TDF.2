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
        /// <summary>
        /// Gets the list of message IDs in the batch.
        /// </summary>
        public IReadOnlyList<int> MessageIds { get; }
        /// <summary>
        /// Gets the receiver's user ID for the batch.
        /// </summary>
        public int ReceiverId { get; }
        /// <summary>
        /// Gets the status to be applied to the batch of messages.
        /// </summary>
        public MessageStatus Status { get; }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="MessageStatusBatch"/> class.
        /// </summary>
        /// <param name="messageIds">The list of message IDs.</param>
        /// <param name="receiverId">The receiver's user ID.</param>
        /// <param name="status">The status to apply.</param>
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