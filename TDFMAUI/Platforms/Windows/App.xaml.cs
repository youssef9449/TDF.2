﻿using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using TDFMAUI.Services;
using TDFShared.Enums;
using TDFShared.Constants;
using TDFMAUI.Helpers;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Activation;
using Microsoft.UI.Dispatching;
using WinRT;
using Microsoft.Win32;
using System.Reflection;
using System.Linq;
using Microsoft.Maui.Hosting;
using Microsoft.Maui;
using Microsoft.Extensions.DependencyInjection;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace TDFMAUI.WinUI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : MauiWinUIApplication
    {
        private bool _uiThreadExceptionHandlersRegistered = false;
        private bool _fallbackWindowShown = false;
        private static readonly TimeSpan LaunchInitializationTimeout = TimeSpan.FromSeconds(45);
        private const int MauiReadyTimeoutMilliseconds = 30000;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            // Set up global exception handlers first
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            // We can't use Exiting event directly in WinUI 3
            // Instead, we'll use the Application.Current.Suspending event
            Microsoft.Maui.ApplicationModel.AppActions.OnAppAction += (sender, args) => {
                if (args.AppAction.Id == "app_closing")
                {
                    // Update user status to Offline when the app is closing
                    if (DeviceHelper.IsDesktop)
                    {
                        UpdateUserStatusToOffline();
                        // Also try the DeviceHelper method as a backup
                        try
                        {
                            Task.Run(async () => await TDFMAUI.Helpers.DeviceHelper.UpdateUserStatusToOfflineOnExit()).Wait(1000);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error calling DeviceHelper.UpdateUserStatusToOfflineOnExit: {ex.Message}");
                        }
                    }
                }
            };

            // Ensure XAML resources are loaded before MAUI app initialization.
            // If this fails we should fail fast instead of continuing with missing
            // theme/style resources that cause hard-to-diagnose startup issues.
            InitializeComponent();

            try
            {
                // Ensure Windows App SDK components are properly initialized
                EnsureWindowsAppSDKInitialized();

                // Check for required Windows UI assemblies before initialization
                CheckCriticalWindowsAssemblies();

                Debug.WriteLine("WinUI App initialization completed successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in WinUI post-XAML initialization: {ex.Message}");
                SafeLogException(ex, "WinUI_PostXamlInitialization");

                try
                {
                    TryToFixWindowsAppSDKRegistration();
                }
                catch (Exception fixEx)
                {
                    Debug.WriteLine($"Failed to fix Windows App SDK: {fixEx.Message}");
                    SafeLogException(fixEx, "WindowsAppSDK_Fix_Failed");
                }

                EnsureFallbackWindow(ex);
            }

            // Register for process exit (this is a more reliable approach)
            AppDomain.CurrentDomain.ProcessExit += (sender, e) => {
                try
                {
                    // Update user status to Offline when the app is closing
                    if (DeviceHelper.IsDesktop)
                    {
                        UpdateUserStatusToOffline();
                        // Also try the DeviceHelper method as a backup
                        try
                        {
                            Task.Run(async () => await TDFMAUI.Helpers.DeviceHelper.UpdateUserStatusToOfflineOnExit()).Wait(1000);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error calling DeviceHelper.UpdateUserStatusToOfflineOnExit: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error during process exit: {ex.Message}");
                    SafeLogException(ex, "ProcessExit");
                }
            };
        }

        /// <summary>
        /// Ensures Windows App SDK is properly initialized
        /// </summary>
        private void EnsureWindowsAppSDKInitialized()
        {
            try
            {
                Debug.WriteLine("Ensuring Windows App SDK is properly initialized...");

                string baseDir = AppContext.BaseDirectory;
                var uiXamlVersion = ValidateCriticalAssembly(baseDir, "Microsoft.UI.Xaml.dll", new[] { "Microsoft.WinUI.dll" });
                var winUiVersion = ValidateCriticalAssembly(baseDir, "Microsoft.WinUI.dll", Array.Empty<string>());

                if (uiXamlVersion != null && winUiVersion != null && (uiXamlVersion.Major != winUiVersion.Major || uiXamlVersion.Minor != winUiVersion.Minor))
                {
                    throw new InvalidOperationException($"WinUI assembly version mismatch detected. Microsoft.UI.Xaml.dll={uiXamlVersion}, Microsoft.WinUI.dll={winUiVersion}.");
                }

                var windowsAppSDKDlls = Directory.GetFiles(baseDir, "Microsoft.WindowsAppRuntime.*.dll");
                if (windowsAppSDKDlls.Length == 0)
                {
                    throw new FileNotFoundException($"No WindowsAppSDK runtime DLLs were found in '{baseDir}'.");
                }

                Version? baselineRuntimeVersion = null;
                foreach (var dll in windowsAppSDKDlls)
                {
                    var versionInfo = FileVersionInfo.GetVersionInfo(dll);
                    var parsedVersion = ParseFileVersionOrThrow(versionInfo.FileVersion, dll);
                    _ = AssemblyName.GetAssemblyName(dll);

                    Debug.WriteLine($"WindowsAppSDK runtime: {Path.GetFileName(dll)} v{versionInfo.FileVersion}");

                    baselineRuntimeVersion ??= parsedVersion;
                    if (parsedVersion.Major != baselineRuntimeVersion.Major || parsedVersion.Minor != baselineRuntimeVersion.Minor)
                    {
                        throw new InvalidOperationException(
                            $"WindowsAppSDK runtime DLL version mismatch detected. Baseline={baselineRuntimeVersion}, {Path.GetFileName(dll)}={parsedVersion}.");
                    }
                }

                var bootstrapDllPath = Path.Combine(baseDir, "Microsoft.WindowsAppRuntime.Bootstrap.dll");
                if (File.Exists(bootstrapDllPath))
                {
                    _ = AssemblyName.GetAssemblyName(bootstrapDllPath);
                }
                else
                {
                    Debug.WriteLine($"WARNING: Bootstrap assembly not found at {bootstrapDllPath}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error ensuring Windows App SDK initialization: {ex.Message}");
                SafeLogException(ex, "WindowsAppSDK_Initialization");
                throw;
            }
        }

        private static Version ParseFileVersionOrThrow(string? fileVersion, string assemblyPath)
        {
            var token = fileVersion?.Split(' ').FirstOrDefault();
            if (Version.TryParse(token, out var parsedVersion))
            {
                return parsedVersion;
            }

            throw new InvalidOperationException($"Unable to parse file version '{fileVersion}' for assembly '{assemblyPath}'.");
        }

        /// <summary>
        /// Checks for critical Windows UI assemblies before initialization
        /// </summary>
        private void CheckCriticalWindowsAssemblies()
        {
            try
            {
                var criticalAssemblies = new[] {
                    "Microsoft.UI.Xaml.dll",
                    "Microsoft.WinUI.dll"
                };

                Debug.WriteLine("Checking critical Windows UI assemblies...");

                var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => criticalAssemblies.Any(ca => a.FullName.Contains(ca.Replace(".dll", ""), StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                foreach (var assembly in loadedAssemblies)
                {
                    Debug.WriteLine($"Found loaded assembly: {assembly.FullName}");
                    Debug.WriteLine($"  Location: {(string.IsNullOrEmpty(assembly.Location) ? "Unknown/Dynamic" : assembly.Location)}");
                }

                string baseDir = AppContext.BaseDirectory;
                foreach (var dllName in criticalAssemblies)
                {
                    var dllPath = Path.Combine(baseDir, dllName);
                    if (!File.Exists(dllPath))
                    {
                        throw new FileNotFoundException($"Missing critical Windows UI assembly: {dllPath}");
                    }
                }

                try
                {
                    using RegistryKey key = Registry.ClassesRoot.OpenSubKey("CLSID\\{2D913B79-1558-5B8E-B16A-CB78973BBA47}");
                    if (key == null)
                    {
                        Debug.WriteLine("WARNING: Microsoft.UI.Xaml COM registration missing");
                    }
                }
                catch (Exception)
                {
                    Debug.WriteLine("Could not check COM registration (requires admin rights)");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking Windows assemblies: {ex.Message}");
                SafeLogException(ex, "WindowsAssemblyValidation");
                throw;
            }
        }

        private static Version? ValidateCriticalAssembly(string baseDir, string assemblyName, string[] dependencies)
        {
            var assemblyPath = Path.Combine(baseDir, assemblyName);
            if (!File.Exists(assemblyPath))
            {
                throw new FileNotFoundException($"Missing required assembly '{assemblyName}' in output folder.", assemblyPath);
            }

            var versionInfo = FileVersionInfo.GetVersionInfo(assemblyPath);
            Debug.WriteLine($"Found {assemblyName} at {assemblyPath} with version {versionInfo.FileVersion}");

            var parsedVersion = ParseFileVersionOrThrow(versionInfo.FileVersion, assemblyPath);

            _ = AssemblyName.GetAssemblyName(assemblyPath);

            foreach (var dependency in dependencies)
            {
                var dependencyPath = Path.Combine(baseDir, dependency);
                if (!File.Exists(dependencyPath))
                {
                    throw new FileNotFoundException($"Dependency '{dependency}' for '{assemblyName}' is missing.", dependencyPath);
                }
            }

            return parsedVersion;
        }

        /// <summary>
        /// Attempts to fix Windows App SDK registration issues
        /// </summary>
        private void TryToFixWindowsAppSDKRegistration()
        {
            try
            {
                Debug.WriteLine("Attempting to fix Windows App SDK registration issues...");

                // Check for the specific DLL mentioned in the error message
                string uiXamlDllPath = Path.Combine(AppContext.BaseDirectory, "Microsoft.ui.xaml.dll");
                string uiXamlDllPathAlt = Path.Combine(AppContext.BaseDirectory, "Microsoft.UI.Xaml.dll");
                string winUiDllPath = Path.Combine(AppContext.BaseDirectory, "Microsoft.WinUI.dll");

                // Log diagnostic information
                Debug.WriteLine($"Looking for Microsoft.ui.xaml.dll at {uiXamlDllPath}");
                Debug.WriteLine($"Looking for Microsoft.UI.Xaml.dll at {uiXamlDllPathAlt}");
                Debug.WriteLine($"Looking for Microsoft.WinUI.dll at {winUiDllPath}");

                // Try to pre-load or register relevant DLLs
                string[] potentialDlls = {
                    // Check both casing versions as mentioned in the error logs
                    "Microsoft.ui.xaml.dll",
                    "Microsoft.UI.Xaml.dll",
                    "Microsoft.WinUI.dll",
                    "Microsoft.WindowsAppRuntime.Bootstrap.dll"
                };

                bool anyDllLoaded = false;

                foreach (var dll in potentialDlls)
                {
                    string dllPath = Path.Combine(AppContext.BaseDirectory, dll);
                    if (File.Exists(dllPath))
                    {
                        Debug.WriteLine($"Found DLL: {dllPath}");

                        try
                        {
                            // Force loading the assembly to assist with initialization
                            var assembly = Assembly.LoadFrom(dllPath);
                            Debug.WriteLine($"Successfully loaded {dll}: {assembly.FullName}");
                            anyDllLoaded = true;
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Failed to load {dll}: {ex.Message}");
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"DLL not found: {dllPath}");
                    }
                }

                // If no DLLs were loaded, try to find and load any WindowsAppRuntime DLLs
                if (!anyDllLoaded)
                {
                    Debug.WriteLine("No critical DLLs were loaded. Searching for any WindowsAppRuntime DLLs...");

                    // As a fallback, try searching for WindowsAppRuntime DLLs
                    var runtimeDlls = Directory.GetFiles(AppContext.BaseDirectory, "*.dll")
                        .Where(f => Path.GetFileName(f).Contains("WindowsAppRuntime") ||
                                    Path.GetFileName(f).Contains("WinUI") ||
                                    Path.GetFileName(f).Contains("UI.Xaml"))
                        .ToArray();

                    Debug.WriteLine($"Found {runtimeDlls.Length} potential WindowsAppRuntime/WinUI DLLs");

                    foreach (var dll in runtimeDlls)
                    {
                        Debug.WriteLine($"Found related DLL: {Path.GetFileName(dll)}");

                        try
                        {
                            // Try to load the assembly
                            var assembly = Assembly.LoadFrom(dll);
                            Debug.WriteLine($"Successfully loaded {Path.GetFileName(dll)}: {assembly.FullName}");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Failed to load {Path.GetFileName(dll)}: {ex.Message}");
                        }
                    }
                }

                // Provide guidance for the user
                Debug.WriteLine("RECOMMENDATION: If the application continues to fail, try the following:");
                Debug.WriteLine("1. Ensure Windows App SDK 1.7.1 is installed on your system");
                Debug.WriteLine("2. Repair the Windows App SDK installation");
                Debug.WriteLine("3. Rebuild the application with a clean configuration");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error fixing Windows App SDK registration: {ex.Message}");
                SafeLogException(ex, "WindowsAppSDK_Fix");
            }
        }



        private void Current_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            // Log the UI exception
            SafeLogException(e.Exception, "WinUI_UnhandledException");
            Debug.WriteLine($"WinUI XAML Exception: {e.Message}");

            // Look for the specific COM class not registered error
            if (e.Exception.HResult == unchecked((int)0x80040154) ||
                e.Exception.Message.Contains("80040154") ||
                e.Exception.Message.Contains("Class not registered"))
            {
                Debug.WriteLine("Detected Class not registered error (80040154) - attempting recovery");

                try
                {
                    // Try the repair approach
                    TryToFixWindowsAppSDKRegistration();

                    // Mark as handled to try to prevent a crash
                    e.Handled = true;

                    // Log a suggestion to reinstall the WindowsAppSDK
                    Debug.WriteLine("SUGGESTION: If the error persists, try reinstalling Windows App SDK 1.7.1 or repairing the installation");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to recover from COM registration error: {ex.Message}");
                }
            }

            // For all exceptions, mark as handled to prevent app crash if possible
            e.Handled = true;
        }

        private void SetupUIThreadExceptionHandling()
        {
            if (_uiThreadExceptionHandlersRegistered)
            {
                return;
            }

            try
            {
                // Get the dispatcher queue for the current thread (UI thread)
                var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
                if (dispatcherQueue != null)
                {
                    Debug.WriteLine("UI thread dispatcher queue obtained successfully");
                }

                if (Microsoft.UI.Xaml.Application.Current != null)
                {
                    Microsoft.UI.Xaml.Application.Current.UnhandledException -= Current_UnhandledException;
                    Microsoft.UI.Xaml.Application.Current.UnhandledException += Current_UnhandledException;
                    Debug.WriteLine("Registered UnhandledException handler for UI exceptions");
                }
                else
                {
                    Debug.WriteLine("WARNING: Microsoft.UI.Xaml.Application.Current is null, cannot register exception handler");
                }

                _uiThreadExceptionHandlersRegistered = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to set up UI thread exception handling: {ex.Message}");
                SafeLogException(ex, "UIThreadExceptionSetup");
            }
        }

        private void RecoverFromInitializationFailure(Exception ex)
        {
            Debug.WriteLine("Attempting to recover from initialization failure");

            try
            {
                // Log detailed information about the failure
                SafeLogException(ex, "InitializationFailure_Recovery");

                // Keep process alive long enough to render a fallback error UI.
                EnsureFallbackWindow(ex);
            }
            catch (Exception recoveryEx)
            {
                Debug.WriteLine($"Recovery attempt failed: {recoveryEx.Message}");
                SafeLogException(recoveryEx, "RecoveryFailure");
            }
        }

        // Use Application.Current.Exit event instead of OnLaunched
        protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            try
            {
                Debug.WriteLine("OnLaunched: Starting initialization");
                Debug.WriteLine("[Windows.App] OnLaunched called - about to initialize MAUI app");

                // Set up UI thread exception handling before calling base implementation
                SetupUIThreadExceptionHandling();

                using var launchCts = new CancellationTokenSource(LaunchInitializationTimeout);

                // Call base implementation - this is critical
                try
                {
                    Debug.WriteLine("[Windows.App] About to call base.OnLaunched - this should trigger MAUI app creation");
                    base.OnLaunched(args);
                    Debug.WriteLine("OnLaunched: Base implementation completed successfully");
                    Debug.WriteLine("[Windows.App] base.OnLaunched completed - MAUI app should now be created");

                    // Immediately activate the window so UI is visible even if startup navigation stalls
                    TryActivateMainWindow("WindowActivationImmediate");
                    
                    // Initialize the main MAUI app after base.OnLaunched completes
                    Debug.WriteLine("[Windows.App] About to call main app initialization");
                    Debug.WriteLine($"[Windows.App] Application.Current type: {Microsoft.Maui.Controls.Application.Current?.GetType().FullName ?? "null"}");
                    
                    // Initialize the app on the UI thread.
                    // Running OnStartAsync via Task.Run causes cross-thread access during
                    // theme/resource initialization and window navigation on WinUI.
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        Debug.WriteLine("[Windows.App] UI-thread startup initialization started");

                        var appReady = await WaitForMauiAppReadyAsync(MauiReadyTimeoutMilliseconds, 50, launchCts.Token);
                        if (!appReady)
                        {
                            throw new TimeoutException("Timed out waiting for MAUI application and window initialization.");
                        }

                        Debug.WriteLine("[Windows.App] About to check for TDFMAUI.App instance");
                        if (Microsoft.Maui.Controls.Application.Current is not TDFMAUI.App mainApp)
                        {
                            var appType = Microsoft.Maui.Controls.Application.Current?.GetType().FullName ?? "null";
                            Debug.WriteLine("[Windows.App] ERROR: Could not find main TDFMAUI.App instance");
                            Debug.WriteLine($"[Windows.App] Available app type: {appType}");
                            throw new InvalidOperationException($"Expected TDFMAUI.App after launch, but found '{appType}'.");
                        }

                        Debug.WriteLine("[Windows.App] Found main TDFMAUI.App instance, calling OnStartAsync");
                        await mainApp.OnStartAsync(launchCts.Token).WaitAsync(launchCts.Token);
                        Debug.WriteLine("[Windows.App] Main app OnStartAsync completed successfully");

                        // Retry activation after startup initialization. Activate() is idempotent,
                        // and retrying protects against a first activation that did not make the
                        // window visible.
                        TryActivateMainWindow("WindowActivationAfterOnStartAsync");
                    });
                }
                catch (Exception baseEx)
                {
                    Debug.WriteLine($"Error in base.OnLaunched: {baseEx.Message}");
                    SafeLogException(baseEx, "Base_OnLaunched");

                    // Try to fix Windows App SDK registration and continue with fallback UI.
                    TryToFixWindowsAppSDKRegistration();
                    RecoverFromInitializationFailure(baseEx);
                }

                // Register for window closed event
                try
                {
                    if (Microsoft.Maui.Controls.Application.Current?.Windows.Count > 0)
                    {
                        var mainWindow = Microsoft.Maui.Controls.Application.Current.Windows[0].Handler?.PlatformView as Microsoft.UI.Xaml.Window;
                        if (mainWindow != null)
                        {
                            mainWindow.Closed += (sender, e) => {
                                try
                                {
                                    Debug.WriteLine("Window.Closed event triggered");

                                    // Update user status to Offline
                                    if (DeviceHelper.IsDesktop)
                                    {
                                        UpdateUserStatusToOffline();
                                        // Also try the DeviceHelper method as a backup
                                        try
                                        {
                                            Task.Run(async () => await TDFMAUI.Helpers.DeviceHelper.UpdateUserStatusToOfflineOnExit()).Wait(1000);
                                        }
                                        catch (Exception ex)
                                        {
                                            Debug.WriteLine($"Error calling DeviceHelper.UpdateUserStatusToOfflineOnExit: {ex.Message}");
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"Error during window close: {ex.Message}");
                                    SafeLogException(ex, "WindowClosed");
                                }
                            };
                            Debug.WriteLine("Window.Closed event handler registered");
                        }
                        else
                        {
                            Debug.WriteLine("WARNING: Could not get main window reference");
                        }
                    }
                    else
                    {
                        Debug.WriteLine("WARNING: No windows available in Application.Current.Windows");
                    }
                }
                catch (Exception windowEx)
                {
                    Debug.WriteLine($"Error setting up window event handlers: {windowEx.Message}");
                    SafeLogException(windowEx, "WindowSetup");
                }

                Debug.WriteLine("OnLaunched: Initialization completed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Critical error in OnLaunched: {ex.Message}");
                SafeLogException(ex, "OnLaunched_Critical");

                // Keep process alive and surface a fallback UI if launch cannot complete.
                RecoverFromInitializationFailure(ex);
            }
        }

        private static async Task<bool> WaitForMauiAppReadyAsync(int timeoutMilliseconds = 10000, int pollIntervalMilliseconds = 50, CancellationToken cancellationToken = default)
        {
            var timeoutAt = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);

            while (DateTime.UtcNow < timeoutAt)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var currentApp = Microsoft.Maui.Controls.Application.Current;
                var hasWindow = currentApp?.Windows?.Count > 0;

                if (currentApp is TDFMAUI.App && hasWindow)
                {
                    return true;
                }

                await Task.Delay(pollIntervalMilliseconds, cancellationToken);
            }

            return false;
        }

        private void UpdateUserStatusToOffline()
        {
            try
            {
                // Get the current user
                var currentUser = TDFMAUI.App.CurrentUser;
                if (currentUser != null)
                {
                    Debug.WriteLine($"Updating status to Offline for user: {currentUser.UserName} (ID: {currentUser.UserID})");
                    
                    // Get the UserPresenceService from DI
                    var services = IPlatformApplication.Current?.Services;
                    if (services != null)
                    {
                        var userPresenceService = services.GetService<IUserPresenceService>();
                        if (userPresenceService != null)
                        {
                            // Run this synchronously since we're shutting down
                            Task.Run(async () =>
                            {
                                try
                                {
                                    // Validate user ID before proceeding
                                    if (currentUser.UserID <= 0)
                                    {
                                        Debug.WriteLine($"Invalid user ID ({currentUser.UserID}), skipping status update on Windows app exit");
                                        return;
                                    }

                                    Debug.WriteLine($"Setting user {currentUser.UserName} (ID: {currentUser.UserID}) status to Offline on Windows app exit");
                                    
                                    // Update through the presence service which handles both WebSocket and API calls
                                    await userPresenceService.UpdateStatusAsync(UserPresenceStatus.Offline, "");
                                    
                                    Debug.WriteLine("Successfully completed offline status update process");
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"Error updating user status to Offline: {ex.Message}");
                                    if (ex.InnerException != null)
                                    {
                                        Debug.WriteLine($"Inner exception: {ex.InnerException.Message}");
                                    }
                                }
                            }).Wait(TimeSpan.FromSeconds(5)); // Wait up to 5 seconds for slower systems/network
                        }
                        else
                        {
                            Debug.WriteLine("UserPresenceService could not be resolved from DI container");
                        }
                    }
                    else
                    {
                        Debug.WriteLine("Services collection is null, cannot resolve UserPresenceService");
                    }
                }
                else
                {
                    Debug.WriteLine("Current user is null, cannot update status to Offline");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in UpdateUserStatusToOffline: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Debug.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
            }
        }

        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            SafeLogException(e.Exception, "TaskScheduler_UnobservedTaskException");
            e.SetObserved(); // Mark as observed to prevent process termination if possible
        }

        private void CurrentDomain_UnhandledException(object sender, System.UnhandledExceptionEventArgs e)
        {
            SafeLogException(e.ExceptionObject as Exception, "CurrentDomain_UnhandledException");
            // Process may still terminate depending on e.IsTerminating
        }

        protected override MauiApp CreateMauiApp()
        {
            try
            {
                Debug.WriteLine("[Windows.App] CreateMauiApp called - about to call MauiProgram.CreateMauiApp()");
                var app = MauiProgram.CreateMauiApp();
                Debug.WriteLine("[Windows.App] MauiProgram.CreateMauiApp() returned successfully");
                return app;
            }
            catch (Exception ex)
            {
                SafeLogException(ex, "CreateMauiApp");
                // Optionally rethrow or handle as critical failure
                throw;
            }
        }

        private void TryActivateMainWindow(string context)
        {
            try
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        var mauiWindow = Microsoft.Maui.Controls.Application.Current?.Windows?.FirstOrDefault();
                        if (mauiWindow == null)
                        {
                            Debug.WriteLine($"No MAUI window found for activation ({context}).");
                            return;
                        }

                        var platformWindow = mauiWindow.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
                        if (platformWindow == null)
                        {
                            Debug.WriteLine($"Platform window unavailable for activation ({context}).");
                            return;
                        }

                        platformWindow.Activate();
                        Debug.WriteLine($"WinUI window activation attempted successfully ({context}).");
                    }
                    catch (Exception activateEx)
                    {
                        Debug.WriteLine($"Error during window activation ({context}): {activateEx.Message}");
                        SafeLogException(activateEx, context);
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to queue window activation ({context}): {ex.Message}");
                SafeLogException(ex, $"{context}_Queue");
            }
        }

        private void EnsureFallbackWindow(Exception ex)
        {
            if (_fallbackWindowShown)
            {
                return;
            }

            _fallbackWindowShown = true;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    var window = new Microsoft.UI.Xaml.Window
                    {
                        Content = new Microsoft.UI.Xaml.Controls.TextBlock
                        {
                            Text = $"The application encountered a startup error and attempted recovery. Please restart the app.\n\n{ex.Message}",
                            TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
                            Margin = new Microsoft.UI.Xaml.Thickness(24)
                        }
                    };

                    window.Activate();
                    Debug.WriteLine("Displayed fallback error window after initialization failure.");
                }
                catch (Exception fallbackEx)
                {
                    Debug.WriteLine($"Failed to show fallback window: {fallbackEx.Message}");
                    SafeLogException(fallbackEx, "FallbackWindow");
                }
            });
        }

        private static void SafeLogException(Exception ex, string context)
        {
            try
            {
                LogException(ex, context);
            }
            catch
            {
                Debug.WriteLine($"SafeLogException fallback: [{context}] {ex?.Message}");
            }
        }

        private static void LogException(Exception ex, string context)
        {
            if (ex == null) return;
            try
            {
                string logFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "tdfmaui_windows_crash.log");
                string errorMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{context}] Exception: {ex.GetType().FullName}\nMessage: {ex.Message}\nStackTrace:\n{ex.StackTrace}\n";
                if (ex.InnerException != null)
                {
                    errorMessage += $"\nInnerException: {ex.InnerException.GetType().FullName}\nMessage: {ex.InnerException.Message}\nStackTrace:\n{ex.InnerException.StackTrace}\n";
                }
                errorMessage += "------------------------------------------------------\n";
                File.AppendAllText(logFilePath, errorMessage);
                Debug.WriteLine(errorMessage);
            }
            catch (Exception logEx)
            {
                Debug.WriteLine($"Failed to log exception: {logEx.Message}");
            }
        }


    }
}
