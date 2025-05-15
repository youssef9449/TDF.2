using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using TDFMAUI.Services;
using TDFShared.Enums;
using TDFMAUI.Helpers;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace TDFMAUI.WinUI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : MauiWinUIApplication
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            // Handle application exit
            this.Exiting += App_Exiting;

            this.InitializeComponent(); // If this throws, AppDomain.CurrentDomain.UnhandledException should catch it.
        }

        private void App_Exiting(object sender, Microsoft.UI.Xaml.ExitEventArgs e)
        {
            // Update user status to Offline when the app is closing
            // Verify we're on a desktop platform using DeviceHelper
            if (DeviceHelper.IsDesktop)
            {
                UpdateUserStatusToOffline();
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
    }
}
