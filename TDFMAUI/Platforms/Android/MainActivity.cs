using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Content;
using System.IO;
using System;
using Microsoft.Maui.Controls;
using System.Text;
using JavaThread = Java.Lang.Thread;
using SystemThread = System.Threading.Thread;
using AndroidX.Core.App;
using Plugin.LocalNotification;

namespace TDFMAUI
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, Exported = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            try
            {
                // Set up global exception handler for the UI thread
                JavaThread.DefaultUncaughtExceptionHandler = new CustomAndroidExceptionHandler(this);

                // Log startup
                LogToFile("MainActivity", "Starting OnCreate");

                // Continue with normal initialization
                base.OnCreate(savedInstanceState);

                // Initialize LocalNotification
                CreateNotificationChannel();

                LogToFile("MainActivity", "OnCreate completed successfully");
            }
            catch (Exception ex)
            {
                // Log the exception
                LogCrash("MainActivity.OnCreate", ex);

                // Try to show a simple error activity
                try
                {
                    var intent = new Intent(this, typeof(ErrorActivity));
                    intent.PutExtra("error_message", ex.Message);
                    intent.PutExtra("error_stack", ex.StackTrace);
                    StartActivity(intent);
                    Finish();
                }
                catch
                {
                    // If we can't even start the error activity, just crash
                    throw;
                }
            }
        }

        private void CreateNotificationChannel()
        {
            if (Build.VERSION.SdkInt < BuildVersionCodes.O)
                return;

            var channelName = "General Notifications";
            var channelDescription = "General notifications for TDF app";
            var channel = new NotificationChannel(
                "default_channel",
                channelName,
                NotificationImportance.Default)
            {
                Description = channelDescription
            };
            var notificationManager = (NotificationManager)GetSystemService(NotificationService);
            notificationManager.CreateNotificationChannel(channel);
        }

        public static void LogToFile(string tag, string message)
        {
            try
            {
                var logsDir = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal), "TDFLogs");
                Directory.CreateDirectory(logsDir);

                var logFile = Path.Combine(logsDir, "app_log.txt");
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var logMessage = $"{timestamp} [{tag}] {message}{System.Environment.NewLine}";

                File.AppendAllText(logFile, logMessage);
            }
            catch
            {
                // Ignore errors from logging
            }
        }

        public static void LogCrash(string source, Exception ex)
        {
            try
            {
                var logsDir = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal), "TDFLogs");
                Directory.CreateDirectory(logsDir);

                var crashFile = Path.Combine(logsDir, $"crash_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                var sb = new StringBuilder();
                sb.AppendLine($"Time: {DateTime.Now}");
                sb.AppendLine($"Source: {source}");
                sb.AppendLine($"Exception: {ex.GetType().Name}");
                sb.AppendLine($"Message: {ex.Message}");
                sb.AppendLine($"Stack Trace: {ex.StackTrace}");

                if (ex.InnerException != null)
                {
                    sb.AppendLine($"Inner Exception: {ex.InnerException.GetType().Name}");
                    sb.AppendLine($"Inner Message: {ex.InnerException.Message}");
                    sb.AppendLine($"Inner Stack Trace: {ex.InnerException.StackTrace}");
                }

                File.WriteAllText(crashFile, sb.ToString());
            }
            catch
            {
                // Ignore errors from logging
            }
        }
    }

    public class CustomAndroidExceptionHandler : Java.Lang.Object, JavaThread.IUncaughtExceptionHandler
    {
        private readonly Context _context;
        private readonly JavaThread.IUncaughtExceptionHandler _defaultHandler;

        public CustomAndroidExceptionHandler(Context context)
        {
            _context = context;
            _defaultHandler = JavaThread.DefaultUncaughtExceptionHandler;
        }

        public void UncaughtException(JavaThread thread, Java.Lang.Throwable ex)
        {
            try
            {
                // Convert Java exception to .NET exception for logging
                var netException = new Exception($"Uncaught Android exception: {ex.Message}",
                    new Exception(ex.ToString()));

                // Log the crash
                MainActivity.LogCrash("UncaughtException", netException);

                // Try to show error activity
                try
                {
                    var intent = new Intent(_context, typeof(ErrorActivity));
                    intent.AddFlags(ActivityFlags.NewTask);
                    intent.PutExtra("error_message", ex.Message);
                    intent.PutExtra("error_stack", ex.ToString());
                    _context.StartActivity(intent);
                }
                catch
                {
                    // If we can't start the error activity, use the default handler
                    _defaultHandler?.UncaughtException(thread, ex);
                }
            }
            catch
            {
                // Last resort, use the default handler
                _defaultHandler?.UncaughtException(thread, ex);
            }
        }
    }
}
