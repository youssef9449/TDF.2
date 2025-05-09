using System;
using System.Text.Json.Serialization;
using TDFShared.Models.Message;
using TDFShared.Enums;

namespace TDFAPI.Messaging
{
    /// <summary>
    /// Represents a WebSocket message with delivery tracking
    /// </summary>
    public class WebSocketMessage
    {
        /// <summary>
        /// Unique message identifier for tracking delivery
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// The type of message
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = "message";

        /// <summary>
        /// The sender user ID
        /// </summary>
        [JsonPropertyName("from")]
        public string From { get; set; }

        /// <summary>
        /// The recipient user ID (null for broadcasts)
        /// </summary>
        [JsonPropertyName("to")]
        public string To { get; set; }

        /// <summary>
        /// The message content
        /// </summary>
        [JsonPropertyName("content")]
        public string Content { get; set; }

        /// <summary>
        /// Timestamp when the message was created
        /// </summary>
        [JsonPropertyName("timestamp")]
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Whether this message requires acknowledgment
        /// </summary>
        [JsonPropertyName("requiresAck")]
        public bool RequiresAcknowledgment { get; set; } = true;

        /// <summary>
        /// Correlation ID for tracking related messages
        /// </summary>
        [JsonPropertyName("correlationId")]
        public string CorrelationId { get; set; }

        /// <summary>
        /// Reference to another message (for replies)
        /// </summary>
        [JsonPropertyName("replyTo")]
        public string ReplyToId { get; set; }
        
        /// <summary>
        /// The message type (chat, system, notification)
        /// </summary>
        [JsonPropertyName("messageType")]
        public MessageType MessageType { get; set; } = MessageType.Chat;
        
        /// <summary>
        /// The message status (sent, delivered, read)
        /// </summary>
        [JsonPropertyName("status")]
        public MessageStatus Status { get; set; } = MessageStatus.Sent;

        /// <summary>
        /// Creates a new WebSocket message
        /// </summary>
        public WebSocketMessage()
        {
        }

        /// <summary>
        /// Creates a new WebSocket message with specified parameters
        /// </summary>
        public WebSocketMessage(string type, string from, string to, string content)
        {
            Type = type;
            From = from;
            To = to;
            Content = content;
        }
        
        /// <summary>
        /// Creates a new WebSocket message from a Message model
        /// </summary>
        public static WebSocketMessage FromMessage(MessageEntity message)
        {
            return new WebSocketMessage
            {
                Type = "message",
                From = message.SenderID.ToString(),
                To = message.ReceiverID.ToString(),
                Content = message.MessageText,
                MessageType = message.MessageType,
                Status = message.Status
            };
        }

        /// <summary>
        /// Creates an acknowledgment message for this message
        /// </summary>
        public WebSocketMessage CreateAcknowledgment()
        {
            return new WebSocketMessage
            {
                Type = "ack",
                From = this.To,
                To = this.From,
                Content = this.Id,
                RequiresAcknowledgment = false,
                CorrelationId = this.CorrelationId,
                ReplyToId = this.Id
            };
        }

        /// <summary>
        /// Creates a receipt confirmation message for this message
        /// </summary>
        public WebSocketMessage CreateReceipt(MessageStatus status = MessageStatus.Delivered)
        {
            string statusStr = status.ToString().ToLower();
            
            return new WebSocketMessage
            {
                Type = "receipt",
                From = this.To,
                To = this.From,
                Content = $"{statusStr}:{this.Id}",
                RequiresAcknowledgment = false,
                CorrelationId = this.CorrelationId,
                ReplyToId = this.Id,
                Status = status
            };
        }
        
        /// <summary>
        /// Converts this WebSocketMessage to a Message model
        /// </summary>
        public MessageEntity ToMessage()
        {
            int.TryParse(From, out int senderId);
            int.TryParse(To, out int receiverId);
            
            return new MessageEntity
            {
                SenderID = senderId,
                ReceiverID = receiverId,
                MessageText = Content,
                Timestamp = Timestamp.DateTime,
                MessageType = MessageType,
                Status = Status,
                IsDelivered = Status >= MessageStatus.Delivered,
                IsRead = Status == MessageStatus.Read
            };
        }
    }
} 