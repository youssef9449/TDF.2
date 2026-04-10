using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TDFMAUI.Config;
using TDFMAUI.Converters;
using TDFMAUI.Pages;
using TDFMAUI.Services;
using TDFMAUI.ViewModels;
using Microsoft.Extensions.Configuration;
using System.Reflection;
using TDFMAUI.Features.Dashboard;
using TDFMAUI.Features.Auth;
using TDFMAUI.Features.Requests;
using TDFMAUI.Features.Admin;
using CommunityToolkit.Maui;
using System.Diagnostics;
using TDFShared.Constants;
using TDFShared.Validation;
using TDFShared.Services;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Storage;
#if ANDROID || IOS
using Plugin.Firebase.CloudMessaging;
using Plugin.LocalNotification;
using Plugin.LocalNotification.EventArgs;
using Plugin.FirebasePushNotifications;
using Plugin.Firebase.Auth;
#endif


namespace TDFMAUI
{
    public static class MauiProgram
    {
#if !WINDOWS && !MACCATALYST
        private static void OnLocalNotificationReceived(NotificationEventArgs e)
        {
            try
            {
                var trackingId = e.Request.ReturningData;
                if (!string.IsNullOrEmpty(trackingId))
                {
                    var notificationService = Application.Current?.Handler?.MauiContext?.Services?.GetService<IPlatformNotificationService>();
                    if (notificationService != null)
                    {
                        Task.Run(async () => {
                            await ((PlatformNotificationService)notificationService).UpdateNotificationDeliveryStatusAsync(trackingId, true);
                        });
                    }
                }
            }
            catch (Exception ex) { Debug.WriteLine($"Error in OnLocalNotificationReceived: {ex.Message}"); }
        }
        
        private static void OnLocalNotificationTapped(NotificationEventArgs e)
        {
            try
            {
                var trackingId = e.Request.ReturningData;
                if (!string.IsNullOrEmpty(trackingId))
                {
                    var notificationService = Application.Current?.Handler?.MauiContext?.Services?.GetService<IPlatformNotificationService>();
                    if (notificationService != null)
                    {
                        Task.Run(async () => {
                            await ((PlatformNotificationService)notificationService).UpdateNotificationDeliveryStatusAsync(trackingId, true);
                        });
                    }
                }
            }
            catch (Exception ex) { Debug.WriteLine($"Error in OnLocalNotificationTapped: {ex.Message}"); }
        }
#else
        private static void OnLocalNotificationReceived(object e) { }
        private static void OnLocalNotificationTapped(object e) { }
#endif
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp(serviceProvider => serviceProvider.GetRequiredService<App>())
                .UseMauiCommunityToolkit()
#if !WINDOWS && !MACCATALYST
                .UseFirebasePushNotifications()
                .UseLocalNotification(config => {
                    config.AddCategory(new Plugin.LocalNotification.NotificationCategory(Plugin.LocalNotification.NotificationCategoryType.Status));
                    Plugin.LocalNotification.LocalNotificationCenter.Current.NotificationReceived += OnLocalNotificationReceived;
                    Plugin.LocalNotification.LocalNotificationCenter.Current.NotificationActionTapped += OnLocalNotificationTapped;
                })
#endif
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSans-Regular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSans-Semibold");
                    fonts.AddFont("materialdesignicons-webfont.ttf", "MaterialDesignIcons");
                });

            try
            {
                var configBuilder = new ConfigurationBuilder();
                bool configLoaded = false;
                string appSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

                try
                {
                    if (File.Exists(appSettingsPath))
                    {
                        configBuilder.AddJsonFile(appSettingsPath, optional: false);
                        configLoaded = true;
                    }
                }
                catch { }

                if (!configLoaded)
                {
                    ApiConfig.BaseUrl = $"https://192.168.100.3:5001/";
                    ApiConfig.WebSocketUrl = $"wss://192.168.100.3:5001{ApiRoutes.WebSocket.Base}";
                    ApiConfig.Timeout = 30;
                    ApiConfig.DevelopmentMode = true;
                }

                var config = configBuilder.Build();
                builder.Configuration.AddConfiguration(config);

                builder.Services.Configure<ApiSettings>(options =>
                {
                    bool isDevelopment = bool.TryParse(builder.Configuration["ApiSettings:DevelopmentMode"], out bool devMode) ? devMode : true;
                    string section = isDevelopment ? "ApiSettings:Development" : "ApiSettings:Production";
                    options.BaseUrl = builder.Configuration[$"{section}:BaseUrl"] ?? "https://192.168.100.3:5001/";
                    options.WebSocketUrl = builder.Configuration[$"{section}:WebSocketUrl"] ?? $"wss://192.168.100.3:5001{ApiRoutes.WebSocket.Base}";
                    options.Timeout = int.TryParse(builder.Configuration["ApiSettings:Timeout"], out int timeout) ? timeout : 30;
                    options.DevelopmentMode = isDevelopment;
                });
            }
            catch { throw; }

#if DEBUG
            builder.Logging.AddDebug();
            builder.Services.AddLogging(lb => { lb.AddDebug(); lb.SetMinimumLevel(LogLevel.Information); });
#else
            builder.Services.AddLogging(lb => { lb.AddDebug(); lb.SetMinimumLevel(LogLevel.Warning); });
#endif

            builder.Services.AddSingleton<WebSocketService>();
            builder.Services.AddSingleton<IWebSocketService>(sp => sp.GetRequiredService<WebSocketService>());
            builder.Services.AddSingleton<ApiService>();
            builder.Services.AddSingleton<IApiService>(sp => sp.GetRequiredService<ApiService>());
            builder.Services.AddSingleton<SecureStorageService>();
            builder.Services.AddSingleton(SecureStorage.Default);
            builder.Services.AddSingleton<NetworkService>();
            builder.Services.AddSingleton<LocalStorageService>();
            builder.Services.AddSingleton<ILocalStorageService, LocalStorageService>();
            builder.Services.AddSingleton<IUserPresenceApiService, UserPresenceApiService>();
            builder.Services.AddSingleton<IUserPresenceEventsService, UserPresenceEventsService>();
            builder.Services.AddSingleton<IUserPresenceCacheService, UserPresenceCacheService>();
            builder.Services.AddSingleton<IUserPresenceService, UserPresenceService>();
            builder.Services.AddSingleton<IUserProfileService, UserProfileService>();
            builder.Services.AddSingleton<PanelStateService>();
            builder.Services.AddSingleton<LookupService>();
            builder.Services.AddSingleton<ILookupService>(sp => sp.GetRequiredService<LookupService>());
            builder.Services.AddSingleton<DevelopmentHttpClientHandler>();
            builder.Services.AddTransient<IRequestService, RequestService>();
            builder.Services.AddSingleton<IRoleService, RoleService>();
            builder.Services.AddSingleton<ISecurityService, SecurityService>();
            builder.Services.AddSingleton<IErrorHandlingService, ErrorHandlingService>();

            builder.Services.AddHttpClient<IHttpClientService, HttpClientService>((sp, client) =>
            {
                var apiSettings = sp.GetRequiredService<IOptions<ApiSettings>>().Value;
                client.BaseAddress = new Uri(apiSettings.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(apiSettings.Timeout);
            })
            .ConfigurePrimaryHttpMessageHandler(sp => sp.GetRequiredService<DevelopmentHttpClientHandler>());

            builder.Services.AddSingleton<TDFMAUI.Services.ConnectivityService>();
            builder.Services.AddSingleton<TDFShared.Services.IConnectivityService>(sp => sp.GetRequiredService<TDFMAUI.Services.ConnectivityService>());
            builder.Services.AddSingleton<ThemeService>();
            builder.Services.AddSingleton<IValidationService, ValidationService>();
            builder.Services.AddSingleton<IBusinessRulesService, BusinessRulesService>();
            builder.Services.AddSingleton<IUserSessionService, UserSessionService>();
            builder.Services.AddSingleton<App>();
            builder.Services.AddSingleton<AppShell>();
            builder.Services.AddSingleton<AuthService>();
            builder.Services.AddSingleton<IAuthService>(sp => sp.GetRequiredService<AuthService>());
            builder.Services.AddSingleton<MessageService>();
            builder.Services.AddSingleton<IPlatformNotificationService, PlatformNotificationService>();
            builder.Services.AddSingleton<NotificationService>();
            builder.Services.AddSingleton<IExtendedNotificationService>(sp => sp.GetRequiredService<NotificationService>());
            builder.Services.AddSingleton<Services.INotificationService>(sp => sp.GetRequiredService<IExtendedNotificationService>());
            
#if WINDOWS || MACCATALYST
            builder.Services.AddSingleton<IPushNotificationService, NoOpPushNotificationService>();
#else
            builder.Services.AddSingleton<PushNotificationService>();
            builder.Services.AddSingleton<IPushNotificationService>(sp => sp.GetRequiredService<PushNotificationService>());
#endif

            // Register ViewModels
            builder.Services.AddTransient<UserProfileViewModel>();
            builder.Services.AddTransient<AddRequestViewModel>();
            builder.Services.AddTransient<DashboardViewModel>();
            builder.Services.AddTransient<LoginPageViewModel>();
            builder.Services.AddTransient<MessagesViewModel>();
            builder.Services.AddTransient<MyTeamViewModel>();
            builder.Services.AddTransient<PrivateMessagesViewModel>();
            builder.Services.AddTransient<RequestApprovalViewModel>();
            builder.Services.AddTransient<RequestsViewModel>();
            builder.Services.AddTransient<ReportsViewModel>();
            builder.Services.AddTransient<SignupViewModel>();
            builder.Services.AddTransient<RequestDetailsViewModel>();
            builder.Services.AddTransient<AdminViewModel>();
            builder.Services.AddTransient<NotificationsViewModel>();
            builder.Services.AddTransient<UsersViewModel>();
            builder.Services.AddTransient<AddUserViewModel>();
            builder.Services.AddTransient<EditUserViewModel>();
            builder.Services.AddTransient<UsersRightPanelViewModel>();

            // Register Converters
            builder.Services.AddSingleton<BoolToThicknessConverter>();
            builder.Services.AddSingleton<StringNotEmptyConverter>();
            builder.Services.AddSingleton<BooleanInverter>();
            builder.Services.AddSingleton<InverseBoolConverter>();
            builder.Services.AddSingleton<BooleanToVisibilityConverter>();
            builder.Services.AddSingleton<LeaveTypeToTimePickersVisibilityConverter>();
            builder.Services.AddSingleton<ValidationStateToColorConverter>();
            builder.Services.AddSingleton<StatusToColorConverter>();

            // Register Pages
            builder.Services.AddTransient<MessagesPage>();
            builder.Services.AddTransient<NewMessagePage>();
            builder.Services.AddTransient<RequestsPage>();
            builder.Services.AddTransient<AdminPage>();
            builder.Services.AddTransient<AddRequestPage>();
            builder.Services.AddTransient<DiagnosticsPage>();
            builder.Services.AddTransient<StartupDiagnosticPage>();
            builder.Services.AddTransient<UsersPage>();
            builder.Services.AddTransient<UserProfilePage>();
            builder.Services.AddTransient<NotificationsPage>();
            builder.Services.AddTransient<NotificationTestPage>();
            builder.Services.AddTransient<SignupPage>();
            builder.Services.AddTransient<RequestDetailsPage>();
            builder.Services.AddTransient<LoginPage>();
            builder.Services.AddTransient<DashboardPage>();
            builder.Services.AddTransient<PrivateMessagesPage>();
            builder.Services.AddTransient<GlobalMessagesPage>();
            builder.Services.AddTransient<ReportsPage>();
            builder.Services.AddTransient<MyTeamPage>();
            builder.Services.AddTransient<GlobalChatPage>();
            builder.Services.AddTransient<AddUserPage>();
            builder.Services.AddTransient<EditUserPage>();
            builder.Services.AddTransient<RequestApprovalPage>();

            #if IOS
            builder.Services.AddSingleton<INotificationPermissionPlatformService, TDFMAUI.Platforms.iOS.NotificationPermissionPlatformService>();
            #endif

            var app = builder.Build();
            App.Services = app.Services;
            DebugService.Initialize();

            var userSessionService = app.Services.GetRequiredService<IUserSessionService>();
            App.InitializeUserSession(userSessionService);
            ApiConfig.ConnectUserSessionService(userSessionService);
            _ = Task.Run(async () => await userSessionService.InitializeAsync());
            ApiConfig.Initialize(isDevelopment: true);

            var connectivityService = app.Services.GetService<IConnectivityService>();
            if (connectivityService != null)
            {
                connectivityService.ConnectivityChanged += async (sender, e) =>
                {
                    var apiService = app.Services.GetService<ApiService>();
                    var webSocketService = app.Services.GetService<WebSocketService>();
                    if (e.IsConnected)
                    {
                        if (apiService != null) await apiService.TestConnectivityAsync();
                        if (webSocketService != null) await webSocketService.ConnectAsync();
                    }
                    else if (webSocketService != null) await webSocketService.DisconnectAsync();
                };
            }

            return app;
        }
    }
}
