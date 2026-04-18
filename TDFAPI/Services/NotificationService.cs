using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using TDFAPI.Repositories;
using TDFShared.DTOs.Messages;
using TDFShared.Enums;
using TDFShared.Models.Notification;
using TDFAPI.Messaging;

namespace TDFAPI.Services
{
    public class NotificationService : INotificationDispatchService
    {
        private readonly INotificationRepository _notificationRepository;
        private readonly IUserRepository _userRepository;
        private readonly WebSocketConnectionManager _webSocketManager;
        private readonly ILogger<NotificationService> _logger;
        private readonly IPushTokenService _pushTokenService;
        private readonly IBackgroundJobService _jobService;
        private readonly MediatR.IMediator _mediator;

        public NotificationService(
            INotificationRepository notificationRepository,
            IUserRepository userRepository,
            WebSocketConnectionManager webSocketManager,
            ILogger<NotificationService> logger,
            IPushTokenService pushTokenService,
            IBackgroundJobService jobService,
            MediatR.IMediator mediator)
        {
            _notificationRepository = notificationRepository;
            _userRepository = userRepository;
            _webSocketManager = webSocketManager;
            _logger = logger;
            _pushTokenService = pushTokenService;
            _jobService = jobService;
            _mediator = mediator;
        }

        public async Task<IEnumerable<NotificationEntity>> GetUnreadNotificationsAsync(int userId)
        {
            return await _notificationRepository.GetUnreadNotificationsAsync(userId);
        }

        public async Task<bool> MarkAsSeenAsync(int notificationId, int userId)
        {
            return await _mediator.Send(new TDFAPI.CQRS.Commands.MarkNotificationAsSeenCommand { NotificationId = notificationId, UserId = userId });
        }

        public async Task<bool> MarkNotificationsAsSeenAsync(IEnumerable<int> notificationIds, int userId)
        {
            bool allMarked = true;
            foreach (var id in notificationIds)
            {
                if (!await MarkAsSeenAsync(id, userId)) allMarked = false;
            }
            return allMarked;
        }

        public async Task<bool> CreateNotificationAsync(int receiverId, string message, int? senderId = null)
        {
            try
            {
                var notification = new NotificationEntity
                {
                    ReceiverID = receiverId,
                    SenderID = senderId,
                    Message = message,
                    IsSeen = false,
                    Timestamp = DateTime.UtcNow
                };
                await _notificationRepository.CreateNotificationAsync(notification);

                // Notify via WebSocket
                await _webSocketManager.SendToAsync(receiverId, new NotificationDto
                {
                    NotificationId = notification.NotificationID,
                    UserId = receiverId,
                    SenderId = senderId,
                    Message = message,
                    Timestamp = notification.Timestamp,
                    Title = "Notification"
                });

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating notification for {ReceiverId}", receiverId);
                return false;
            }
        }

        public async Task SendNotificationAsync(int userId, string title, string message, NotificationType type = NotificationType.Info, string? data = null)
        {
            try
            {
                var notification = new NotificationEntity
                {
                    ReceiverID = userId,
                    Message = message,
                    IsSeen = false,
                    Timestamp = DateTime.UtcNow
                };
                await _notificationRepository.CreateNotificationAsync(notification);

                await _webSocketManager.SendToAsync(userId, new NotificationDto
                {
                    NotificationId = notification.NotificationID,
                    UserId = userId,
                    Title = title,
                    Message = message,
                    NotificationType = type,
                    Timestamp = notification.Timestamp,
                    Data = data
                });

                // Trigger push notification if needed
                _ = SendPushNotificationIfNeededAsync(userId, notification, title, type, data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notification to user {UserId}", userId);
            }
        }

        public async Task SendNotificationAsync(IEnumerable<int> userIds, string title, string message, NotificationType type = NotificationType.Info, string? data = null)
        {
            foreach (var userId in userIds) await SendNotificationAsync(userId, title, message, type, data);
        }

        public async Task SendDepartmentNotificationAsync(string department, string title, string message, NotificationType type = NotificationType.Info, string? data = null)
        {
            var users = await _userRepository.GetUsersByDepartmentAsync(department);
            await SendNotificationAsync(users.Select(u => u.UserID), title, message, type, data);
        }

        public async Task ScheduleNotificationAsync(int userId, string title, string message, DateTime deliveryTime, NotificationType type = NotificationType.Info, string? data = null)
        {
            await _jobService.ScheduleJobAsync(
                "SendNotification",
                new Dictionary<string, object>
                {
                    { "userId", userId },
                    { "title", title },
                    { "message", message },
                    { "type", (int)type },
                    { "data", data ?? string.Empty }
                },
                deliveryTime);
        }

        public async Task CancelScheduledNotificationAsync(string notificationId)
        {
            await _jobService.DeleteJobAsync("SendNotification", notificationId);
        }

        public async Task<IEnumerable<NotificationRecord>> GetScheduledNotificationsAsync(int userId)
        {
            var jobs = await _jobService.GetJobsAsync("SendNotification", userId.ToString());
            return jobs.Select(job => new NotificationRecord
            {
                Id = job.Id,
                Title = job.Data["title"].ToString()!,
                Message = job.Data["message"].ToString()!,
                Type = (NotificationType)Convert.ToInt32(job.Data["type"]),
                Timestamp = job.ScheduledTime,
                Data = job.Data["data"]?.ToString()
            });
        }

        public async Task<bool> DeleteNotificationAsync(int notificationId, int userId)
        {
            return await _mediator.Send(new TDFAPI.CQRS.Commands.DeleteNotificationCommand { NotificationId = notificationId, UserId = userId });
        }

        private async Task SendPushNotificationIfNeededAsync(int userId, NotificationEntity notification, string title, NotificationType type, string? data)
        {
            try
            {
                var tokens = await _pushTokenService.GetUserTokensAsync(userId);
                foreach (var token in tokens)
                {
                    // Logic to send push notification via FCM/APNS would go here
                    _logger.LogInformation("Sending push notification to {Platform} for user {UserId}", token.Platform, userId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending push notification for user {UserId}", userId);
            }
        }
    }
}
