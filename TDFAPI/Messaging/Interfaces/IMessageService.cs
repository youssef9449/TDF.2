using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TDFShared.Models.Message;
using TDFShared.Enums;
using TDFAPI.Messaging.Commands;

namespace TDFAPI.Messaging.Interfaces
{
    /// <summary>
    /// Service interface for managing messages between users
    /// </summary>
    public interface IMessageService
    {
        /// <summary>
        /// Sends a message from one user to another
        /// </summary>
        /// <param name="senderId">The user sending the message</param>
        /// <param name="receiverId">The user receiving the message</param>
        /// <param name="content">Message content</param>
        /// <param name="messageType">Message type (chat, system, or notification)</param>
        /// <returns>Success status</returns>
        Task<bool> SendMessageAsync(int senderId, int receiverId, string content, MessageType messageType = MessageType.Chat);

        /// <summary>
        /// Retrieves unread messages for a user
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>Collection of unread messages</returns>
        Task<IEnumerable<MessageEntity>> GetUnreadMessagesAsync(int userId);

        /// <summary>
        /// Marks a message as read
        /// </summary>
        /// <param name="messageId">Message ID</param>
        /// <param name="userId">User ID of the reader</param>
        /// <returns>Success status</returns>
        Task<bool> MarkAsReadAsync(int messageId, int userId);

        /// <summary>
        /// Marks a message as delivered
        /// </summary>
        /// <param name="messageId">Message ID</param>
        /// <param name="userId">User ID of the recipient</param>
        /// <returns>Success status</returns>
        Task<bool> MarkAsDeliveredAsync(int messageId, int userId);

        /// <summary>
        /// Gets all messages between two users
        /// </summary>
        /// <param name="userId1">First user ID</param>
        /// <param name="userId2">Second user ID</param>
        /// <returns>Collection of messages</returns>
        Task<IEnumerable<MessageEntity>> GetConversationAsync(int userId1, int userId2);

        /// <summary>
        /// Gets messages for a user (sent and received)
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>Collection of messages</returns>
        Task<IEnumerable<MessageEntity>> GetUserMessagesAsync(int userId);

        /// <summary>
        /// Deletes a message if the user has permissions
        /// </summary>
        /// <param name="messageId">Message ID</param>
        /// <param name="userId">User ID requesting deletion</param>
        /// <returns>Success status</returns>
        Task<bool> DeleteMessageAsync(int messageId, int userId);

        /// <summary>
        /// Broadcasts a message to multiple users
        /// </summary>
        /// <param name="command">The broadcast command</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The list of created messages</returns>
        Task<IReadOnlyList<MessageEntity>> BroadcastMessageAsync(BroadcastMessageCommand command, CancellationToken cancellationToken = default);

        /// <summary>
        /// Marks messages as delivered
        /// </summary>
        /// <param name="command">The command containing message IDs to mark</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The number of messages marked as delivered</returns>
        Task<int> MarkMessagesAsDeliveredAsync(MarkMessagesAsDeliveredCommand command, CancellationToken cancellationToken = default);

        /// <summary>
        /// Marks messages as read
        /// </summary>
        /// <param name="command">The command containing message IDs to mark</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The number of messages marked as read</returns>
        Task<int> MarkMessagesAsReadAsync(MarkMessagesAsReadCommand command, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves undelivered messages for a user
        /// </summary>
        /// <param name="userId">The ID of the user</param>
        /// <param name="limit">Maximum number of messages to retrieve</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Undelivered messages</returns>
        Task<IReadOnlyList<MessageEntity>> GetUndeliveredMessagesAsync(int userId, int limit = 100, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets message thread between two users
        /// </summary>
        /// <param name="user1Id">First user ID</param>
        /// <param name="user2Id">Second user ID</param>
        /// <param name="limit">Maximum number of messages to retrieve</param>
        /// <param name="before">Optional timestamp to get messages before</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of messages between the users</returns>
        Task<IReadOnlyList<MessageEntity>> GetMessageThreadAsync(int user1Id, int user2Id, int limit = 50, DateTime? before = null, CancellationToken cancellationToken = default);
    }
}