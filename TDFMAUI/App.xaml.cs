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
using Plugin.Firebase.CloudMessaging;
using Plugin.Firebase.CloudMessaging.EventArgs;

namespace TDFMAUI
{
    public partial class App : Application
    {
        private static UserDto? _currentUser;
        private readonly ILogger<App> _logger;

        public static UserDto? CurrentUser
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
        public static event EventHandler? UserChanged;

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
                // Initialize logger
                _logger = Services?.GetService<ILogger<App>>() ?? 
                    throw new InvalidOperationException("Logger service not available");

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

                    // Initialize theme service for platform-aware theme adaptation
                    var themeService = Services?.GetService<ThemeService>();
                    themeService?.Initialize();
                    
                    // Dynamically merge the correct theme dictionary
                    SetThemeColors();
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

                // --- Plugin.Firebase v3+ push notification event handlers ---
                SetupFirebaseCloudMessaging();
                // --- End Plugin.Firebase push notification event handlers ---

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
            services.AddTransient<DashboardViewModel>();

            // Register pages
            services.AddTransient<LoginPage>();
            services.AddTransient<SignupPage>();
            services.AddTransient<MainPage>();
            services.AddTransient<ProfilePage>();
            services.AddTransient<MessagesPage>();
            services.AddTransient<RequestsPage>(); // Commented out missing page
            services.AddTransient<GlobalChatPage>();
            services.AddTransient<AdminPage>();
            services.AddTransient<DashboardPage>(); // Add DashboardPage
        }

        protected override async void OnStart()
        {
            try
            {
                DebugService.LogInfo("App", "OnStart method called");

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
                }

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
                if (Services == null)
                {
                    DebugService.LogError("App", "Services not initialized, cannot set up initial page");
                    DisplayFatalErrorPage("App services not initialized correctly.", null);
                    return;
                }

                ApiConfig.IsDevelopmentMode = true;

                if (SafeMode)
                {
                    DebugService.LogWarning("App", "*** STARTING IN SAFE MODE ***");
                    DebugService.LogWarning("App", "Some features will be disabled for stability");
                    await Shell.Current.GoToAsync("DiagnosticsPage");
                    return;
                }

                try
                {
                    var authService = Services.GetRequiredService<IAuthService>();
                    var secureStorage = Services.GetRequiredService<SecureStorageService>();
                    var logger = Services.GetRequiredService<ILogger<App>>();

                    bool hasValidToken = await secureStorage.HandleTokenPersistenceAsync();
                    UserDto currentUser = hasValidToken ? await authService.GetCurrentUserAsync() : null;
                    bool isAuthenticated = currentUser != null;

                    if (isAuthenticated)
                    {
                        logger.LogInformation("User is authenticated. Showing main shell.");
                        var shell = Services.GetRequiredService<AppShell>();
                        MainPage = shell;
                        RegisterWebSocketEventHandlers();
                        await Shell.Current.GoToAsync("DashboardPage");
                    }
                    else
                    {
                        logger.LogInformation("User is not authenticated. Showing login page.");
                        var loginPage = Services.GetRequiredService<TDFMAUI.Features.Auth.LoginPage>();
                        MainPage = new NavigationPage(loginPage);
                    }
                }
                catch (Exception ex)
                {
                    DebugService.LogError("App", $"Error preparing next page: {ex.Message}");
                    DebugService.LogError("App", $"Stack trace: {ex.StackTrace}");
                    await Shell.Current.GoToAsync("DiagnosticsPage");
                }
            }
            catch (Exception ex)
            {
                DebugService.LogError("App", $"Error setting up initial page: {ex.Message}");
                DebugService.LogError("App", $"Stack trace: {ex.StackTrace}");
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

            // When app goes to background, reconnect socket when it comes back
            DebugService.LogInfo("App", "Application going to sleep");

            // For desktop platforms, we should NOT clear token when app is just minimized
            // Only update user status to Offline
            if (DeviceHelper.IsDesktop)
            {
                DebugService.LogInfo("App", "Desktop platform detected, setting user offline without clearing token");
            
                // Update user status to Offline
                UpdateUserStatusToOffline();
            }
            // For non-mobile large screen devices, just update status to Offline
            else if (!DeviceHelper.IsMobile && DeviceHelper.IsLargeScreen)
            {
                UpdateUserStatusToOffline();
            }

            // Unregister WebSocketService event handlers to prevent memory leaks
            UnregisterWebSocketEventHandlers();
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
                        // Use a timeout to prevent hanging during shutdown
                        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

                        // Run this in a fire-and-forget manner since we're shutting down
                        Task.Run(async () =>
                        {
                            try
                            {
                                DebugService.LogInfo("App", "Setting user status to Offline on app closing");

                                // Use the cancellation token to prevent hanging
                                await userPresenceService.UpdateStatusAsync(
                                    TDFShared.Enums.UserPresenceStatus.Offline,
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
                                                CurrentUser = null;
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

        /// <summary>
        /// Dynamically applies the correct theme resource dictionary for colors by using ThemeHelper.
        /// </summary>
        private void SetThemeColors()
        {
            try
            {
                // Configure the merged dictionaries for the current theme
                ConfigureThemeDictionaries();
                
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
                resources["AdaptiveTextColor"] = resources.ContainsKey("TextColor") ? resources["TextColor"] : Colors.Black;
                resources["AdaptiveBackgroundColor"] = resources.ContainsKey("BackgroundColor") ? resources["BackgroundColor"] : Colors.White;
                resources["AdaptiveSurfaceColor"] = resources.ContainsKey("SurfaceColor") ? resources["SurfaceColor"] : Colors.White;
                resources["AdaptiveBorderColor"] = resources.ContainsKey("BorderColor") ? resources["BorderColor"] : Colors.Gray;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error adding adaptive theme bindings: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Configure the merged dictionaries for the current theme
        /// </summary>
        private void ConfigureThemeDictionaries()
        {
            try
            {
                var appTheme = Application.Current?.RequestedTheme ?? AppTheme.Light;
                var merged = Application.Current.Resources.MergedDictionaries;
                
                if (merged == null || merged.Count == 0)
                    return;
                
                // Find the theme dictionaries
                var colorsDict = merged.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Colors.xaml"));
                var lightDict = merged.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Colors.Light.xaml"));
                var darkDict = merged.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Colors.Dark.xaml"));
                var platformDict = merged.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("PlatformColors.xaml"));
                var stylesDict = merged.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Styles.xaml"));
                
                // Create a new ordered list of dictionaries
                var orderedDictionaries = new List<ResourceDictionary>();
                
                // Add the appropriate theme dictionary first
                if (appTheme == AppTheme.Dark && darkDict != null)
                {
                    orderedDictionaries.Add(darkDict);
                }
                else if (lightDict != null)
                {
                    orderedDictionaries.Add(lightDict);
                }
                
                // Add colors dictionary next
                if (colorsDict != null && !orderedDictionaries.Contains(colorsDict))
                {
                    orderedDictionaries.Add(colorsDict);
                }
                
                // Add platform colors dictionary after the theme colors
                if (platformDict != null && !orderedDictionaries.Contains(platformDict))
                {
                    orderedDictionaries.Add(platformDict);
                }
                
                // Add all other dictionaries except styles
                foreach (var dict in merged)
                {
                    if (dict != lightDict && dict != darkDict && dict != stylesDict && dict != colorsDict && dict != platformDict)
                    {
                        orderedDictionaries.Add(dict);
                    }
                }
                
                // Add styles dictionary last
                if (stylesDict != null)
                {
                    orderedDictionaries.Add(stylesDict);
                }
                
                // Replace the merged dictionaries with our ordered list
                merged.Clear();
                foreach (var dict in orderedDictionaries)
                {
                    merged.Add(dict);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error configuring theme dictionaries: {ex.Message}");
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

        private void SetupFirebaseCloudMessaging()
        {
#if ANDROID || IOS
            try
            {
                // Subscribe to notification received
                CrossFirebaseCloudMessaging.Current.NotificationReceived += async (sender, e) =>
                {
                    try
                    {
                        var notification = e.Notification;
                        System.Diagnostics.Debug.WriteLine($"FCM Notification Received: {notification.Title} - {notification.Body}");
                        var notificationService = Application.Current?.Handler?.MauiContext?.Services?.GetService<IPushNotificationService>();
                        if (notificationService != null && notification != null)
                        {
                            await notificationService.ShowLocalNotificationAsync(
                                notification.Title ?? "New Message",
                                notification.Body ?? string.Empty,
                                NotificationType.Info,
                                notification.Data?.ToString()
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error handling FCM notification: {ex.Message}");
                    }
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting up FCM: {ex.Message}");
            }
#else
            // FCM is not supported on this platform
            System.Diagnostics.Debug.WriteLine("Firebase Cloud Messaging is not supported on this platform");
#endif
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
    }
}
