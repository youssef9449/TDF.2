using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
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
        private static readonly object _firebaseInitLock = new();
        private static bool _firebaseInitialized;
        private static bool _firebaseUnavailable;

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

                // Notify via WebSocket (fans out to all live connections of the user —
                // desktop app and any mobile device that's currently online).
                await _webSocketManager.SendToAsync(receiverId, new NotificationDto
                {
                    NotificationId = notification.NotificationID,
                    UserId = receiverId,
                    SenderId = senderId,
                    Message = message,
                    Timestamp = notification.Timestamp,
                    Title = "Notification"
                });

                // Fire-and-forget push so a mobile device that's not currently on
                // WebSocket still gets the notification.
                _ = SendPushNotificationIfNeededAsync(receiverId, notification, "Notification", NotificationType.Info, null);

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
                var tokens = (await _pushTokenService.GetUserTokensAsync(userId)).ToList();
                if (tokens.Count == 0) return;

                if (!EnsureFirebaseInitialized()) return;

                var messaging = FirebaseMessaging.DefaultInstance;
                foreach (var token in tokens)
                {
                    if (string.IsNullOrWhiteSpace(token.Token)) continue;
                    try
                    {
                        var fcmMessage = new Message
                        {
                            Token = token.Token,
                            Notification = new FirebaseAdmin.Messaging.Notification
                            {
                                Title = title,
                                Body = notification.Message
                            },
                            Data = new Dictionary<string, string>
                            {
                                ["notificationId"] = notification.NotificationID.ToString(),
                                ["type"] = ((int)type).ToString(),
                                ["data"] = data ?? string.Empty
                            }
                        };

                        var messageId = await messaging.SendAsync(fcmMessage);
                        _logger.LogInformation("Sent FCM message {MessageId} to {Platform} token for user {UserId}",
                            messageId, token.Platform, userId);
                    }
                    catch (FirebaseMessagingException fex) when (
                        fex.MessagingErrorCode == MessagingErrorCode.Unregistered ||
                        fex.MessagingErrorCode == MessagingErrorCode.SenderIdMismatch)
                    {
                        _logger.LogWarning(fex, "Removing stale push token for user {UserId} ({Platform})", userId, token.Platform);
                        await _pushTokenService.UnregisterTokenAsync(userId, token.Token);
                    }
                    catch (Exception tokenEx)
                    {
                        _logger.LogError(tokenEx, "Failed to send FCM message to {Platform} token for user {UserId}",
                            token.Platform, userId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending push notification for user {UserId}", userId);
            }
        }

        private bool EnsureFirebaseInitialized()
        {
            if (_firebaseInitialized) return true;
            if (_firebaseUnavailable) return false;

            lock (_firebaseInitLock)
            {
                if (_firebaseInitialized) return true;
                if (_firebaseUnavailable) return false;

                try
                {
                    if (FirebaseApp.DefaultInstance == null)
                    {
                        var credentialPath = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS");
                        if (string.IsNullOrWhiteSpace(credentialPath) || !File.Exists(credentialPath))
                        {
                            var fallback = Path.Combine(AppContext.BaseDirectory, "google-service-account.json");
                            if (File.Exists(fallback)) credentialPath = fallback;
                        }

                        GoogleCredential credential;
                        if (!string.IsNullOrWhiteSpace(credentialPath) && File.Exists(credentialPath))
                        {
                            credential = GoogleCredential.FromFile(credentialPath);
                        }
                        else
                        {
                            // Falls back to ADC (e.g. metadata server in GCP); if this throws we
                            // treat Firebase as unavailable and log once.
                            credential = GoogleCredential.GetApplicationDefault();
                        }

                        FirebaseApp.Create(new AppOptions { Credential = credential });
                    }

                    _firebaseInitialized = true;
                    return true;
                }
                catch (Exception ex)
                {
                    _firebaseUnavailable = true;
                    _logger.LogWarning(ex,
                        "Firebase Admin SDK not initialized — push notifications disabled. " +
                        "Set GOOGLE_APPLICATION_CREDENTIALS or place google-service-account.json alongside the API binary.");
                    return false;
                }
            }
        }
    }
}
