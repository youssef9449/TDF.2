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
using Plugin.Firebase.Auth; // Added for Firebase Auth
#endif


namespace TDFMAUI
{
    public static class MauiProgram
    {
        // Event handlers for notification delivery tracking
#if !WINDOWS && !MACCATALYST
        private static void OnLocalNotificationReceived(NotificationEventArgs e)
        {
            try
            {
                // Get the notification ID and tracking data
                var notificationId = e.Request.NotificationId;
                var trackingId = e.Request.ReturningData;
                
                // Log notification delivery
                System.Diagnostics.Debug.WriteLine($"Notification received: ID={notificationId}, TrackingID={trackingId}");
                
                // Update delivery status in platform notification service
                // This will be handled by the service when it's initialized
                if (!string.IsNullOrEmpty(trackingId))
                {
                    // The PlatformNotificationService will handle this via its own event handlers
                    // We're adding this here as a backup to ensure delivery tracking
                    var notificationService = Application.Current?.Handler?.MauiContext?.Services?.GetService<IPlatformNotificationService>();
                    if (notificationService != null)
                    {
                        Task.Run(async () => {
                            bool success = await ((PlatformNotificationService)notificationService).UpdateNotificationDeliveryStatusAsync(trackingId, true);
                            if (!success) {
                               Debug.WriteLine($"Failed to update delivery status for received notification {trackingId}");
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in OnLocalNotificationReceived: {ex.Message}");
            }
        }
        
        private static void OnLocalNotificationTapped(NotificationEventArgs e)
        {
            try
            {
                // Get the notification ID and tracking data
                var notificationId = e.Request.NotificationId;
                var trackingId = e.Request.ReturningData;
                
                // Log notification interaction
                Debug.WriteLine($"Notification tapped: ID={notificationId}, TrackingID={trackingId}");
                
                // Update delivery status in platform notification service
                if (!string.IsNullOrEmpty(trackingId))
                {
                    // The PlatformNotificationService will handle this via its own event handlers
                    // We're adding this here as a backup to ensure delivery tracking
                    var notificationService = Application.Current?.Handler?.MauiContext?.Services?.GetService<IPlatformNotificationService>();
                    if (notificationService != null)
                    {
                        Task.Run(async () => {
                            bool success = await ((PlatformNotificationService)notificationService).UpdateNotificationDeliveryStatusAsync(trackingId, true);
                            if (!success) {
                                Debug.WriteLine($"Failed to update delivery status for tapped notification {trackingId}");
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in OnLocalNotificationTapped: {ex.Message}");
            }
        }
#else
        // Stub methods for Windows and macOS platforms
        private static void OnLocalNotificationReceived(object e)
        {
           Debug.WriteLine("Local notifications not supported on Windows or macOS platforms");
        }
        
        private static void OnLocalNotificationTapped(object e)
        {
          Debug.WriteLine("Local notifications not supported on Windows or macOS platforms");
        }
#endif
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
builder
                .UseMauiApp<App>()
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
                // Configuration
                var configBuilder = new ConfigurationBuilder();
                bool configLoaded = false;

                // First try to load from file system (prioritize this approach)
                string appSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

                try
                {
                    if (File.Exists(appSettingsPath))
                    {
                        configBuilder.AddJsonFile(appSettingsPath, optional: false);
                        System.Diagnostics.Debug.WriteLine($"Loaded configuration from file: {appSettingsPath}");
                        configLoaded = true;
                    }
                    else
                    {
                        // If file doesn't exist, try as embedded resource as fallback
                        var assembly = Assembly.GetExecutingAssembly();
                        var resourceNames = assembly.GetManifestResourceNames();
                        System.Diagnostics.Debug.WriteLine($"Available embedded resources: {string.Join(", ", resourceNames)}");

                        // Look for any resource that ends with appsettings.json
                        var configResourceName = resourceNames.FirstOrDefault(name => name.EndsWith("appsettings.json"));

                        if (!string.IsNullOrEmpty(configResourceName))
                        {
                            using (var stream = assembly.GetManifestResourceStream(configResourceName))
                            {
                                if (stream != null)
                                {
                                    using (var reader = new StreamReader(stream))
                                    {
                                        string jsonConfig = reader.ReadToEnd();
                                        if (!string.IsNullOrEmpty(jsonConfig))
                                        {
                                            // Create a temporary file with the config content
                                            var tempFile = Path.GetTempFileName();
                                            File.WriteAllText(tempFile, jsonConfig);

                                            // Load from the temp file
                                            configBuilder.AddJsonFile(tempFile, optional: false);
                                            Debug.WriteLine($"Loaded configuration from embedded resource via temp file");
                                            configLoaded = true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading configuration: {ex.Message}");
                }

                // If we couldn't load config from any source, use defaults
                if (!configLoaded)
                {
                    System.Diagnostics.Debug.WriteLine("Warning: appsettings.json not found, using default configuration");

                    // Set default API configuration
                    ApiConfig.BaseUrl = $"https://192.168.100.3:5001/";
                    ApiConfig.WebSocketUrl = $"wss://192.168.100.3:5001{ApiRoutes.WebSocket.Base}";
                    ApiConfig.Timeout = 30;
                    ApiConfig.DevelopmentMode = true;
                }

                var config = configBuilder.Build();
                builder.Configuration.AddConfiguration(config);

                // Load API configuration
                // Use an object class for Options pattern instead of the static ApiConfig
                builder.Services.Configure<ApiSettings>(options =>
                {
                    // Check if we're in development mode
                    bool isDevelopment = bool.TryParse(builder.Configuration["ApiSettings:DevelopmentMode"], out bool devMode) ? devMode : true;

                    // Get the appropriate base URL and websocket URL based on environment
                    string section = isDevelopment ? "ApiSettings:Development" : "ApiSettings:Production";

                    options.BaseUrl = builder.Configuration[$"{section}:BaseUrl"];
                    if (string.IsNullOrEmpty(options.BaseUrl))
                    {
                        options.BaseUrl = $"https://192.168.100.3:5001/"; // Default fallback
                        System.Diagnostics.Debug.WriteLine($"Warning: {section}:BaseUrl not found in configuration. Using default DI BaseUrl: {options.BaseUrl}");
                    }
                    options.WebSocketUrl = builder.Configuration[$"{section}:WebSocketUrl"];
                    if (string.IsNullOrEmpty(options.WebSocketUrl))
                    {
                        options.WebSocketUrl = $"wss://192.168.100.3:5001{ApiRoutes.WebSocket.Base}"; // Default fallback
                        System.Diagnostics.Debug.WriteLine($"Warning: {section}:WebSocketUrl not found in configuration. Using default DI WebSocketUrl: {options.WebSocketUrl}");
                    }
                    options.Timeout = int.TryParse(builder.Configuration["ApiSettings:Timeout"], out int timeout) ? timeout : 30;
                    options.DevelopmentMode = isDevelopment;
                });

                // Manually load into static class for easier access (consider singleton service instead)
                // URLs are the same for dev and prod, load them directly.
                // DevelopmentMode will be set by ApiConfig.Initialize to true to allow IP cert bypass.
                string configSection = "ApiSettings:Development"; // Or "ApiSettings:Production", as they are the same

                ApiConfig.BaseUrl = builder.Configuration[$"{configSection}:BaseUrl"];
                ApiConfig.WebSocketUrl = builder.Configuration[$"{configSection}:WebSocketUrl"];
                ApiConfig.Timeout = int.TryParse(builder.Configuration["ApiSettings:Timeout"], out int timeout) ? timeout : 30;

                // Add some debug info
                System.Diagnostics.Debug.WriteLine($"Config loaded for static ApiConfig: BaseUrl={ApiConfig.BaseUrl}, WebSocketUrl={ApiConfig.WebSocketUrl}");
            }
            catch (Exception ex)
            {
                // Log configuration loading errors critically
                System.Diagnostics.Debug.WriteLine($"CRITICAL ERROR during Configuration Loading: {ex}");
                HandleUnhandledException("ConfigurationLoading", ex);
                // Optionally rethrow or handle differently if needed,
                // but logging might be sufficient for diagnosis
                throw; // Rethrow to ensure the app doesn't proceed incorrectly configured
            }

            // Configure logging
#if DEBUG
            builder.Logging.AddDebug();

            // Set up logging level for debug mode
            builder.Services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddDebug();
                // Set minimum level to Information to see our diagnostic logs
                loggingBuilder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information);
            });
#else
            // In release mode, only log warnings and errors
            builder.Services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddDebug();
                loggingBuilder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Warning);
            });
#endif

            // Register MAUI-specific services
            // Note: MAUI services are automatically registered by the framework

            // Register core services
            builder.Services.AddSingleton<WebSocketService>();
            builder.Services.AddSingleton<IWebSocketService>(sp => sp.GetRequiredService<WebSocketService>());
            builder.Services.AddSingleton<ApiService>();
            builder.Services.AddSingleton<IApiService>(sp => sp.GetRequiredService<ApiService>());
            builder.Services.AddSingleton<SecureStorageService>();
            builder.Services.AddSingleton(SecureStorage.Default);
            builder.Services.AddSingleton<NetworkService>();
            builder.Services.AddSingleton<ILogger<ApiService>>(sp => 
                sp.GetRequiredService<ILoggerFactory>().CreateLogger<ApiService>());
            builder.Services.AddSingleton<ILogger<NetworkService>>(sp => 
                sp.GetRequiredService<ILoggerFactory>().CreateLogger<NetworkService>());
            builder.Services.AddSingleton<ILogger<WebSocketService>>(sp => 
                sp.GetRequiredService<ILoggerFactory>().CreateLogger<WebSocketService>());
            Console.WriteLine("[MauiProgram] we are before the service");            // Register LookupService

            builder.Services.AddSingleton<ILogger<TDFMAUI.Services.ConnectivityService>>(sp => 
                sp.GetRequiredService<ILoggerFactory>().CreateLogger<TDFMAUI.Services.ConnectivityService>());
            Console.WriteLine("[MauiProgram] we are past the service");            // Register LookupService
            builder.Services.AddSingleton<LocalStorageService>();
            builder.Services.AddSingleton<ILocalStorageService, LocalStorageService>();
            builder.Services.AddSingleton<IUserPresenceService, UserPresenceService>();
            builder.Services.AddSingleton<IUserProfileService, UserProfileService>();

            // Register NetworkService and IConnectivity
            builder.Services.AddSingleton<LookupService>();
            builder.Services.AddSingleton<ILookupService>(sp => sp.GetRequiredService<LookupService>());

            // Register DevelopmentHttpClientHandler for use with AddHttpClient
            builder.Services.AddSingleton<DevelopmentHttpClientHandler>();

            // Register RequestService
            builder.Services.AddTransient<IRequestService, RequestService>();

            // Register RoleService
            builder.Services.AddSingleton<IRoleService, RoleService>();

            // Register SecurityService
            builder.Services.AddSingleton<ISecurityService, SecurityService>();

            // Register shared error handling service
            builder.Services.AddSingleton<IErrorHandlingService, ErrorHandlingService>();

            // Register shared HTTP client services with DevelopmentHttpClientHandler
            builder.Services.AddHttpClient<IHttpClientService, HttpClientService>((serviceProvider, client) =>
            {
                var apiSettings = serviceProvider.GetRequiredService<IOptions<ApiSettings>>().Value;
                client.BaseAddress = new Uri(apiSettings.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(apiSettings.Timeout);
                client.DefaultRequestHeaders.Add("User-Agent", "TDF-MAUI/1.0");
            })
            .ConfigurePrimaryHttpMessageHandler(serviceProvider =>
            {
                var handler = serviceProvider.GetRequiredService<DevelopmentHttpClientHandler>();
                var logger = serviceProvider.GetRequiredService<ILogger<HttpClientService>>();
                logger.LogInformation("Configuring HttpClient with DevelopmentHttpClientHandler for SSL bypass");
                return handler;
            });

            // Register platform-specific connectivity service with shared interface
            builder.Services.AddSingleton<TDFMAUI.Services.ConnectivityService>();
            builder.Services.AddSingleton<TDFShared.Services.IConnectivityService>(sp => sp.GetRequiredService<TDFMAUI.Services.ConnectivityService>());

            // Register theme service for platform-aware theme adaptation
            builder.Services.AddSingleton<ThemeService>();

            // Register shared validation services
            builder.Services.AddSingleton<IValidationService, ValidationService>();
            builder.Services.AddSingleton<IBusinessRulesService, BusinessRulesService>();

            // Register centralized user session service
            builder.Services.AddSingleton<IUserSessionService, UserSessionService>();

            // Register AuthService with shared HTTP client service
            builder.Services.AddSingleton<AuthService>();
            builder.Services.AddSingleton<IAuthService>(sp => sp.GetRequiredService<AuthService>());

            // Register MessageService with shared HTTP client service
            builder.Services.AddSingleton<MessageService>();

            // Register NotificationService hierarchy
            builder.Services.AddSingleton<IPlatformNotificationService, PlatformNotificationService>();
            builder.Services.AddSingleton<NotificationService>();
            builder.Services.AddSingleton<IExtendedNotificationService>(sp => sp.GetRequiredService<NotificationService>());
            builder.Services.AddSingleton<Services.INotificationService>(sp => sp.GetRequiredService<IExtendedNotificationService>());
            
            // Register push notification service
            builder.Services.AddSingleton<PushNotificationService>();
            builder.Services.AddSingleton<IPushNotificationService>(sp => sp.GetRequiredService<PushNotificationService>());

            // Register ViewModels
            builder.Services.AddTransient<UserProfileViewModel>();
            builder.Services.AddTransient<AddRequestViewModel>();
            builder.Services.AddTransient<DashboardViewModel>();
            builder.Services.AddTransient<LoginPageViewModel>();
            builder.Services.AddTransient<MessagesViewModel>();
            builder.Services.AddTransient<MyTeamViewModel>();
            builder.Services.AddTransient<PrivateMessagesViewModel>();
            builder.Services.AddTransient<RequestApprovalViewModel>();
            builder.Services.AddTransient<RequestsViewModel>(sp => 
                new RequestsViewModel(
                    sp.GetRequiredService<IRequestService>(),
                    sp.GetRequiredService<IAuthService>(),
                    sp.GetRequiredService<ILogger<RequestsViewModel>>(),
                    sp.GetRequiredService<IErrorHandlingService>(),
                    sp.GetRequiredService<ILookupService>()
                ));
            builder.Services.AddTransient<ReportsViewModel>();
            builder.Services.AddTransient<SignupViewModel>();
            builder.Services.AddTransient<RequestDetailsViewModel>();

            // Register converters for use in XAML
            builder.Services.AddSingleton<BoolToThicknessConverter>();
            builder.Services.AddSingleton<StringNotEmptyConverter>();
            builder.Services.AddSingleton<BooleanInverter>();
            builder.Services.AddSingleton<InverseBoolConverter>();  // Added for AddRequestPage.xaml
            builder.Services.AddSingleton<BooleanToVisibilityConverter>();
            builder.Services.AddSingleton<LeaveTypeToTimePickersVisibilityConverter>();
            builder.Services.AddSingleton<ValidationStateToColorConverter>();  // Added for AddRequestPage.xaml
            builder.Services.AddSingleton<StatusToColorConverter>();  // Added for DashboardPage.xaml

            // Register pages with dependencies
            builder.Services.AddTransient<MessagesPage>();
            builder.Services.AddTransient<NewMessagePage>();
            builder.Services.AddTransient<RequestsPage>();
            builder.Services.AddTransient<AdminPage>();
            builder.Services.AddTransient<AddRequestPage>();
            builder.Services.AddTransient<DiagnosticsPage>(); // Add our new diagnostics page
            builder.Services.AddTransient<UsersPage>();
            builder.Services.AddTransient<UserProfilePage>();
            builder.Services.AddTransient<NotificationsPage>();
            builder.Services.AddTransient<NotificationTestPage>();
            builder.Services.AddTransient<SignupPage>();
            builder.Services.AddTransient<RequestDetailsPage>();
            builder.Services.AddTransient<LoginPage>();
            builder.Services.AddTransient<DashboardPage>(); // Add DashboardPage

            // Add missing pages used in AppShell.xaml
            builder.Services.AddTransient<MainPage>();
            builder.Services.AddTransient<PrivateMessagesPage>();
            builder.Services.AddTransient<GlobalMessagesPage>();
            builder.Services.AddTransient<ReportsPage>();
            builder.Services.AddTransient<MyTeamPage>();
            builder.Services.AddTransient<GlobalChatPage>();

            // Register AppShell
            builder.Services.AddTransient<AppShell>();

            // HTTP client service already registered above with TDFShared.Services.IHttpClientService
            // ConnectivityService already registered above with TDFShared.Services.IConnectivityService

            #if IOS
            builder.Services.AddSingleton<INotificationPermissionPlatformService, TDFMAUI.Platforms.iOS.NotificationPermissionPlatformService>();
            #endif

            MauiApp app = null; // Declare app outside try block
            try // Wrap builder.Build() and subsequent initialization
            {
                // After building the app, initialize network monitoring
                var logger = builder.Services.BuildServiceProvider().GetService<ILogger<MauiApp>>();
                logger?.LogInformation("Building MauiApp from builder...");

                try
                {
                    app = builder.Build();
                    logger?.LogInformation("MauiApp built successfully, initializing services...");
                }
                catch (Exception ex)
                {
                    logger?.LogCritical(ex, "CRITICAL ERROR during builder.Build()");
                    System.Diagnostics.Debug.WriteLine($"CRITICAL BUILD ERROR: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                    throw; // Re-throw after logging
                }

                // Initialize DebugService - THIS IS THE FIRST INITIALIZATION POINT
                logger?.LogInformation("Initializing DebugService...");
                DebugService.Initialize();
                logger?.LogInformation("DebugService initialized.");

                // Assign the final service provider to the static App property
                logger?.LogInformation("Assigning App.Services...");
                App.Services = app.Services;
                logger?.LogInformation("App.Services initialized with ServiceProvider.");

                // Initialize the centralized user session service
                logger?.LogInformation("Attempting to get IUserSessionService from container...");
                var userSessionService = app.Services.GetRequiredService<IUserSessionService>();
                logger?.LogInformation("IUserSessionService resolved. Initializing App.UserSessionService...");
                App.InitializeUserSession(userSessionService);
                logger?.LogInformation("UserSessionService initialized and connected to App.");

                // Connect UserSessionService to ApiConfig for centralized token management
                logger?.LogInformation("Connecting UserSessionService to ApiConfig...");
                ApiConfig.ConnectUserSessionService(userSessionService);
                logger?.LogInformation("UserSessionService connected to ApiConfig.");

                // Initialize session from persistent storage (important for mobile devices)
                logger?.LogInformation("Initializing UserSessionService from persistent storage asynchronously...");
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await userSessionService.InitializeAsync();
                        logger?.LogInformation("UserSessionService initialized from persistent storage.");
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "Error initializing UserSessionService from persistent storage.");
                    }
                });
                logger?.LogInformation("UserSessionService persistent storage initialization task started.");

                // Initialize ApiConfig with isDevelopment: true to ensure DevelopmentHttpClientHandler bypasses SSL validation for the IP.
                logger?.LogInformation("Initializing ApiConfig...");
                ApiConfig.Initialize(isDevelopment: true);
                logger?.LogInformation("ApiConfig.Initialize called with isDevelopment: true (to enable IP cert bypass).");

                // Test AppShell resolution - this can help identify DI issues
                logger?.LogInformation("Attempting to resolve AppShell from container...");
                try
                {
                    var appShellLogger = app.Services.GetService<ILogger<AppShell>>();
                    appShellLogger?.LogInformation("About to resolve AppShell from container.");

                    var appShell = app.Services.GetService<AppShell>();
                    logger?.LogInformation("AppShell resolution result: {Success}", appShell != null);

                    if (appShell == null)
                    {
                        logger?.LogError("CRITICAL: Failed to resolve AppShell from container.");
                    }
                }
                catch (Exception shellEx)
                {
                    logger?.LogError(shellEx, "Error resolving AppShell from container.");
                    logger?.LogError(shellEx, "Error resolving AppShell");
                }

                // Try to register additional handlers for debugging
                try
                {
                    System.Diagnostics.Debug.WriteLine("[MauiProgram] About to add first chance exception handler");
                    // Temporarily disable FirstChanceException handler as it might cause hanging
                    // AppDomain.CurrentDomain.FirstChanceException += (sender, args) =>
                    // {
                    //     System.Diagnostics.Debug.WriteLine($"FIRST CHANCE EXCEPTION: {args.Exception.GetType().Name}: {args.Exception.Message}");
                    // };

                    System.Diagnostics.Debug.WriteLine("[MauiProgram] First chance exception handler setup completed (disabled)");
                    logger?.LogInformation("First chance exception handler setup completed (disabled)");
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Failed to add first chance exception handler");
                }

                // Set up network status monitoring - with error handling
                System.Diagnostics.Debug.WriteLine("[MauiProgram] About to set up network status monitoring");
                logger?.LogInformation("About to set up network status monitoring...");
                try
                {
                    System.Diagnostics.Debug.WriteLine("[MauiProgram] Inside network monitoring try block");
                    logger?.LogInformation("Setting up network status monitoring...");
                    System.Diagnostics.Debug.WriteLine("[MauiProgram] About to resolve ConnectivityService");
                    var connectivityService = app.Services.GetService<IConnectivityService>();
                    System.Diagnostics.Debug.WriteLine($"[MauiProgram] ConnectivityService resolved: {connectivityService != null}");
                    if (connectivityService != null)
                    {
                        System.Diagnostics.Debug.WriteLine("[MauiProgram] ConnectivityService is not null, setting up event handler");
                        logger?.LogInformation("ConnectivityService resolved successfully");
                        
                        // Set up connectivity monitoring without immediately resolving other services
                        // to avoid potential circular dependencies or initialization issues
                        System.Diagnostics.Debug.WriteLine("[MauiProgram] About to set up ConnectivityChanged event handler");
                        connectivityService.ConnectivityChanged += async (sender, e) =>
                        {
                            try
                            {
                                logger?.LogInformation($"Connectivity changed: {e.IsConnected}");
                                
                                // Resolve services only when needed to avoid initialization deadlocks
                                var apiService = app.Services.GetService<ApiService>();
                                var webSocketService = app.Services.GetService<WebSocketService>();
                                
                                if (e.IsConnected)
                                {
                                    if (apiService != null)
                                        await apiService.TestConnectivityAsync();
                                    if (webSocketService != null)
                                        await webSocketService.ConnectAsync();
                                }
                                else
                                {
                                    if (webSocketService != null)
                                        await webSocketService.DisconnectAsync();
                                }
                            }
                            catch (Exception connEx)
                            {
                                logger?.LogError(connEx, "Error handling connectivity change");
                            }
                        };
                        
                        System.Diagnostics.Debug.WriteLine("[MauiProgram] ConnectivityChanged event handler set up successfully");
                        logger?.LogInformation("Network status monitoring set up successfully");
                    }
                    else
                    {
                        logger?.LogWarning("ConnectivityService not available, skipping network monitoring setup");
                    }
                }
                catch (Exception netEx)
                {
                    logger?.LogError(netEx, "Error setting up network status monitoring");
                    // Don't throw - continue with app initialization
                }
                logger?.LogInformation("MauiProgram.CreateMauiApp() completed successfully, returning app");
                System.Diagnostics.Debug.WriteLine("[MauiProgram] CreateMauiApp completed successfully");
            }
            catch (Exception ex)
            {
                // Log build/initialization errors critically
                System.Diagnostics.Debug.WriteLine($"CRITICAL ERROR during App Build/Initialization: {ex}");
                System.Diagnostics.Debug.WriteLine($"Exception details: {ex}");
                HandleUnhandledException("AppBuildInitialization", ex);
                // Depending on the error, you might want to stop the app here
                throw; // Rethrow to ensure the app doesn't proceed incorrectly
            }

            // If app is null here, it means builder.Build() or initialization failed critically
            if (app == null)
            {
                 // Log or handle the critical failure where app couldn't be built/initialized
                 System.Diagnostics.Debug.WriteLine("CRITICAL FAILURE: MauiApp could not be built or initialized.");
                 // Depending on requirements, you might want to explicitly exit or throw
                 throw new ApplicationException("Failed to build or initialize the MauiApp.");
            }

            System.Diagnostics.Debug.WriteLine("[MauiProgram] About to return MauiApp");
            return app;
        }

        private static void HandleUnhandledException(string source, Exception exception)
        {
            // Log the exception
            System.Diagnostics.Debug.WriteLine($"CRITICAL ERROR ({source}): {exception.Message}");
            System.Diagnostics.Debug.WriteLine(exception.StackTrace);

            // In a real app, you might want to log to a file or send to a crash reporting service
            try
            {
                var logDir = Path.Combine(FileSystem.AppDataDirectory, "CrashLogs");
                Directory.CreateDirectory(logDir);

                var logFile = Path.Combine(logDir, $"crash_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                var logContent = $"Time: {DateTime.Now}\n" +
                                $"Source: {source}\n" +
                                $"Exception: {exception.GetType().Name}\n" +
                                $"Message: {exception.Message}\n" +
                                $"Stack Trace: {exception.StackTrace}\n";

                if (exception.InnerException != null)
                {
                    logContent += $"\nInner Exception: {exception.InnerException.GetType().Name}\n" +
                                  $"Inner Message: {exception.InnerException.Message}\n" +
                                  $"Inner Stack Trace: {exception.InnerException.StackTrace}";
                }

                File.WriteAllText(logFile, logContent);
            }
            catch
            {
                // If logging fails, there's not much we can do at this point
            }
        }
    }
}
