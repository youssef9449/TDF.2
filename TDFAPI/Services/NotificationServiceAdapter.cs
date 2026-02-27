using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TDFShared.Services;
using TDFShared.DTOs.Messages;
using TDFShared.Models.Message;
using TDFShared.Models.Notification;

namespace TDFAPI.Services
{
    /// <summary>
    /// Adapter class that implements TDFShared.Services.INotificationService
    /// and delegates to TDFAPI.Services.INotificationService
    /// </summary>
    public class NotificationServiceAdapter : TDFShared.Services.INotificationService
    {
        private readonly TDFAPI.Services.INotificationService _notificationService;
        private readonly ILogger<NotificationServiceAdapter> _logger;

        public NotificationServiceAdapter(
            TDFAPI.Services.INotificationService notificationService,
            ILogger<NotificationServiceAdapter> logger)
        {
            _notificationService = notificationService;
            _logger = logger;
        }

        public event EventHandler<NotificationDto> NotificationReceived;

        public void Dispose()
        {
            // No resources to dispose
        }

        public async Task<bool> BroadcastNotificationAsync(string message, int? senderId = null, string? department = null)
        {
            try
            {
                return await _notificationService.BroadcastNotificationAsync(message, senderId, department);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in BroadcastNotificationAsync");
                return false;
            }
        }

        public async Task<bool> CreateNotificationAsync(int receiverId, string message, int? senderId = null)
        {
            try
            {
                return await _notificationService.CreateNotificationAsync(receiverId, message, senderId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CreateNotificationAsync");
                return false;
            }
        }

        public async Task<bool> DeleteConversationAsync(int otherUserId, int? currentUserId = null)
        {
            try
            {
                return await _notificationService.DeleteConversationAsync(otherUserId, currentUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DeleteConversationAsync");
                return false;
            }
        }

        public async Task<bool> DeleteMessageAsync(int messageId, int? currentUserId = null)
        {
            try
            {
                return await _notificationService.DeleteMessageAsync(messageId, currentUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DeleteMessageAsync");
                return false;
            }
        }

        public async Task<IEnumerable<MessageDto>> GetMessageHistoryAsync(int otherUserId, int? currentUserId = null, int page = 1, int pageSize = 50)
        {
            try
            {
                return await _notificationService.GetMessageHistoryAsync(otherUserId, currentUserId, page, pageSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetMessageHistoryAsync");
                return new List<MessageDto>();
            }
        }

        public async Task<IEnumerable<MessageDto>> GetUnreadMessagesAsync(int? userId = null)
        {
            try
            {
                return await _notificationService.GetUnreadMessagesAsync(userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetUnreadMessagesAsync");
                return new List<MessageDto>();
            }
        }

        public async Task<int> GetUnreadMessagesCountAsync(int? userId = null)
        {
            try
            {
                return await _notificationService.GetUnreadMessagesCountAsync(userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetUnreadMessagesCountAsync");
                return 0;
            }
        }

        public async Task<IEnumerable<NotificationEntity>> GetUnreadNotificationsAsync(int? userId = null)
        {
            try
            {
                return await _notificationService.GetUnreadNotificationsAsync(userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetUnreadNotificationsAsync");
                return new List<NotificationEntity>();
            }
        }

        public async Task HandleUserConnectionAsync(WebSocketConnectionEntity connection, WebSocket socket)
        {
            try
            {
                await _notificationService.HandleUserConnectionAsync(connection, socket);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in HandleUserConnectionAsync (socket)");
            }
        }

        public async Task<bool> HandleUserConnectionAsync(int userId, bool isConnected, string? machineName = null)
        {
            try
            {
                return await _notificationService.HandleUserConnectionAsync(userId, isConnected, machineName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in HandleUserConnectionAsync");
                return false;
            }
        }

        public async Task<bool> IsUserOnline(int userId)
        {
            try
            {
                return await _notificationService.IsUserOnline(userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in IsUserOnline");
                return false;
            }
        }

        public async Task<bool> MarkAsSeenAsync(int notificationId, int? userId = null)
        {
            try
            {
                return await _notificationService.MarkAsSeenAsync(notificationId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in MarkAsSeenAsync");
                return false;
            }
        }

        public async Task<bool> MarkMessagesAsDeliveredAsync(int senderId, int? currentUserId = null)
        {
            try
            {
                return await _notificationService.MarkMessagesAsDeliveredAsync(senderId, currentUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in MarkMessagesAsDeliveredAsync (sender)");
                return false;
            }
        }

        public async Task<bool> MarkMessagesAsDeliveredAsync(IEnumerable<int> messageIds, int? currentUserId = null)
        {
            try
            {
                return await _notificationService.MarkMessagesAsDeliveredAsync(messageIds, currentUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in MarkMessagesAsDeliveredAsync (ids)");
                return false;
            }
        }

        public async Task<bool> MarkMessagesAsReadAsync(int senderId, int? currentUserId = null)
        {
            try
            {
                return await _notificationService.MarkMessagesAsReadAsync(senderId, currentUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in MarkMessagesAsReadAsync");
                return false;
            }
        }

        public async Task<bool> MarkNotificationsAsSeenAsync(IEnumerable<int> notificationIds, int? userId = null)
        {
            try
            {
                return await _notificationService.MarkNotificationsAsSeenAsync(notificationIds, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in MarkNotificationsAsSeenAsync");
                return false;
            }
        }

        public async Task<bool> SendChatMessageAsync(int receiverId, string message, bool queueIfOffline = true)
        {
            try
            {
                return await _notificationService.SendChatMessageAsync(receiverId, message, queueIfOffline);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SendChatMessageAsync");
                return false;
            }
        }

        public async Task SendToAllAsync(object message, IEnumerable<string>? excludedConnections = null)
        {
            try
            {
                await _notificationService.SendToAllAsync(message, excludedConnections);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SendToAllAsync");
            }
        }

        public async Task SendToGroupAsync(string group, object message)
        {
            try
            {
                await _notificationService.SendToGroupAsync(group, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SendToGroupAsync");
            }
        }

        public async Task SendToUserAsync(int userId, object message)
        {
            try
            {
                await _notificationService.SendToUserAsync(userId, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SendToUserAsync");
            }
        }

        public async Task<bool> SendNotificationAsync(int receiverId, string message)
        {
            try
            {
                return await _notificationService.SendNotificationAsync(receiverId, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SendNotificationAsync");
                return false;
            }
        }

        public async Task ShowErrorAsync(string message)
        {
            await _notificationService.ShowErrorAsync(message);
        }

        public async Task ShowSuccessAsync(string message)
        {
            await _notificationService.ShowSuccessAsync(message);
        }

        public async Task ShowWarningAsync(string message)
        {
            await _notificationService.ShowWarningAsync(message);
        }
    }
}