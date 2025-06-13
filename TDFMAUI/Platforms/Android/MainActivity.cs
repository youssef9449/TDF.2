﻿using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Content;
using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;
using System.Text;
using JavaThread = Java.Lang.Thread;
using SystemThread = System.Threading.Thread;
using AndroidX.Core.App;
using Plugin.LocalNotification;
using Plugin.Firebase;
using AndroidX.Core.Content;
using Android.Widget;
using System.Runtime.Versioning;
using System.Reflection;

namespace TDFMAUI
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, Exported = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            try
            {
                // Set up global exception handler for the UI thread
                JavaThread.DefaultUncaughtExceptionHandler = new CustomAndroidExceptionHandler(this);

                // Log startup
                LogToFile("MainActivity", "Starting OnCreate");
                
                // Add console debug logs (will appear in VS Code Debug Console)
                System.Diagnostics.Debug.WriteLine("DEBUG LOG: MainActivity OnCreate starting...");
                Console.WriteLine("CONSOLE LOG: MainActivity OnCreate starting...");

                // Continue with normal initialization
                base.OnCreate(savedInstanceState);
                
                // Initialize Firebase with proper error handling
                 try
                 {
                     var firebaseApp = Firebase.FirebaseApp.InitializeApp(this);
                     if (firebaseApp != null)
                     {
                         LogToFile("MainActivity", "Firebase initialized successfully");
                     }
                     else
                     {
                         LogToFile("MainActivity", "Firebase initialization returned null");
                     }
                 }
                 catch (Exception firebaseEx)
                 {
                     LogToFile("MainActivity", $"Firebase initialization failed: {firebaseEx.Message}");
                     // Continue without Firebase rather than crashing the app
                 }
                
                // Initialize LocalNotification
                CreateNotificationChannel();
                
                // Request all necessary permissions
                RequestAllPermissions();

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
        
        private void RequestAllPermissions()
        {
            try
            {
                LogToFile("MainActivity", "Checking and requesting all necessary permissions");
                
                var permissionsToRequest = new List<string>();
                
                // Check notification permission for Android 13+ (API 33+)
                if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
                {
                    const string PostNotificationsPermission = "android.permission.POST_NOTIFICATIONS";
                    if (ContextCompat.CheckSelfPermission(this, PostNotificationsPermission) != Permission.Granted)
                    {
                        permissionsToRequest.Add(PostNotificationsPermission);
                    }
                }
                
                // Check camera permission
                if (ContextCompat.CheckSelfPermission(this, Android.Manifest.Permission.Camera) != Permission.Granted)
                {
                    permissionsToRequest.Add(Android.Manifest.Permission.Camera);
                }
                
                // Check storage permissions based on Android version
                if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu) // Android 13+
                {
                    if (ContextCompat.CheckSelfPermission(this, Android.Manifest.Permission.ReadMediaImages) != Permission.Granted)
                    {
                        permissionsToRequest.Add(Android.Manifest.Permission.ReadMediaImages);
                    }
                }
                else if (Build.VERSION.SdkInt >= BuildVersionCodes.M) // Android 6+
                {
                    if (ContextCompat.CheckSelfPermission(this, Android.Manifest.Permission.ReadExternalStorage) != Permission.Granted)
                    {
                        permissionsToRequest.Add(Android.Manifest.Permission.ReadExternalStorage);
                    }
                    
                    if (Build.VERSION.SdkInt <= BuildVersionCodes.P && // Only for Android 9 and below
                        ContextCompat.CheckSelfPermission(this, Android.Manifest.Permission.WriteExternalStorage) != Permission.Granted)
                    {
                        permissionsToRequest.Add(Android.Manifest.Permission.WriteExternalStorage);
                    }
                }
                
                // Check location permissions
                if (ContextCompat.CheckSelfPermission(this, Android.Manifest.Permission.AccessFineLocation) != Permission.Granted)
                {
                    permissionsToRequest.Add(Android.Manifest.Permission.AccessFineLocation);
                }
                
                if (ContextCompat.CheckSelfPermission(this, Android.Manifest.Permission.AccessCoarseLocation) != Permission.Granted)
                {
                    permissionsToRequest.Add(Android.Manifest.Permission.AccessCoarseLocation);
                }
                
                // Request permissions if any are needed
                if (permissionsToRequest.Count > 0)
                {
                    LogToFile("MainActivity", $"Requesting {permissionsToRequest.Count} permissions: {string.Join(", ", permissionsToRequest)}");
                    
                    // Instead of showing all permission rationales at once,
                    // we'll request permissions one by one to prevent freezing
                    if (permissionsToRequest.Count > 0)
                    {
                        // Request the first permission in the list
                        string firstPermission = permissionsToRequest[0];
                        LogToFile("MainActivity", $"Requesting permission: {firstPermission}");
                        
                        bool shouldShowRationale = ActivityCompat.ShouldShowRequestPermissionRationale(this, firstPermission);
                        
                        if (shouldShowRationale)
                        {
                            // Show rationale for this specific permission
                            AlertDialog.Builder builder = new AlertDialog.Builder(this);
                            builder.SetTitle("Permission Required");
                            
                            string message = "This permission is needed for app functionality:";
                            if (firstPermission.Contains("NOTIFICATION")) 
                                message += "\n\n• Notifications: To alert you about request updates";
                            else if (firstPermission.Contains("CAMERA"))
                                message += "\n\n• Camera: To capture photos for requests";
                            else if (firstPermission.Contains("STORAGE") || firstPermission.Contains("MEDIA"))
                                message += "\n\n• Storage: To save and access files";
                            else if (firstPermission.Contains("LOCATION"))
                                message += "\n\n• Location: For location-based features";
                            
                            builder.SetMessage(message);
                            builder.SetPositiveButton("Grant Permission", (sender, args) => {
                                ActivityCompat.RequestPermissions(this, new[] { firstPermission }, 100);
                            });
                            builder.SetNegativeButton("Cancel", (sender, args) => {
                                LogToFile("MainActivity", $"User declined permission after rationale: {firstPermission}");
                            });
                            builder.Show();
                        }
                        else
                        {
                            // Request just this one permission
                            ActivityCompat.RequestPermissions(this, new[] { firstPermission }, 100);
                        }
                    }
                }
                else
                {
                    LogToFile("MainActivity", "All required permissions already granted");
                }
            }
            catch (Exception ex)
            {
                LogToFile("MainActivity", $"Error checking permissions: {ex.Message}");
            }
        }
        
        [SupportedOSPlatform("android23.0")]
        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
        {
            try
            {
                // IMPORTANT: Call Platform.OnRequestPermissionsResult instead of base implementation
                // This properly informs the MAUI permission system about the results
                Microsoft.Maui.ApplicationModel.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);
                
                if (requestCode == 100) // The request code we used for all permissions
                {
                    var grantedPermissions = new List<string>();
                    var deniedPermissions = new List<string>();
                    
                    for (int i = 0; i < permissions.Length && i < grantResults.Length; i++)
                    {
                        if (grantResults[i] == Permission.Granted)
                        {
                            grantedPermissions.Add(permissions[i]);
                        }
                        else
                        {
                            deniedPermissions.Add(permissions[i]);
                        }
                    }
                    
                    if (grantedPermissions.Count > 0)
                    {
                        LogToFile("MainActivity", $"Permissions granted: {string.Join(", ", grantedPermissions)}");
                    }
                    
                    if (deniedPermissions.Count > 0)
                    {
                        LogToFile("MainActivity", $"Permissions denied: {string.Join(", ", deniedPermissions)}");
                        
                        // Handle denied permissions by showing a Toast AFTER a slight delay
                        // This prevents UI freezing when interacting with permission dialogs
                        var messages = new List<string>();
                        
                        if (deniedPermissions.Any(p => p.Contains("NOTIFICATION")))
                        {
                            messages.Add("• Notifications: You won't receive alerts about request updates");
                        }
                        
                        if (deniedPermissions.Any(p => p.Contains("CAMERA")))
                        {
                            messages.Add("• Camera: You won't be able to take photos for requests");
                        }
                        
                        if (deniedPermissions.Any(p => p.Contains("STORAGE") || p.Contains("MEDIA")))
                        {
                            messages.Add("• Storage: You won't be able to save or access files");
                        }
                        
                        if (deniedPermissions.Any(p => p.Contains("LOCATION")))
                        {
                            messages.Add("• Location: Location-based features won't work");
                        }
                        
                        if (messages.Count > 0)
                        {
                            // Use a handler with a short delay to prevent UI freeze
                            new Android.OS.Handler(Android.OS.Looper.MainLooper).PostDelayed(() => {
                                string message = "Some permissions were denied. This may affect app functionality:\n\n" + string.Join("\n", messages);
                                Toast.MakeText(this, message, ToastLength.Long)?.Show();
                            }, 500); // 500ms delay
                        }
                    }
                    
                    // Log final permission state
                    LogToFile("MainActivity", $"Permission request completed. Granted: {grantedPermissions.Count}, Denied: {deniedPermissions.Count}");
                }
            }
            catch (Exception ex)
            {
                LogToFile("MainActivity", $"Error in OnRequestPermissionsResult: {ex.Message}");
            }
        }
        
        // This method will be called for API < 23 to maintain compatibility
        private void HandlePermissionResult(int requestCode, bool granted)
        {
            // Handle permission results for older Android versions
            if (requestCode == 100)
            {
                if (granted)
                {
                    LogToFile("MainActivity", "Permission granted (legacy)");
                }
                else
                {
                    LogToFile("MainActivity", "Permission denied (legacy)");
                    Toast.MakeText(this, "Notifications disabled. Some features may not work properly.", ToastLength.Long)?.Show();
                }
            }
        }

        private void CreateNotificationChannel()
        {
            try
            {
                // Notification channels are only needed on Android Oreo (API 26) and above
                if (Build.VERSION.SdkInt < BuildVersionCodes.O)
                {
                    LogToFile("MainActivity", "Android version < O (API 26), notification channels not needed");
                    SetupLegacyNotifications();
                    return;
                }

                var notificationManager = GetSystemService(NotificationService) as NotificationManager;
                if (notificationManager == null)
                {
                    LogToFile("MainActivity", "Failed to get NotificationManager");
                    return;
                }

                // Create notification channels for Oreo and above
                CreateNotificationChannelsForOreo(notificationManager);
                LogToFile("MainActivity", "Notification channels created successfully");
            }
            catch (Exception ex)
            {
                LogToFile("MainActivity", $"Error creating notification channels: {ex.Message}");
            }
        }
        
        // This method is only called on Android Oreo (API 26) and above
        [SupportedOSPlatform("android26.0")]
        private void CreateNotificationChannelsForOreo(NotificationManager notificationManager)
        {
            // Create default channel for general notifications
            var channelName = "General Notifications";
            var channelDescription = "General notifications for TDF app";
            
            var channel = new NotificationChannel(
                "default_channel", 
                channelName,
                NotificationImportance.Default);
            
            channel.Description = channelDescription;
            channel.EnableLights(true);
            channel.EnableVibration(true);
            notificationManager.CreateNotificationChannel(channel);
            
            // Create high priority channel for important notifications
            var highPriorityChannel = new NotificationChannel(
                "high_priority_channel",
                "Important Notifications",
                NotificationImportance.High);
           
             highPriorityChannel.Description = "High priority notifications that require immediate attention";
            highPriorityChannel.EnableLights(true);
            highPriorityChannel.EnableVibration(true);
            notificationManager.CreateNotificationChannel(highPriorityChannel);
           
             LogToFile("MainActivity", "Notification channels created successfully");
        }
        
        private void SetupLegacyNotifications()
        {
            // Add any legacy notification setup here (pre-Android O)
            LogToFile("MainActivity", "Setting up legacy notification support");
            
            // For older Android versions, no specific setup is needed
            // as notifications will use the app's default settings
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
        private readonly JavaThread.IUncaughtExceptionHandler? _defaultHandler;

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
