using Microsoft.Extensions.Logging;
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


namespace TDFMAUI
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
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
                                            System.Diagnostics.Debug.WriteLine($"Loaded configuration from embedded resource via temp file");
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
                    ApiConfig.BaseUrl = "https://192.168.100.3:5001/api/";
                    ApiConfig.WebSocketUrl = "wss://192.168.100.3:5001/ws";
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
                        options.BaseUrl = "https://192.168.100.3:5001/api/"; // Default fallback
                        System.Diagnostics.Debug.WriteLine($"Warning: {section}:BaseUrl not found in configuration. Using default DI BaseUrl: {options.BaseUrl}");
                    }
                    options.WebSocketUrl = builder.Configuration[$"{section}:WebSocketUrl"];
                    if (string.IsNullOrEmpty(options.WebSocketUrl))
                    {
                        options.WebSocketUrl = "wss://192.168.100.3:5001/ws"; // Default fallback
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

                // Test API connectivity after configuration - DISABLED
                // Task.Run(async () => {
                //     bool isConnected = await ApiConfig.TestApiConnectivityAsync();
                //     System.Diagnostics.Debug.WriteLine($"--- API Connectivity Test Result: {(isConnected ? "SUCCESS" : "FAILURE")} ---");
                // });
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

            // Register services
            builder.Services.AddSingleton<WebSocketService>();
            builder.Services.AddSingleton<IWebSocketService>(sp => sp.GetRequiredService<WebSocketService>());
            builder.Services.AddSingleton<ApiService>();
            builder.Services.AddSingleton<IApiService>(sp => sp.GetRequiredService<ApiService>());
            builder.Services.AddSingleton<SecureStorageService>();
            builder.Services.AddSingleton<NetworkMonitorService>();
            builder.Services.AddSingleton<LocalStorageService>();
            builder.Services.AddSingleton<ILocalStorageService, LocalStorageService>();
            builder.Services.AddSingleton<IUserPresenceService, UserPresenceService>();
            builder.Services.AddSingleton<IUserProfileService, UserProfileService>();

            // Register NetworkService and IConnectivity
            builder.Services.AddSingleton(Connectivity.Current);
            builder.Services.AddSingleton<NetworkService>();

            // Register LookupService
            builder.Services.AddSingleton<LookupService>();
            builder.Services.AddSingleton<ILookupService>(sp => sp.GetRequiredService<LookupService>());

            // Always register DevelopmentHttpClientHandler since we need to bypass cert validation for the IP address in all modes.
            builder.Services.AddSingleton<DevelopmentHttpClientHandler>();
            builder.Services.AddSingleton<HttpClient>(sp =>
            {
                var handler = sp.GetRequiredService<DevelopmentHttpClientHandler>();
                var logger = sp.GetRequiredService<ILogger<HttpClient>>();
                logger.LogInformation("Creating HttpClient with DevelopmentHttpClientHandler (for IP address target)");
                return new HttpClient(handler);
                // BaseAddress and Timeout will be set by HttpClientService using IOptions<ApiSettings>
            });

            // Register RequestService
            builder.Services.AddTransient<IRequestService, RequestService>();

            // Register AuthService (Assuming implementation exists)
            builder.Services.AddSingleton<IAuthService, AuthService>();

            // Register NotificationService hierarchy
            builder.Services.AddSingleton<IPlatformNotificationService, PlatformNotificationService>();
            builder.Services.AddSingleton<NotificationService>();
            builder.Services.AddSingleton<IExtendedNotificationService>(sp => sp.GetRequiredService<NotificationService>());
            builder.Services.AddSingleton<INotificationService>(sp => sp.GetRequiredService<IExtendedNotificationService>());

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

            // Register new HttpClientService
            builder.Services.AddSingleton<IHttpClientService, HttpClientService>();

            // Register ConnectivityService
            builder.Services.AddSingleton<IConnectivityService, ConnectivityService>();

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
                DebugService.Initialize();

                // Assign the final service provider to the static App property
                App.Services = app.Services;
                logger?.LogInformation("App.Services initialized with ServiceProvider");

                // Initialize ApiConfig with isDevelopment: true to ensure DevelopmentHttpClientHandler bypasses SSL validation for the IP.
                ApiConfig.Initialize(isDevelopment: true);
                System.Diagnostics.Debug.WriteLine("ApiConfig.Initialize called with isDevelopment: true (to enable IP cert bypass)");

                // Test AppShell resolution - this can help identify DI issues
                try {
                    var appShellLogger = app.Services.GetService<ILogger<AppShell>>();
                    appShellLogger?.LogInformation("About to resolve AppShell from container");

                    var appShell = app.Services.GetService<AppShell>();
                    appShellLogger?.LogInformation("AppShell resolution result: {Success}", appShell != null);

                    if (appShell == null) {
                        appShellLogger?.LogError("CRITICAL: Failed to resolve AppShell from container");
                    }
                } catch (Exception shellEx) {
                    logger?.LogError(shellEx, "Error resolving AppShell");
                }

                // Try to register additional handlers for debugging
                try
                {
                    // Add a handler to log unhandled exceptions in case the AppDomain handler misses them
                    AppDomain.CurrentDomain.FirstChanceException += (sender, args) =>
                    {
                        System.Diagnostics.Debug.WriteLine($"FIRST CHANCE EXCEPTION: {args.Exception.GetType().Name}: {args.Exception.Message}");
                    };

                    logger?.LogInformation("Added first chance exception handler");
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Failed to add first chance exception handler");
                }

                // Set up and start the NetworkMonitorService
                var networkService = app.Services.GetRequiredService<NetworkService>();
                var webSocketService = app.Services.GetRequiredService<WebSocketService>();
                var secureStorage = app.Services.GetRequiredService<SecureStorageService>();
                var wsLogger = app.Services.GetRequiredService<ILogger<WebSocketService>>();

                // Register for network status changes
                networkService.NetworkStatusChanged += async (sender, isConnected) =>
                {
                    // Log the status change but defer connection attempt during initial startup
                    wsLogger.LogInformation($"Network status changed. IsConnected: {isConnected}");

                    if (isConnected)
                    {
                        // Attempt to connect web socket when network becomes available
                        // logger.LogInformation("Network is now available, attempting to connect WebSocket"); // Defer connection

                        // Get auth token
                        // var tokenResult = await secureStorage.GetTokenAsync();
                        // var token = tokenResult.Item1;
                        // if (!string.IsNullOrEmpty(token))
                        // {
                        //     // Try to connect with the token
                        //     // await webSocketService.ConnectAsync(token); // Defer connection
                        // }
                    }
                    else
                    {
                        // Log network disconnection
                        wsLogger.LogWarning("Network is no longer available");
                        // Optionally, disconnect the WebSocket if it was connected
                        // if (webSocketService.IsConnected) { await webSocketService.DisconnectAsync(); }
                    }
                };

                // Start network monitoring - DISABLED
                // Task.Run(async () =>
                // {
                //     try
                //     {
                //         await networkService.StartMonitoringAsync();
                //         wsLogger.LogInformation("Network monitoring started");
                //     }
                //     catch (Exception ex)
                //     {
                //         wsLogger.LogCritical(ex, "Failed to start network monitoring");
                //
                //         // Log critical errors to a file in app data directory
                //         var appDataPath = FileSystem.AppDataDirectory;
                //         var errorLogPath = Path.Combine(appDataPath, "error.log");
                //
                //         try
                //         {
                //             File.AppendAllText(errorLogPath, $"{DateTime.Now}: {ex.Message}\n{ex.StackTrace}\n\n");
                //         }
                //         catch
                //         {
                //             // If we can't even write to the log file, there's not much else we can do
                //         }
                //     }
                // });
            }
            catch (Exception ex)
            {
                // Log build/initialization errors critically
                System.Diagnostics.Debug.WriteLine($"CRITICAL ERROR during App Build/Initialization: {ex}");
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
