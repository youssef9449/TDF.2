using TDFMAUI.Pages;
using TDFMAUI.Services;
using TDFMAUI.Helpers;
using System.Collections.ObjectModel;
using TDFMAUI.Config;
using TDFMAUI.Features.Auth;
using TDFShared.DTOs.Messages;
using TDFShared.DTOs.Users;
using TDFShared.Exceptions;
using TDFMAUI.Features.Admin;
using TDFMAUI.ViewModels;
using Microsoft.Extensions.Logging;
using TDFShared.Enums;

namespace TDFMAUI
{
    public partial class App : Application
    {
        private static UserDto _currentUser;
        public static UserDto CurrentUser
        {
            get => _currentUser;
            set
            {
                if (_currentUser != value)
                {
                    _currentUser = value;
                    OnUserChanged(); // Raise the event
                }
            }
        }

        // Event to notify when the user changes
        public static event EventHandler UserChanged;

        protected static void OnUserChanged()
        {
            UserChanged?.Invoke(null, EventArgs.Empty);
        }

        public static IServiceProvider Services { get; internal set; }
        public static ObservableCollection<NotificationDto> Notifications { get; } = new ObservableCollection<NotificationDto>();

        // Flag to indicate if we're in safe mode (passed from Android activity)
        public static bool SafeMode { get; private set; } = false;

        public App()
        {
            try
            {
                // Check if we're starting in safe mode (from intent)
                CheckSafeMode();

                // Set up global unhandled exception handler
                AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
                {
                    var exception = args.ExceptionObject as Exception;
                    System.Diagnostics.Debug.WriteLine($"UNHANDLED DOMAIN EXCEPTION: {exception?.GetType().Name}: {exception?.Message}");
                    System.Diagnostics.Debug.WriteLine($"STACK TRACE: {exception?.StackTrace}");

                    // Try to log the exception
                    try
                    {
                        DebugService.LogError("AppDomain", $"UNHANDLED EXCEPTION: {exception?.Message}");
                        DebugService.LogError("AppDomain", $"Stack trace: {exception?.StackTrace}");

                        // Record the crash for recovery tracking
                        ApiConfig.RecordCrash();
                    }
                    catch { /* Ignore errors from logging errors */ }
                };

                // Add handler for unhandled UI thread exceptions
                TaskScheduler.UnobservedTaskException += (sender, args) =>
                {
                    System.Diagnostics.Debug.WriteLine($"UNOBSERVED TASK EXCEPTION: {args.Exception?.GetType().Name}: {args.Exception?.Message}");
                    System.Diagnostics.Debug.WriteLine($"STACK TRACE: {args.Exception?.StackTrace}");

                    // Mark as observed so it doesn't crash the app
                    args.SetObserved();

                    // Try to log the exception
                    try
                    {
                        DebugService.LogError("TaskScheduler", $"UNOBSERVED EXCEPTION: {args.Exception?.Message}");
                        DebugService.LogError("TaskScheduler", $"Stack trace: {args.Exception?.StackTrace}");

                        // Record the crash for recovery tracking
                        ApiConfig.RecordCrash();
                    }
                    catch { /* Ignore errors from logging errors */ }
                };

                // Replace Application.Current.UnhandledException with appropriate handlers
                // For MAUI, we need to register platform-specific exception handlers
                // We'll handle this at a high level using AppDomain

                // Set a global exception handler that will catch exceptions after Initialize
#if DEBUG
                // In debug mode, we want to see the exception in the debugger
                // The debugger break will already be added by MAUI generated code
#else
                // In release mode, we want to log and handle the exception
                AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
                {
                    var exception = args.ExceptionObject as Exception;
                    try
                    {
                        // Log the exception
                        System.Diagnostics.Debug.WriteLine($"CRITICAL UNHANDLED EXCEPTION: {exception?.GetType().Name}: {exception?.Message}");
                        System.Diagnostics.Debug.WriteLine($"STACK TRACE: {exception?.StackTrace}");

                        // Try to show a simple error UI if possible
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            try
                            {
                                // Set a very simple error page
                                MainPage = new ContentPage
                                {
                                    BackgroundColor = Colors.Red,
                                    Content = new VerticalStackLayout
                                    {
                                        Padding = new Thickness(20),
                                        Children = {
                                            new Label { Text = "Critical Error", FontSize = 24, TextColor = Colors.White },
                                            new Label { Text = exception?.Message, TextColor = Colors.White },
                                            new Label { Text = exception?.StackTrace, TextColor = Colors.White, FontSize = 12 }
                                        }
                                    }
                                };
                            }
                            catch { /* Last resort - can't even show error page */ }
                        });
                    }
                    catch { /* Ignore errors from logging errors */ }
                };
#endif

                // Wrap InitializeComponent in detailed error handling
                try {
                    InitializeComponent();
                    System.Diagnostics.Debug.WriteLine("InitializeComponent completed successfully");

                    // Set a temporary page to satisfy MainPage requirement
                    MainPage = new ContentPage(); // This will be replaced in OnStart
                }
                catch (Exception initEx)
                {
                    // Capture detailed information about the XAML parsing error
                    System.Diagnostics.Debug.WriteLine($"XAML INITIALIZATION ERROR: {initEx.GetType().Name}: {initEx.Message}");
                    System.Diagnostics.Debug.WriteLine($"STACK TRACE: {initEx.StackTrace}");

                    if (initEx.InnerException != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"INNER EXCEPTION: {initEx.InnerException.GetType().Name}: {initEx.InnerException.Message}");
                        System.Diagnostics.Debug.WriteLine($"INNER STACK TRACE: {initEx.InnerException.StackTrace}");
                    }

                    // Display error on screen instead of crashing
                    MainPage = new ContentPage
                    {
                        BackgroundColor = Colors.Red,
                        Content = new VerticalStackLayout
                        {
                            Padding = new Thickness(20),
                            Children = {
                                new Label { Text = "XAML Initialization Error", FontSize = 24, TextColor = Colors.White },
                                new Label { Text = initEx.Message, TextColor = Colors.White },
                                new Label { Text = initEx.StackTrace, TextColor = Colors.White, FontSize = 12 }
                            }
                        }
                    };
                    return; // Skip the rest of initialization
                }

                // We no longer need to initialize DebugService here as it's already initialized in MauiProgram.cs
                // DebugService.Initialize();

                // Use standard console logging until DebugService is ready
                System.Diagnostics.Debug.WriteLine("App constructor starting");

                System.Diagnostics.Debug.WriteLine("App constructor finished");

            }
            catch (Exception ex)
            {
                // This outer catch block handles any errors that might occur before DebugService is initialized
                System.Diagnostics.Debug.WriteLine($"CRITICAL ERROR: {ex.Message}");
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);

                // Create a simple error page
                MainPage = new ContentPage
                {
                    BackgroundColor = Colors.Red,
                    Content = new VerticalStackLayout
                    {
                        Padding = new Thickness(20),
                        Children = {
                            new Label { Text = "Critical Error", FontSize = 24, TextColor = Colors.White },
                            new Label { Text = ex.Message, TextColor = Colors.White },
                            new Label { Text = ex.StackTrace, TextColor = Colors.White, FontSize = 12 }
                        }
                    }
                };
            }
        }

        private void ConfigureServices(ServiceCollection services)
        {
            // Register services
            services.AddSingleton<SecureStorageService>();
            services.AddSingleton<ApiService>();
            services.AddSingleton<LookupService>();

            // Register ViewModels
            services.AddTransient<LoginPageViewModel>();
            services.AddTransient<SignupViewModel>();

            // Register pages
            services.AddTransient<LoginPage>();
            services.AddTransient<SignupPage>();
            services.AddTransient<MainPage>();
            services.AddTransient<ProfilePage>();
            services.AddTransient<MessagesPage>();
            services.AddTransient<RequestsPage>(); // Commented out missing page
            services.AddTransient<GlobalChatPage>();
            services.AddTransient<AdminPage>();
        }

        protected override async void OnStart()
        {
            try
            {
                DebugService.LogInfo("App", "OnStart method called");

                // Log important app state information
                DebugService.LogInfo("App", $"Current MainPage type: {MainPage?.GetType().Name ?? "null"}");
                DebugService.LogInfo("App", $"App.Services available: {Services != null}");

                // MainPage is now set here instead of the constructor
                await SetupInitialPageAsync(); // Call the setup logic asynchronously

                DebugService.LogInfo("App", "OnStart finished");
            }
            catch (Exception ex)
            {
                DebugService.LogError("App", $"ERROR in OnStart: {ex.Message}");
                DebugService.LogError("App", $"Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Check if we're starting in safe mode (from Android intent)
        /// </summary>
        private void CheckSafeMode()
        {
            try
            {
                // On Android, we can check the intent extras
#if ANDROID
                var context = Android.App.Application.Context;
                var currentPackageName = context.PackageName;
                if (context.PackageManager != null && !string.IsNullOrEmpty(currentPackageName))
                {
                    var intent = context.PackageManager.GetLaunchIntentForPackage(currentPackageName);

                    if (intent?.Extras != null && intent.Extras.ContainsKey("safe_mode"))
                {
                    SafeMode = intent.Extras.GetBoolean("safe_mode", false);
                    System.Diagnostics.Debug.WriteLine($"Safe mode from intent: {SafeMode}");
                }
            } // <<< Add this closing brace for the 'if (context.PackageManager != null ...)' block
#endif

                // Log the safe mode status
                System.Diagnostics.Debug.WriteLine($"Application starting in {(SafeMode ? "SAFE MODE" : "NORMAL MODE")}");
            }
            catch (Exception ex)
            {
                // If there's an error, default to normal mode
                System.Diagnostics.Debug.WriteLine($"Error checking safe mode: {ex.Message}");
                SafeMode = false;
            }
        }

        /// <summary>
        /// Sets up the initial app page based on authentication status. Now async.
        /// </summary>
        private async Task SetupInitialPageAsync()
        {
            try
            {
                // Check if Services was initialized correctly
                if (Services == null)
                {
                    DebugService.LogError("App", "Services not initialized, cannot set up initial page");
                    DisplayFatalErrorPage("App services not initialized correctly.", null);
                    return;
                }

                // Configure global SSL settings - DISABLED
                // ApiConfig.ConfigureGlobalSslSettings();

                // Initialize API config with safe mode if needed - DISABLED
                // ApiConfig.Initialize(isDevelopment: true, safeMode: SafeMode);

                // Just set the development mode flag without running connectivity tests
                ApiConfig.IsDevelopmentMode = true;

                if (SafeMode)
                {
                    DebugService.LogWarning("App", "*** STARTING IN SAFE MODE ***");
                    DebugService.LogWarning("App", "Some features will be disabled for stability");
                }

                // First, show the diagnostic page to help troubleshoot any startup issues
                // This is especially helpful for Android where we're experiencing crashes
                var diagnosticPage = new StartupDiagnosticPage();
                var navPage = new NavigationPage(diagnosticPage);
                MainPage = navPage;

                DebugService.LogInfo("App", "Showing startup diagnostic page");

                // Continue with normal startup after diagnostic page
                try
                {
                    // Get required services
                    var authService = Services.GetRequiredService<IAuthService>();
                    var logger = Services.GetRequiredService<ILogger<App>>();

                    // In safe mode, skip authentication check and go straight to diagnostic page
                    if (SafeMode)
                    {
                        logger.LogInformation("Safe mode active - staying on diagnostic page");
                        // Don't set NextPage so we stay on the diagnostic page
                        return;
                    }

                    // Check authentication status asynchronously by trying to get the current user
                    UserDto currentUser = await authService.GetCurrentUserAsync();
                    bool isAuthenticated = currentUser != null;

                    // Store the page we'll navigate to after diagnostics
                    Page nextPage;

                    if (isAuthenticated)
                    {
                        logger.LogInformation("User is authenticated. Will set MainPage to AppShell after diagnostics.");
                        // Resolve AppShell from DI container
                        nextPage = Services.GetRequiredService<AppShell>();
                    }
                    else
                    {
                        logger.LogInformation("User is not authenticated. Will set MainPage to LoginPage after diagnostics.");
                        // Resolve LoginPage from DI container
                        nextPage = Services.GetRequiredService<LoginPage>();
                    }

                    // Store the next page in the diagnostic page so it can navigate when ready
                    diagnosticPage.NextPage = nextPage;

                    logger.LogInformation($"Next page prepared: {nextPage.GetType().Name}");
                }
                catch (Exception ex)
                {
                    DebugService.LogError("App", $"Error preparing next page: {ex.Message}");
                    DebugService.LogError("App", $"Stack trace: {ex.StackTrace}");
                    // We'll stay on the diagnostic page if there's an error
                }
            }
            catch (Exception ex)
            {
                DebugService.LogError("App", $"Error setting up initial page: {ex.Message}");
                DebugService.LogError("App", $"Stack trace: {ex.StackTrace}");
                DisplayFatalErrorPage("Failed to initialize application.", ex);
            }
        }

        private void OnNotificationReceived(object sender, NotificationEventArgs e)
        {
            // Notification handling code
        }

        private void OnChatMessageReceived(object sender, ChatMessageEventArgs e)
        {
            // Chat message handling code
        }

        private void OnConnectionStatusChanged(object sender, ConnectionStatusEventArgs e)
        {
            // Connection status handling code
        }

        private void OnUserStatusChanged(object sender, UserStatusEventArgs e)
        {
            // User status handling code
        }

        private void ShowLocalNotification(string title, string message, bool silent = false)
        {
            try
            {
                // Simplified for compilation - original notification code commented out
                DebugService.LogInfo("App", $"Would show notification: {title} - {message}");

                /*
#if ANDROID || IOS
                // Request permission first
                var status = await Permissions.CheckStatusAsync<Permissions.Notifications>();
                if (status != PermissionStatus.Granted)
                {
                    status = await Permissions.RequestAsync<Permissions.Notifications>();
                    if (status != PermissionStatus.Granted)
                        return;
                }

                // Create the notification
                var notificationRequest = new NotificationRequest
                {
                    NotificationId = new Random().Next(100000), // Random ID to avoid overwriting
                    Title = title,
                    Description = message,
                    ReturningData = data != null ? string.Join("&", data.Select(kv => $"{kv.Key}={kv.Value}")) : "",
                    Silent = silent
                };

                // Send the notification
                await LocalNotificationCenter.Current.Show(notificationRequest);
#else
                // On other platforms, use a different approach or just log
                DebugService.LogInfo("App", $"Notification would show: {title} - {message}");
#endif
                */
            }
            catch (Exception ex)
            {
                DebugService.LogError("App", $"Error showing notification: {ex.Message}");
            }
        }

        protected override void OnSleep()
        {
            base.OnSleep();

            // When app goes to background, reconnect socket when it comes back
            DebugService.LogInfo("App", "Application going to sleep");

            // For non-mobile devices, update user status to Offline when app is closed
            if (DeviceHelper.IsDesktop || (!DeviceHelper.IsMobile && DeviceHelper.IsLargeScreen))
            {
                UpdateUserStatusToOffline();
            }
        }

        private void UpdateUserStatusToOffline()
        {
            if (CurrentUser != null)
            {
                try
                {
                    var userPresenceService = Services.GetService<IUserPresenceService>();
                    if (userPresenceService != null)
                    {
                        // Run this in a fire-and-forget manner since we're shutting down
                        Task.Run(async () =>
                        {
                            try
                            {
                                DebugService.LogInfo("App", "Setting user status to Offline on app closing");
                                await userPresenceService.UpdateStatusAsync(TDFShared.Enums.UserPresenceStatus.Offline, "");
                            }
                            catch (Exception ex)
                            {
                                DebugService.LogError("App", $"Error updating user status to Offline: {ex.Message}");
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    DebugService.LogError("App", $"Error getting UserPresenceService: {ex.Message}");
                }
            }
        }

        protected override void OnResume()
        {
            base.OnResume();

            DebugService.LogInfo("App", "Application resuming");

            // Reconnect WebSocket if it's not connected - DEFER THIS
            /*
            if (_webSocketService != null && !_webSocketService.IsConnected)
            {
                Task.Run(async () =>
                {
                    try
                    {
                        var secureStorage = Services.GetService<SecureStorageService>();
                        if (secureStorage != null)
                        {
                            var (token, _) = await secureStorage.GetTokenAsync();
                            if (!string.IsNullOrEmpty(token))
                            {
                                await _webSocketService.ConnectAsync(token);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugService.LogError("App", $"Error reconnecting WebSocket: {ex.Message}");
                    }
                });
            }
            */
        }

        private void DisplayFatalErrorPage(string message, Exception ex)
        {
            // Create a simple error page to display when application can't start properly
            MainPage = new ContentPage
            {
                BackgroundColor = Colors.White,
                Content = new VerticalStackLayout
                {
                    Spacing = 20,
                    Padding = new Thickness(20),
                    VerticalOptions = LayoutOptions.Center,
                    Children =
                    {
                        new Label
                        {
                            Text = "Application Error",
                            FontSize = 24,
                            FontAttributes = FontAttributes.Bold,
                            HorizontalOptions = LayoutOptions.Center
                        },
                        new Label
                        {
                            Text = message,
                            FontSize = 16,
                            HorizontalOptions = LayoutOptions.Center
                        },
                        new Label
                        {
                            Text = ex.Message,
                            FontSize = 14,
                            TextColor = Colors.Red
                        },
                        new Button
                        {
                            Text = "Save Error Logs",
                            Command = new Command(async () =>
                            {
                                bool saved = await DebugService.SaveLogsToFile();
                                await Application.Current.MainPage.DisplayAlert(
                                    saved ? "Logs Saved" : "Error",
                                    saved ? "Logs have been saved to the application data directory." : "Failed to save logs.",
                                    "OK");
                            })
                        },
                        new Button
                        {
                            Text = "Retry",
                            Command = new Command(() => {
                                var loginViewModel = Services.GetService<LoginPageViewModel>();
                                Application.Current.MainPage = new NavigationPage(new LoginPage(loginViewModel));
                            })
                        }
                    }
                }
            };
        }
    }
}
