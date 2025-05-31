using Android.App;
using Android.Content;
using Firebase.Messaging;
using Microsoft.Extensions.Logging;
using Plugin.LocalNotification;
using System;
using System.Threading.Tasks;
using TDFMAUI.Services; // For IPushNotificationService, ILocalStorageService
using TDFShared.DTOs.Messages;
using TDFShared.Enums;

namespace TDFMAUI.Platforms.Android.Services
{
    [Service(Exported = true)]
    [IntentFilter(new[] { "com.google.firebase.MESSAGING_EVENT" })]
    public class FirebaseMessagingServiceImpl : FirebaseMessagingService
    {
        private const string TAG = "FirebaseMessagingService";
        private ILogger<FirebaseMessagingServiceImpl> _logger;
        private IPushNotificationService _pushNotificationService;
        private ILocalStorageService _localStorage;

        public override void OnCreate()
        {
            base.OnCreate();
            var services = MauiApplication.Current.Services;
            _logger = services.GetService(typeof(ILogger<FirebaseMessagingServiceImpl>)) as ILogger<FirebaseMessagingServiceImpl>;
            _pushNotificationService = services.GetService(typeof(IPushNotificationService)) as IPushNotificationService;
            _localStorage = services.GetService(typeof(ILocalStorageService)) as ILocalStorageService;
            _logger?.LogInformation("FirebaseMessagingService created");
        }

        public override void OnNewToken(string token)
        {
            _logger?.LogInformation("FirebaseMessagingService: New FCM token received: {Token}", token);
            Task.Run(async () =>
            {
                try
                {
                    if (_localStorage != null)
                        await _localStorage.SetItemAsync("fcm_token", token);

                    if (_pushNotificationService != null)
                        await _pushNotificationService.RegisterTokenAsync();
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error handling new FCM token");
                }
            });
        }

        public override void OnMessageReceived(RemoteMessage message)
        {
            _logger?.LogInformation("FirebaseMessagingService: Message received from: {From}", message.From);

            try
            {
                var notification = message.GetNotification();
                var data = message.Data;

                string title = notification?.Title ?? (data != null && data.TryGetValue("title", out var t) ? t : "New Notification");
                string body = notification?.Body ?? (data != null && data.TryGetValue("body", out var b) ? b : "");
                string notificationTypeStr = data != null && data.TryGetValue("notificationType", out var nt) ? nt : "Info";
                string notificationId = data != null && data.TryGetValue("notificationId", out var nid) ? nid : Guid.NewGuid().ToString();
                string extraData = data != null && data.TryGetValue("data", out var ed) ? ed : string.Empty;

                NotificationType notificationType = NotificationType.Info;
                Enum.TryParse(notificationTypeStr, true, out notificationType);

                var notificationRequest = new NotificationRequest
                {
                    NotificationId = int.TryParse(notificationId, out var id) ? id : new Random().Next(100000, 999999),
                    Title = title,
                    Description = body,
                    ReturningData = extraData
                };

                LocalNotificationCenter.Current.Show(notificationRequest);

            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error processing FCM message");
            }
        }
    }
}