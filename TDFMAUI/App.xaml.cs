using TDFMAUI.Pages;
using TDFMAUI.Services;
using TDFShared.Services;
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
using System.Linq;
using TDFMAUI.Helpers;
using TDFMAUI.Features.Dashboard;
using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
#if ANDROID || IOS
using Plugin.Firebase.CloudMessaging;
using Plugin.Firebase.CloudMessaging.EventArgs;
#endif


namespace TDFMAUI
{
    public partial class App : Application
    {
        private readonly ILogger<App> _logger;
        private static IUserSessionService? _userSessionService;

        /// <summary>
        /// Gets the current user from the centralized session service
        /// </summary>
        public static UserDto? CurrentUser => _userSessionService?.CurrentUser;

        /// <summary>
        /// Gets the user session service instance
        /// </summary>
        public static IUserSessionService? UserSessionService => _userSessionService;

        // Event to notify when the user changes (for backward compatibility)
        public static event EventHandler? UserChanged;

        /// <summary>
        /// Initializes the user session service
        /// </summary>
        public static void InitializeUserSession(IUserSessionService userSessionService)
        {
            _userSessionService = userSessionService;
            
            // Subscribe to user changes from the session service
            _userSessionService.UserChanged += (sender, args) =>
            {
                UserChanged?.Invoke(null, EventArgs.Empty);
            };
        }

        protected static void OnUserChanged()
        {
            UserChanged?.Invoke(null, EventArgs.Empty);
        }

        public static IServiceProvider? Services { get; internal set; }
        public static ObservableCollection<NotificationDto> Notifications { get; } = new ObservableCollection<NotificationDto>();

        // Flag to indicate if we're in safe mode (passed from Android activity)
        public static bool SafeMode { get; private set; } = false;

        public App()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[App] Constructor started");
                System.Diagnostics.Debug.WriteLine("[App] App constructor called - MauiProgram completed successfully");

                // Always set a visible fallback page immediately
                MainPage = new ContentPage
                {
                    Content = new Label
                    {
                        Text = "App is starting...",
                        VerticalOptions = LayoutOptions.Center,
                        HorizontalOptions = LayoutOptions.Center,
                        FontSize = 24
                    }
                };

                // Logger will be initialized later via DI. Use Debug.WriteLine for early logs.
                // _logger = Services?.GetService<ILogger<App>>() ??
                //     throw new InvalidOperationException("Logger service not available");

                // Check if we're starting in safe mode (from intent)
                CheckSafeMode();

                // Set up global unhandled exception handler
                AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
                {
                    var exception = args.ExceptionObject as Exception;
                    System.Diagnostics.Debug.WriteLine($"UNHANDLED DOMAIN EXCEPTION: {exception?.GetType().Name}: {exception?.Message}");
                    System.Diagnostics.Debug.WriteLine($"STACK TRACE: {exception?.StackTrace}");
                    try
                    {
                        DebugService.LogError("AppDomain", $"UNHANDLED EXCEPTION: {exception?.Message}");
                        DebugService.LogError("AppDomain", $"Stack trace: {exception?.StackTrace}");
                        ApiConfig.RecordCrash();
                    }
                    catch { }
                    // Show error page
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
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
                    });
                };

                // Add handler for unhandled UI thread exceptions
                TaskScheduler.UnobservedTaskException += (sender, args) =>
                {
                    System.Diagnostics.Debug.WriteLine($"UNOBSERVED TASK EXCEPTION: {args.Exception?.GetType().Name}: {args.Exception?.Message}");
                    System.Diagnostics.Debug.WriteLine($"STACK TRACE: {args.Exception?.StackTrace}");
                    args.SetObserved();
                    try
                    {
                        DebugService.LogError("TaskScheduler", $"UNOBSERVED EXCEPTION: {args.Exception?.Message}");
                        DebugService.LogError("TaskScheduler", $"Stack trace: {args.Exception?.StackTrace}");
                        ApiConfig.RecordCrash();
                    }
                    catch { }
                    // Show error page
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        MainPage = new ContentPage
                        {
                            BackgroundColor = Colors.Red,
                            Content = new VerticalStackLayout
                            {
                                Padding = new Thickness(20),
                                Children = {
                                    new Label { Text = "Critical Error", FontSize = 24, TextColor = Colors.White },
                                    new Label { Text = args.Exception?.Message, TextColor = Colors.White },
                                    new Label { Text = args.Exception?.StackTrace, TextColor = Colors.White, FontSize = 12 }
                                }
                            }
                        };
                    });
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
                    System.Diagnostics.Debug.WriteLine("[App] About to call InitializeComponent");
                    InitializeComponent();
                    System.Diagnostics.Debug.WriteLine("[App] InitializeComponent completed successfully");

                    // Initialize theme service for platform-aware theme adaptation
                    System.Diagnostics.Debug.WriteLine("[App] About to initialize theme service");
                    var themeService = Services?.GetService<ThemeService>();
                    themeService?.Initialize();
                    System.Diagnostics.Debug.WriteLine("[App] Theme service initialized");
                    
                    // Dynamically merge the correct theme dictionary
                    System.Diagnostics.Debug.WriteLine("[App] About to call SetThemeColors");
                    SetThemeColors();
                    System.Diagnostics.Debug.WriteLine("[App] SetThemeColors completed");
                }
                catch (Exception initEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[App] XAML INITIALIZATION ERROR: {initEx.GetType().Name}: {initEx.Message}");
                    System.Diagnostics.Debug.WriteLine($"STACK TRACE: {initEx.StackTrace}");
                    if (initEx.InnerException != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"INNER EXCEPTION: {initEx.InnerException.GetType().Name}: {initEx.InnerException.Message}");
                        System.Diagnostics.Debug.WriteLine($"INNER STACK TRACE: {initEx.InnerException.StackTrace}");
                    }
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
                    return;
                }
                System.Diagnostics.Debug.WriteLine("[App] Constructor finished");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App] CRITICAL ERROR: {ex.Message}");
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
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
            services.AddTransient<DashboardViewModel>();

            // Register pages
            services.AddTransient<LoginPage>();
            services.AddTransient<SignupPage>();
            services.AddTransient<MainPage>();
            services.AddTransient<ProfilePage>();
            services.AddTransient<MessagesPage>();
            services.AddTransient<RequestsPage>();
            services.AddTransient<GlobalChatPage>();
            services.AddTransient<AdminPage>();
            services.AddTransient<DashboardPage>();
        }

        protected override async void OnStart()
        {
            try
            {
                DebugService.LogInfo("App", "OnStart method entered.");
                System.Diagnostics.Debug.WriteLine("[App] OnStart entered");

                // Log important app state information
                DebugService.LogInfo("App", $"Current MainPage type: {MainPage?.GetType().Name ?? "null"}");
                DebugService.LogInfo("App", $"App.Services available: {Services != null}");
                
                // Initialize theme helper
                ThemeHelper.Initialize();
                DebugService.LogInfo("App", $"Theme initialized: {ThemeHelper.CurrentTheme}");
                
                // Configure window size and properties for desktop platforms
                if (DeviceHelper.IsDesktop && Application.Current?.Windows.Count > 0)
                {
                    var mainWindow = Application.Current.Windows[0];
                    WindowManager.ConfigureMainWindow(mainWindow);
                    DebugService.LogInfo("App", "Window configured.");

                    // Subscribe to the Destroying event for desktop platforms
                    mainWindow.Destroying += OnWindowDestroying;
                    DebugService.LogInfo("App", "Subscribed to Window.Destroying event.");
                }

                // MainPage is now set here instead of the constructor
                DebugService.LogInfo("App", "Calling SetupInitialPageAsync...");
                await SetupInitialPageAsync(); // Call the setup logic asynchronously
                System.Diagnostics.Debug.WriteLine("[App] SetupInitialPageAsync completed");
                DebugService.LogInfo("App", "OnStart finished");
            }
            catch (Exception ex)
            {
                DebugService.LogError("App", $"ERROR in OnStart: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[App] ERROR in OnStart: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[App] OnStart Exception Stack Trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[App] OnStart Inner Exception: {ex.InnerException.Message}");
                    System.Diagnostics.Debug.WriteLine($"[App] OnStart Inner Stack Trace: {ex.InnerException.StackTrace}");
                }
                
                // Try to show an error page
                try
                {
                    DisplayFatalErrorPage("Error during app startup", ex);
                }
                catch (Exception displayEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[App] Failed to display error page: {displayEx.Message}");
                }
            }
        }

        private async Task SetupInitialPageAsync()
        {
            DebugService.LogInfo("App", "SetupInitialPageAsync entered.");
            System.Diagnostics.Debug.WriteLine("[App] SetupInitialPageAsync entered");
            try
            {
                if (Services == null)
                {
                    DebugService.LogError("App", "Services not initialized, cannot set up initial page");
                    System.Diagnostics.Debug.WriteLine("[App] Services not initialized, cannot set up initial page");
                    DisplayFatalErrorPage("App services not initialized correctly.", null);
                    return;
                }
                ApiConfig.IsDevelopmentMode = true;
                System.Diagnostics.Debug.WriteLine("[App] ApiConfig.IsDevelopmentMode set");
                if (SafeMode)
                {
                    DebugService.LogWarning("App", "*** STARTING IN SAFE MODE ***");
                    System.Diagnostics.Debug.WriteLine("[App] Starting in SAFE MODE");
                    await Shell.Current.GoToAsync("DiagnosticsPage");
                    return;
                }
                try
                {
                    var authService = Services.GetRequiredService<IAuthService>();
                    var secureStorage = Services.GetRequiredService<SecureStorageService>();
                    var logger = Services.GetRequiredService<ILogger<App>>();
                    System.Diagnostics.Debug.WriteLine("[App] Got required services for SetupInitialPageAsync");
                    bool hasValidToken = await secureStorage.HandleTokenPersistenceAsync();
                    System.Diagnostics.Debug.WriteLine($"[App] HandleTokenPersistenceAsync result: {hasValidToken}");
                    UserDto currentUser = hasValidToken ? await authService.GetCurrentUserAsync() : null;
                    System.Diagnostics.Debug.WriteLine($"[App] GetCurrentUserAsync result: {currentUser != null}");
                    bool isAuthenticated = currentUser != null;
                    if (isAuthenticated)
                    {
                        logger.LogInformation("User is authenticated. Setting MainPage to AppShell.");
                        System.Diagnostics.Debug.WriteLine("[App] User is authenticated. Setting MainPage to AppShell");
                        var shell = Services.GetRequiredService<AppShell>();
                        MainPage = shell;
                        System.Diagnostics.Debug.WriteLine("[App] MainPage set to AppShell");
                        DebugService.LogInfo("App", "MainPage set to AppShell. Registering WebSocket event handlers.");
                        RegisterWebSocketEventHandlers();
                        DebugService.LogInfo("App", "Navigating to DashboardPage.");
                        await Shell.Current.GoToAsync("DashboardPage");
                        System.Diagnostics.Debug.WriteLine("[App] Navigated to DashboardPage");
                        DebugService.LogInfo("App", "Navigation to DashboardPage completed.");
                    }
                    else
                    {
                        logger.LogInformation("User is not authenticated. Setting MainPage to LoginPage.");
                        System.Diagnostics.Debug.WriteLine("[App] User is not authenticated. Setting MainPage to LoginPage");
                        var loginPage = Services.GetRequiredService<TDFMAUI.Features.Auth.LoginPage>();
                        MainPage = new NavigationPage(loginPage);
                        System.Diagnostics.Debug.WriteLine("[App] MainPage set to LoginPage");
                        DebugService.LogInfo("App", "MainPage set to LoginPage.");
                    }
                }
                catch (Exception ex)
                {
                    DebugService.LogError("App", $"Error preparing next page: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"[App] Error preparing next page: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"[App] Error preparing next page stack trace: {ex.StackTrace}");
                    if (ex.InnerException != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[App] Inner exception: {ex.InnerException.Message}");
                        System.Diagnostics.Debug.WriteLine($"[App] Inner stack trace: {ex.InnerException.StackTrace}");
                    }
                    
                    // Try to show diagnostics page, but if that fails, show error page
                    try
                    {
                        await Shell.Current.GoToAsync("DiagnosticsPage");
                    }
                    catch (Exception navEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[App] Failed to navigate to DiagnosticsPage: {navEx.Message}");
                        DisplayFatalErrorPage("Failed to set up initial page", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                DebugService.LogError("App", $"Error setting up initial page: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[App] Error setting up initial page: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[App] Error setting up initial page stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[App] Inner exception: {ex.InnerException.Message}");
                    System.Diagnostics.Debug.WriteLine($"[App] Inner stack trace: {ex.InnerException.StackTrace}");
                }
                DisplayFatalErrorPage("Failed to initialize application.", ex);
            }
        }

        private void OnNotificationReceived(object sender, TDFShared.DTOs.Messages.NotificationEventArgs e)
        {
            try
            {
                // Log the notification
                DebugService.LogInfo("App", $"Notification received: {e.Title} - {e.Message}");

                // Show a local notification to the user
                ShowLocalNotification(e.Title, e.Message);

                // Add to the app's notification collection for display in the UI
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    // Create a NotificationDto from the event args
                    var notification = new NotificationDto
                    {
                        NotificationId = e.NotificationId,
                        Title = e.Title,
                        Message = e.Message,
                        Timestamp = e.Timestamp,
                        SenderId = e.SenderId,
                        SenderName = e.SenderName,
                        Type = e.Type.ToString()
                    };

                    // Add to the collection (at the beginning to show newest first)
                    Notifications.Insert(0, notification);

                    // Limit the collection size to prevent memory issues
                    if (Notifications.Count > 100)
                    {
                        Notifications.RemoveAt(Notifications.Count - 1);
                    }
                });

                // If we have a notification service, mark as seen if appropriate
                if (Services != null && e.NotificationId > 0)
                {
                    var notificationService = Services.GetService<Services.INotificationService>();
                    if (notificationService != null && CurrentUser != null)
                    {
                        // Fire and forget - don't block the UI
                        Task.Run(async () =>
                        {
                            try
                            {
                                await notificationService.MarkAsSeenAsync(e.NotificationId);
                            }
                            catch (Exception ex)
                            {
                                DebugService.LogError("App", $"Error marking notification as seen: {ex.Message}");
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                DebugService.LogError("App", $"Error handling notification: {ex.Message}");
            }
        }

        private void OnChatMessageReceived(object sender, ChatMessageEventArgs e)
        {
            try
            {
                // Log the chat message
                DebugService.LogInfo("App", $"Chat message received from {e.SenderName}: {e.Message}");

                // Show a local notification for the chat message
                ShowLocalNotification($"Message from {e.SenderName}", e.Message);

                // If we have a chat service, update the UI or mark as delivered
                if (Services != null)
                {
                    var webSocketService = Services.GetService<IWebSocketService>();
                    if (webSocketService != null)
                    {
                        // Mark the message as delivered
                        Task.Run(async () =>
                        {
                            try
                            {
                                // Mark the message as delivered to the sender
                                await webSocketService.MarkMessagesAsDeliveredAsync(e.SenderId);
                            }
                            catch (Exception ex)
                            {
                                DebugService.LogError("App", $"Error marking message as delivered: {ex.Message}");
                            }
                        });
                    }
                }

                // If the app is in the background, we've already shown a notification
                // If the app is in the foreground, the active chat view should handle displaying the message
            }
            catch (Exception ex)
            {
                DebugService.LogError("App", $"Error handling chat message: {ex.Message}");
            }
        }

        private void OnConnectionStatusChanged(object sender, ConnectionStatusEventArgs e)
        {
            try
            {
                // Log the connection status change
                DebugService.LogInfo("App", $"Connection status changed: {(e.IsConnected ? "Connected" : "Disconnected")}");

                // Update UI or show notification based on connection status
                if (e.IsConnected)
                {
                    // Connection established
                    DebugService.LogInfo("App", "WebSocket connection established");

                    // If we were previously disconnected, show a notification
                    if (Services != null)
                    {
                        var webSocketService = Services.GetService<IWebSocketService>();
                        if (webSocketService != null)
                        {
                            // Update user status to Online if we're reconnecting
                            if (CurrentUser != null)
                            {
                                var userPresenceService = Services.GetService<IUserPresenceService>();
                                if (userPresenceService != null)
                                {
                                    // Update status to Online
                                    Task.Run(async () =>
                                    {
                                        try
                                        {
                                            await userPresenceService.UpdateStatusAsync(UserPresenceStatus.Online, "");
                                        }
                                        catch (Exception ex)
                                        {
                                            DebugService.LogError("App", $"Error updating user status to Online: {ex.Message}");
                                        }
                                    });
                                }
                            }
                        }
                    }
                }
                else
                {
                    // Connection lost
                    string message = e.WasClean ?
                        "Connection closed normally" :
                        "Connection lost unexpectedly";

                    DebugService.LogWarning("App", message);

                    // Show a notification if the disconnection was unexpected
                    if (!e.WasClean)
                    {
                        ShowLocalNotification("Connection Lost", "The connection to the server was lost. Attempting to reconnect...");

                        // If reconnection failed after multiple attempts, show a more serious notification
                        if (e.ReconnectionFailed)
                        {
                            ShowLocalNotification("Connection Error", "Failed to reconnect to the server. Please check your internet connection and try again later.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugService.LogError("App", $"Error handling connection status change: {ex.Message}");
            }
        }

        private void OnUserStatusChanged(object sender, UserStatusEventArgs e)
        {
            try
            {
                // Log the user status change
                DebugService.LogInfo("App", $"User status changed: {e.Username} is now {e.PresenceStatus}");

                // Parse the presence status string to enum
                UserPresenceStatus presenceStatus = UserPresenceStatus.Offline;
                if (!string.IsNullOrEmpty(e.PresenceStatus) &&
                    Enum.TryParse<UserPresenceStatus>(e.PresenceStatus, true, out var parsedStatus))
                {
                    presenceStatus = parsedStatus;
                }

                // If this is the current user, update the UI
                if (CurrentUser != null && e.UserId == CurrentUser.UserID)
                {
                    DebugService.LogInfo("App", $"Current user status changed to {e.PresenceStatus}");
                    // No need to update our own status as we initiated the change
                }
                else
                {
                    // This is another user's status change

                    // Show a notification for important status changes (e.g., user coming online)
                    if (e.IsConnected && e.PresenceStatus == "Online")
                    {
                        // Only show notifications for users coming online if they were previously offline
                        ShowLocalNotification("User Online", $"{e.Username} is now online", true);
                    }

                    // Update the UsersRightPanel if it's loaded
                    UpdateUsersRightPanel(e.UserId, e.Username, presenceStatus, e.StatusMessage);
                }

                // Directly notify the UserPresenceService about the status change
                // This will propagate the change to all parts of the application that are subscribed to the event
                if (Services != null)
                {
                    var userPresenceService = Services.GetService<IUserPresenceService>();
                    if (userPresenceService != null)
                    {
                        // Create event args for the service
                        var args = new UserStatusChangedEventArgs
                        {
                            UserId = e.UserId,
                            Username = e.Username,
                            Status = presenceStatus
                        };

                        // Update the user status directly
                        // This will trigger the UserStatusChanged event on the service
                        // which will notify all subscribers, including the UsersRightPanel
                        MainThread.BeginInvokeOnMainThread(async () =>
                        {
                            try
                            {
                                await userPresenceService.UpdateUserStatusAsync(e.UserId, presenceStatus);
                                DebugService.LogInfo("App", $"Updated user {e.Username} status to {presenceStatus} via UserPresenceService");
                            }
                            catch (Exception ex)
                            {
                                DebugService.LogError("App", $"Error updating user status via UserPresenceService: {ex.Message}");
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                DebugService.LogError("App", $"Error handling user status change: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates the user status in the UsersRightPanel if it's currently loaded
        /// </summary>
        private void UpdateUsersRightPanel(int userId, string username, UserPresenceStatus status, string statusMessage)
        {
            try
            {
                // Run on the UI thread
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    try
                    {
                        // Check if Shell is available
                        if (Shell.Current == null)
                        {
                            DebugService.LogWarning("App", "Cannot update UsersRightPanel: Shell.Current is null");
                            return;
                        }

                        // Try to find the UsersRightPanel
                        var usersRightPanel = FindUsersRightPanel();

                        if (usersRightPanel != null)
                        {
                            DebugService.LogInfo("App", $"Updating user {username} status to {status} in UsersRightPanel");

                            // Get the Users collection from the panel
                            var usersProperty = usersRightPanel.GetType().GetProperty("Users");
                            if (usersProperty != null)
                            {
                                var users = usersProperty.GetValue(usersRightPanel) as ObservableCollection<UserViewModel>;
                                if (users != null)
                                {
                                    // Find the user in the collection
                                    var user = users.FirstOrDefault(u => u.UserId == userId);
                                    if (user != null)
                                    {
                                        // Update the user's status
                                        user.Status = status;
                                        if (!string.IsNullOrEmpty(statusMessage))
                                        {
                                            user.StatusMessage = statusMessage;
                                        }

                                        DebugService.LogInfo("App", $"Updated user {username} status in UsersRightPanel");
                                    }
                                    else
                                    {
                                        DebugService.LogInfo("App", $"User {username} not found in UsersRightPanel, refreshing panel");

                                        // User not found in the collection, try to refresh the panel
                                        var refreshMethod = usersRightPanel.GetType().GetMethod("RefreshUsersAsync",
                                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                                        if (refreshMethod != null)
                                        {
                                            // Invoke the refresh method
                                            var task = refreshMethod.Invoke(usersRightPanel, null) as Task;
                                            if (task != null)
                                            {
                                                await task;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            DebugService.LogInfo("App", "UsersRightPanel not currently loaded");
                            // No need to do anything - the panel will load the latest data when it appears
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugService.LogError("App", $"Error updating UsersRightPanel: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                DebugService.LogError("App", $"Error in UpdateUsersRightPanel: {ex.Message}");
            }
        }

        /// <summary>
        /// Finds the UsersRightPanel instance if it's currently loaded
        /// </summary>
        private UsersRightPanel FindUsersRightPanel()
        {
            try
            {
                // Check if we're currently on the users route
                bool isOnUsersRoute = Shell.Current.CurrentState.Location?.OriginalString?.EndsWith("//users") ?? false;

                if (isOnUsersRoute)
                {
                    // If we're on the users route, the current page should be the UsersRightPanel
                    if (Shell.Current.CurrentPage is UsersRightPanel panel)
                    {
                        return panel;
                    }
                }

                // Try to find the UsersRightPanel in the Shell items
                foreach (var item in Shell.Current.Items)
                {
                    if (item is ShellItem shellItem && shellItem.Route == "users")
                    {
                        foreach (var section in shellItem.Items)
                        {
                            if (section is ShellSection shellSection)
                            {
                                foreach (var content in shellSection.Items)
                                {
                                    if (content is ShellContent shellContent)
                                    {
                                        if (shellContent.Content is UsersRightPanel panel)
                                        {
                                            return panel;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                DebugService.LogError("App", $"Error finding UsersRightPanel: {ex.Message}");
                return null;
            }
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

            DebugService.LogInfo("App", "Application going to sleep (minimized/backgrounded)");

            // For mobile platforms, we might want to keep the user online for a short period
            // or handle push notifications. This is handled by the platform-specific services.
            // On desktop, we do nothing here as the app is still running.
        }

        private void OnWindowDestroying(object? sender, EventArgs e)
        {
            DebugService.LogInfo("App", "Window is destroying, setting user offline and unregistering WebSocket handlers.");
            UpdateUserStatusToOffline();
            UnregisterWebSocketEventHandlers();
        }

        private void UpdateUserStatusToOffline()
        {
            if (CurrentUser != null && CurrentUser.UserID > 0)
            {
                try
                {
                    var userPresenceService = Services.GetService<IUserPresenceService>();
                    if (userPresenceService != null)
                    {
                        // Use a timeout to prevent hanging during shutdown
                        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

                        // Run this in a fire-and-forget manner since we're shutting down
                        Task.Run(async () =>
                        {
                            try
                            {
                                DebugService.LogInfo("App", $"Setting user {CurrentUser.UserName} (ID: {CurrentUser.UserID}) status to Offline on app closing");

                                // Use the cancellation token to prevent hanging
                                await userPresenceService.UpdateStatusAsync(
                                    UserPresenceStatus.Offline,
                                    "",
                                    cts.Token);
                            }
                            catch (OperationCanceledException)
                            {
                                DebugService.LogWarning("App", "Setting user status to Offline was cancelled due to timeout");
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
            else if (CurrentUser != null && CurrentUser.UserID <= 0)
            {
                DebugService.LogWarning("App", $"Invalid user ID ({CurrentUser.UserID}), skipping status update on app closing");
            }
        }

        /// <summary>
        /// Registers event handlers for the WebSocketService
        /// </summary>
        public void RegisterWebSocketEventHandlers()
        {
            try
            {
                if (Services != null)
                {
                    var webSocketService = Services.GetService<IWebSocketService>();
                    if (webSocketService != null)
                    {
                        DebugService.LogInfo("App", "Registering WebSocketService event handlers");

                        // Unregister first to avoid duplicate handlers
                        UnregisterWebSocketEventHandlers();

                        // Register event handlers
                        webSocketService.NotificationReceived += new EventHandler<TDFShared.DTOs.Messages.NotificationEventArgs>(OnNotificationReceived);
                        webSocketService.ChatMessageReceived += OnChatMessageReceived;
                        webSocketService.ConnectionStatusChanged += OnConnectionStatusChanged;
                        webSocketService.UserStatusChanged += OnUserStatusChanged;
                        webSocketService.ErrorReceived += OnWebSocketError;

                        DebugService.LogInfo("App", "WebSocketService event handlers registered successfully");
                    }
                    else
                    {
                        DebugService.LogWarning("App", "WebSocketService not available, cannot register event handlers");
                    }
                }
            }
            catch (Exception ex)
            {
                DebugService.LogError("App", $"Error registering WebSocketService event handlers: {ex.Message}");
            }
        }

        /// <summary>
        /// Unregisters event handlers for the WebSocketService
        /// </summary>
        public void UnregisterWebSocketEventHandlers()
        {
            try
            {
                if (Services != null)
                {
                    var webSocketService = Services.GetService<IWebSocketService>();
                    if (webSocketService != null)
                    {
                        DebugService.LogInfo("App", "Unregistering WebSocketService event handlers");

                        // Unregister event handlers
                        webSocketService.NotificationReceived -= new EventHandler<TDFShared.DTOs.Messages.NotificationEventArgs>(OnNotificationReceived);
                        webSocketService.ChatMessageReceived -= OnChatMessageReceived;
                        webSocketService.ConnectionStatusChanged -= OnConnectionStatusChanged;
                        webSocketService.UserStatusChanged -= OnUserStatusChanged;
                        webSocketService.ErrorReceived -= OnWebSocketError;
                    }
                }
            }
            catch (Exception ex)
            {
                DebugService.LogError("App", $"Error unregistering WebSocketService event handlers: {ex.Message}");
            }
        }

        protected override void OnResume()
        {
            base.OnResume();

            DebugService.LogInfo("App", "Application resuming");
            
            if (DeviceHelper.IsMobile)
            {
                // Re-register WebSocketService event handlers if the user is logged in
                if (CurrentUser != null)
                {
                    RegisterWebSocketEventHandlers();

                    // Check if WebSocket is connected and reconnect if needed
                    if (Services != null)
                    {
                        var webSocketService = Services.GetService<IWebSocketService>();
                        var secureStorage = Services.GetService<SecureStorageService>();

                        if (webSocketService != null && !webSocketService.IsConnected && secureStorage != null)
                        {
                            DebugService.LogInfo("App", "WebSocket not connected, attempting to reconnect");

                            // Reconnect in a background task
                            Task.Run(async () =>
                            {
                                try
                                {
                                    // For mobile platforms, use the stored token
                                    var (token, expiration) = await secureStorage.GetTokenAsync();
                                    if (!string.IsNullOrEmpty(token) && expiration > DateTime.UtcNow)
                                    {
                                        await webSocketService.ConnectAsync(token);
                                        DebugService.LogInfo("App", "WebSocket reconnected successfully");
                                    }
                                    else
                                    {
                                        DebugService.LogWarning("App", "WebSocket reconnect skipped: token missing or expired.");
                                        // Only redirect to login if token is actually expired, not just because we're resuming
                                        if (expiration <= DateTime.UtcNow)
                                        {
                                            // Handle token expiration by redirecting to login
                                            await MainThread.InvokeOnMainThreadAsync(async () =>
                                            {
                                                try
                                                {
                                                    // Clear current user and token
                                                    _userSessionService?.SetCurrentUser(null);
                                                    await secureStorage.RemoveTokenAsync();

                                                    // Navigate to login page
                                                    var loginPage = Services?.GetService<LoginPage>();
                                                    if (loginPage != null)
                                                    {
                                                        MainPage = loginPage;
                                                    }
                                                    else
                                                    {
                                                        await Shell.Current.GoToAsync("LoginPage");
                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    DebugService.LogError("App", $"Error handling token expiration: {ex.Message}");
                                                }
                                            });
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    DebugService.LogError("App", $"Error reconnecting WebSocket: {ex.Message}");
                                }
                            });
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Dynamically applies the correct theme resource dictionary for colors by using ThemeHelper.
        /// </summary>
        private void SetThemeColors()
        {
            try
            {
                // Add adaptive theme bindings programmatically to ensure proper initialization order
                AddAdaptiveThemeBindings();

                // Let ThemeHelper handle theme application
                ThemeHelper.ApplyTheme();

                // Apply platform-specific theme adaptations
                ThemeHelper.ApplyPlatformSpecificAdaptations();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting theme colors: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// Add adaptive theme bindings programmatically to ensure proper initialization order
        /// </summary>
        private void AddAdaptiveThemeBindings()
        {
            try
            {
                var resources = Application.Current.Resources;
                // Assign adaptive resources directly based on the current theme
                // These resources are SolidColorBrush, not Color, so we need to extract the Color from them
                if (resources["TextColor"] is SolidColorBrush textBrush)
                    resources["AdaptiveTextColor"] = textBrush.Color;
                if (resources["BackgroundColor"] is SolidColorBrush backgroundBrush)
                    resources["AdaptiveBackgroundColor"] = backgroundBrush.Color;
                if (resources["SurfaceColor"] is SolidColorBrush surfaceBrush)
                    resources["AdaptiveSurfaceColor"] = surfaceBrush.Color;
                if (resources["BorderColor"] is SolidColorBrush borderBrush)
                    resources["AdaptiveBorderColor"] = borderBrush.Color;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error adding adaptive theme bindings: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
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
                            Command = new Command(async () => {
                                // Always use Shell navigation for retry
                                if (Services != null)
                                {
                                    var shell = Services.GetService<AppShell>();
                                    if (shell != null)
                                    {
                                        MainPage = shell;
                                        await Shell.Current.GoToAsync("LoginPage");
                                    }
                                }
                            })
                        }
                    }
                }
            };
        }

        private void OnWebSocketError(object sender, WebSocketErrorEventArgs e)
        {
            _logger.LogWarning("WebSocket error received: {ErrorCode} - {ErrorMessage}", e.ErrorCode, e.ErrorMessage);

            // Only redirect to login page on mobile platforms
            if (e.ErrorCode == "401" && DeviceHelper.ShouldHandleAutoReconnect)
            {
                _logger.LogWarning("WebSocket authentication failed, redirecting to login page");
                if (Services != null)
                {
                    var loginPage = Services.GetService<LoginPage>();
                    if (loginPage != null)
                    {
                        MainPage = loginPage;
                    }
                    else
                    {
                        _logger.LogError("Failed to get LoginPage from service provider");
                    }
                }
            }
            else if (e.ErrorCode == "401")
            {
                _logger.LogInformation("WebSocket authentication failed on desktop platform - skipping login redirect");
            }
        }
        public new Page MainPage
        {
            get => base.MainPage;
            set
            {
                System.Diagnostics.Debug.WriteLine($"[App] MainPage setter called. Old: {base.MainPage?.GetType().Name ?? "null"}, New: {value?.GetType().Name ?? "null"}");
                base.MainPage = value;
            }
        }
        /// <summary>
        /// Ensures all necessary resource dictionaries are merged into the application's resources.
        /// This prevents issues with AppThemeBinding by keeping all theme dictionaries present.
        /// </summary>
        private void EnsureAllResourceDictionariesMerged()
        {
            try
            {
                var merged = Application.Current?.Resources?.MergedDictionaries;
                if (merged == null) return;

                // Define the expected resource dictionary sources
                var expectedSources = new List<string>
                {
                    "Resources/Styles/Colors.xaml",
                    "Resources/Styles/PlatformColors.xaml",
                    "Resources/Styles/Styles.xaml"
                };

                foreach (var source in expectedSources)
                {
                    if (!merged.Any(d => d.Source?.OriginalString?.Contains(source) == true))
                    {
                        // If a dictionary is missing, add it
                        merged.Add(new ResourceDictionary { Source = new Uri(source, UriKind.Relative) });
                        System.Diagnostics.Debug.WriteLine($"Added missing resource dictionary: {source}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error ensuring all resource dictionaries are merged: {ex.Message}");
            }
        }

#if ANDROID || IOS
        private void SetupFirebaseCloudMessaging()
        {
            try
            {
                // Subscribe to FCM notification received event
                CrossFirebaseCloudMessaging.Current.NotificationReceived += async (sender, e) =>
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"FCM Notification received: {e.Notification?.Title}");
                        
                        // Show as local notification on Android and iOS
#if ANDROID || IOS
                        var platformNotificationService = Services?.GetService<IPlatformNotificationService>();
                        if (platformNotificationService != null)
                        {
                            await platformNotificationService.ShowLocalNotificationAsync(
                                 e.Notification?.Title ?? "Notification",
                                 e.Notification?.Body ?? "You have a new message",
                                 NotificationType.Info
                             );
                        }
#endif
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error handling FCM notification: {ex.Message}");
                    }
                };
                
                System.Diagnostics.Debug.WriteLine("Firebase Cloud Messaging setup completed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting up Firebase Cloud Messaging: {ex.Message}");
            }
        }
#endif

        private void CheckSafeMode()
        {
            try
            {
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
                }
#endif
                System.Diagnostics.Debug.WriteLine($"Application starting in {(SafeMode ? "SAFE MODE" : "NORMAL MODE")}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking safe mode: {ex.Message}");
                SafeMode = false;
            }
        }
    }
}
