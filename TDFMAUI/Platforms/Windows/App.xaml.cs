﻿using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using TDFMAUI.Services;
using TDFShared.Enums;
using TDFMAUI.Helpers;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Activation;
using Microsoft.UI.Dispatching;
using WinRT;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Reflection;
using System.Security.Principal;
using System.Linq;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace TDFMAUI.WinUI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : MauiWinUIApplication
    {
        // Import RegSvr32 functions to manually register DLLs if needed
        [DllImport("shell32.dll", SetLastError = true)]
        static extern IntPtr ShellExecute(IntPtr hwnd, string lpOperation, string lpFile, string lpParameters, string lpDirectory, int nShowCmd);

        // Constants for ShellExecute
        private const int SW_HIDE = 0;
        private const string OPERATION_OPEN = "open";

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
                    }
                }
            };

            try
            {
                // Ensure Windows App SDK components are properly initialized
                EnsureWindowsAppSDKInitialized();

                // Check for required Windows UI assemblies before initialization
                CheckCriticalWindowsAssemblies();

                // Initialize the component without any additional logic that could fail
                this.InitializeComponent();
                Debug.WriteLine("WinUI App initialization completed successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in WinUI initialization: {ex.Message}");
                LogException(ex, "WinUI_Initialization");

                // Try to recover from initialization failure
                try {
                    // Try to fix Windows App SDK registration
                    TryToFixWindowsAppSDKRegistration();
                } catch (Exception fixEx) {
                    Debug.WriteLine($"Failed to fix Windows App SDK: {fixEx.Message}");
                    LogException(fixEx, "WindowsAppSDK_Fix_Failed");
                }
            }

            // Register for process exit (this is a more reliable approach)
            AppDomain.CurrentDomain.ProcessExit += (sender, e) => {
                try
                {
                    // Update user status to Offline when the app is closing
                    if (DeviceHelper.IsDesktop)
                    {
                        UpdateUserStatusToOffline();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error during process exit: {ex.Message}");
                    LogException(ex, "ProcessExit");
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

                // Check if Microsoft.UI.Xaml.dll exists in the application directory
                string baseDir = AppContext.BaseDirectory;
                string uiXamlDllPath = Path.Combine(baseDir, "Microsoft.UI.Xaml.dll");

                if (File.Exists(uiXamlDllPath))
                {
                    Debug.WriteLine($"Found Microsoft.UI.Xaml.dll at {uiXamlDllPath}");

                    // Get version information
                    FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(uiXamlDllPath);
                    Debug.WriteLine($"Microsoft.UI.Xaml.dll version: {versionInfo.FileVersion}");

                    //    // Try to load the assembly to ensure it's valid
                    //    try
                    //    {
                    //        //var assembly = Assembly.LoadFrom(uiXamlDllPath);
                    //     //   Debug.WriteLine($"Successfully loaded Microsoft.UI.Xaml.dll: {assembly.FullName}");
                    //    }
                    //    catch (Exception ex)
                    //    {
                    //        Debug.WriteLine($"Failed to load Microsoft.UI.Xaml.dll: {ex.Message}");
                    //        // Don't throw here, we'll continue and try to initialize anyway
                    //    }
                }
                else
                {
                    Debug.WriteLine($"WARNING: Microsoft.UI.Xaml.dll not found at {uiXamlDllPath}");
                }

                // Check for WindowsAppSDK DLLs
                var windowsAppSDKDlls = Directory.GetFiles(baseDir, "Microsoft.WindowsAppRuntime.*.dll");
                if (windowsAppSDKDlls.Length > 0)
                {
                    Debug.WriteLine($"Found {windowsAppSDKDlls.Length} WindowsAppSDK DLLs:");
                    foreach (var dll in windowsAppSDKDlls)
                    {
                        FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(dll);
                        Debug.WriteLine($"  - {Path.GetFileName(dll)}: {versionInfo.FileVersion}");
                    }
                }
                else
                {
                    Debug.WriteLine("WARNING: No WindowsAppSDK DLLs found in application directory");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error ensuring Windows App SDK initialization: {ex.Message}");
                // Log but don't throw to allow initialization to continue
                LogException(ex, "WindowsAppSDK_Initialization");
            }
        }

        /// <summary>
        /// Checks for critical Windows UI assemblies before initialization
        /// </summary>
        private void CheckCriticalWindowsAssemblies()
        {
            try
            {
                // Define critical assemblies to check
                var criticalAssemblies = new[] {
                    "Microsoft.UI.Xaml.dll",
                    "Microsoft.WinUI.dll"
                };

                Debug.WriteLine("Checking critical Windows UI assemblies...");

                // Examine loaded assemblies
                var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => criticalAssemblies.Any(ca => a.FullName.Contains(ca.Replace(".dll", ""))))
                    .ToList();

                foreach (var assembly in loadedAssemblies)
                {
                    Debug.WriteLine($"Found loaded assembly: {assembly.FullName}");
                    Debug.WriteLine($"  Location: {(string.IsNullOrEmpty(assembly.Location) ? "Unknown/Dynamic" : assembly.Location)}");
                }

                // Check for DLLs in app directory
                string baseDir = AppContext.BaseDirectory;
                foreach (var dllName in criticalAssemblies)
                {
                    string dllPath = Path.Combine(baseDir, dllName);
                    if (File.Exists(dllPath))
                    {
                        Debug.WriteLine($"Found DLL file: {dllPath}");
                        FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(dllPath);
                        Debug.WriteLine($"  Version: {versionInfo.FileVersion}");
                    }
                    else
                    {
                        Debug.WriteLine($"Missing critical DLL: {dllPath}");
                    }
                }

                // For 80040154 (Class not registered) errors, check COM registration
                try
                {
                    // Check registry entries for Microsoft.UI.Xaml
                    using (RegistryKey key = Registry.ClassesRoot.OpenSubKey("CLSID\\{2D913B79-1558-5B8E-B16A-CB78973BBA47}"))
                    {
                        if (key != null)
                        {
                            Debug.WriteLine("Microsoft.UI.Xaml COM registration found");
                        }
                        else
                        {
                            Debug.WriteLine("WARNING: Microsoft.UI.Xaml COM registration missing");
                        }
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
            }
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
                            // Force loading the assembly to assist with COM registration
                            var assembly = Assembly.LoadFrom(dllPath);
                            Debug.WriteLine($"Successfully loaded {dll}: {assembly.FullName}");
                            anyDllLoaded = true;

                            // Try to register the DLL if running as admin
                            if (IsRunningAsAdministrator())
                            {
                                RegisterDllWithRegsvr32(dllPath);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Failed to load {dll}: {ex.Message}");

                            // Try to register the DLL even if loading failed
                            if (IsRunningAsAdministrator())
                            {
                                RegisterDllWithRegsvr32(dllPath);
                            }
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

                            // Try to register the DLL if running as admin
                            if (IsRunningAsAdministrator())
                            {
                                RegisterDllWithRegsvr32(dll);
                            }
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
                LogException(ex, "WindowsAppSDK_Fix");
            }
        }

        /// <summary>
        /// Attempts to register a DLL using regsvr32
        /// </summary>
        private void RegisterDllWithRegsvr32(string dllPath)
        {
            try
            {
                string regsvr32Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "regsvr32.exe");
                if (File.Exists(regsvr32Path))
                {
                    Debug.WriteLine($"Attempting to register {Path.GetFileName(dllPath)} using regsvr32");
                    ShellExecute(IntPtr.Zero, OPERATION_OPEN, regsvr32Path, $"/s \"{dllPath}\"", null, SW_HIDE);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error registering DLL: {ex.Message}");
            }
        }

        private void Current_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            // Log the UI exception
            LogException(e.Exception, "WinUI_UnhandledException");
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
            try
            {
                // Get the dispatcher queue for the current thread (UI thread)
                var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
                if (dispatcherQueue != null)
                {
                    Debug.WriteLine("UI thread dispatcher queue obtained successfully");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to set up UI thread exception handling: {ex.Message}");
                LogException(ex, "UIThreadExceptionSetup");
            }
        }

        private void RecoverFromInitializationFailure(Exception ex)
        {
            Debug.WriteLine("Attempting to recover from initialization failure");

            try
            {
                // Log detailed information about the failure
                LogException(ex, "InitializationFailure_Recovery");

                // We won't throw here - we'll try to let the app continue
                // The base MAUI initialization might still succeed
            }
            catch (Exception recoveryEx)
            {
                Debug.WriteLine($"Recovery attempt failed: {recoveryEx.Message}");
                LogException(recoveryEx, "RecoveryFailure");
                // At this point we have to let the app crash naturally
                throw ex;
            }
        }

        // Use Application.Current.Exit event instead of OnLaunched
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            try
            {
                Debug.WriteLine("OnLaunched: Starting initialization");

                // Set up UI thread exception handling before calling base implementation
                SetupUIThreadExceptionHandling();

                // Call base implementation - this is critical
                try
                {
                    base.OnLaunched(args);
                    Debug.WriteLine("OnLaunched: Base implementation completed successfully");
                }
                catch (Exception baseEx)
                {
                    Debug.WriteLine($"Error in base.OnLaunched: {baseEx.Message}");
                    LogException(baseEx, "Base_OnLaunched");

                    // Try to fix Windows App SDK registration
                    TryToFixWindowsAppSDKRegistration();

                    // Rethrow to allow the app to handle the error
                    throw;
                }

                // Add specific UI exception handler for WinUI
                if (Microsoft.UI.Xaml.Application.Current != null)
                {
                    Microsoft.UI.Xaml.Application.Current.UnhandledException += Current_UnhandledException;
                    Debug.WriteLine("Registered UnhandledException handler for UI exceptions");
                }
                else
                {
                    Debug.WriteLine("WARNING: Microsoft.UI.Xaml.Application.Current is null, cannot register exception handler");
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
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"Error during window close: {ex.Message}");
                                    LogException(ex, "WindowClosed");
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
                    LogException(windowEx, "WindowSetup");
                }

                Debug.WriteLine("OnLaunched: Initialization completed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Critical error in OnLaunched: {ex.Message}");
                LogException(ex, "OnLaunched_Critical");

                // Try to recover from the error
                try
                {
                    RecoverFromInitializationFailure(ex);
                }
                catch
                {
                    // If recovery fails, we'll let the app crash naturally
                }
            }
        }

        private void UpdateUserStatusToOffline()
        {
            try
            {
                // Get the current user
                var currentUser = TDFMAUI.App.CurrentUser;
                if (currentUser != null)
                {
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
                                    Debug.WriteLine("Setting user status to Offline on Windows app exit");
                                    await userPresenceService.UpdateStatusAsync(UserPresenceStatus.Offline, "");
                                    // Give it a moment to complete the request
                                    await Task.Delay(500);
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"Error updating user status to Offline: {ex.Message}");
                                }
                            }).Wait(1000); // Wait up to 1 second for the status update to complete
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in UpdateUserStatusToOffline: {ex.Message}");
            }
        }

        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            LogException(e.Exception, "TaskScheduler_UnobservedTaskException");
            e.SetObserved(); // Mark as observed to prevent process termination if possible
        }

        private void CurrentDomain_UnhandledException(object sender, System.UnhandledExceptionEventArgs e)
        {
            LogException(e.ExceptionObject as Exception, "CurrentDomain_UnhandledException");
            // Process may still terminate depending on e.IsTerminating
        }

        protected override MauiApp CreateMauiApp()
        {
            try
            {
                return MauiProgram.CreateMauiApp();
            }
            catch (Exception ex)
            {
                LogException(ex, "CreateMauiApp");
                // Optionally rethrow or handle as critical failure
                throw;
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

        /// <summary>
        /// Checks if the application is running with administrator privileges
        /// </summary>
        private bool IsRunningAsAdministrator()
        {
            try
            {
                using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking admin privileges: {ex.Message}");
                return false;
            }
        }
    }
}
