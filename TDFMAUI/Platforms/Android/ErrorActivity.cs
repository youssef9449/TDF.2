using Android.App;
using Android.Content;
using Android.OS;
using Android.Widget;
using System.IO;
using System;
using Android.Graphics;
using Android.Views;
using Android.Util;
using Color = Android.Graphics.Color;
using View = Android.Views.View;
using ScrollView = Android.Widget.ScrollView;
using Button = Android.Widget.Button;

namespace TDFMAUI
{
    [Activity(Theme = "@android:style/Theme.DeviceDefault", ConfigurationChanges = Android.Content.PM.ConfigChanges.Orientation | Android.Content.PM.ConfigChanges.ScreenSize)]
    public class ErrorActivity : Activity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            try
            {
                // Log that we're showing the error screen
                MainActivity.LogToFile("ErrorActivity", "Showing error screen");

                // Create a linear layout
                var layout = new LinearLayout(this)
                {
                    Orientation = Orientation.Vertical,
                    LayoutParameters = new LinearLayout.LayoutParams(
                        LinearLayout.LayoutParams.MatchParent,
                        LinearLayout.LayoutParams.MatchParent)
                };
                layout.SetBackgroundColor(Color.Red);
                layout.SetPadding(20, 20, 20, 20);

                // Add a title
                var titleText = new TextView(this)
                {
                    Text = "Application Error",
                    TextSize = 24,
                    Gravity = Android.Views.GravityFlags.Center
                };
                titleText.SetTextColor(Color.White);
                layout.AddView(titleText);

                // Add a separator
                var separator = new View(this)
                {
                    LayoutParameters = new LinearLayout.LayoutParams(
                        LinearLayout.LayoutParams.MatchParent,
                        2)
                };
                separator.SetBackgroundColor(Color.White);
                layout.AddView(separator);

                // Get error details from intent
                string errorMessage = Intent.GetStringExtra("error_message") ?? "Unknown error";
                string errorStack = Intent.GetStringExtra("error_stack") ?? "No stack trace available";

                // Add error message
                var messageText = new TextView(this)
                {
                    Text = $"Error: {errorMessage}",
                    TextSize = 16,
                    Gravity = Android.Views.GravityFlags.Left
                };
                messageText.SetTextColor(Color.White);
                layout.AddView(messageText);

                // Add stack trace in a scrollable view
                var scrollView = new ScrollView(this)
                {
                    LayoutParameters = new LinearLayout.LayoutParams(
                        LinearLayout.LayoutParams.MatchParent,
                        LinearLayout.LayoutParams.WrapContent,
                        1.0f)
                };

                var stackText = new TextView(this)
                {
                    Text = errorStack,
                    TextSize = 12
                };
                var textSecondaryColor = Microsoft.Maui.Graphics.Color.FromArgb("#90A4AE"); // Default fallback
            try
            {
                if (Microsoft.Maui.Controls.Application.Current?.Resources?.ContainsKey("TextSecondaryColor") == true)
                {
                    textSecondaryColor = Microsoft.Maui.Controls.Application.Current.Resources["TextSecondaryColor"] as Microsoft.Maui.Graphics.Color ?? textSecondaryColor;
                }
            }
            catch { /* Use fallback */ }
            stackText.SetTextColor(textSecondaryColor);
                scrollView.AddView(stackText);
                layout.AddView(scrollView);

                // Add a button to restart the app
                var restartButton = new Button(this)
                {
                    Text = "Restart App",
                    LayoutParameters = new LinearLayout.LayoutParams(
                        LinearLayout.LayoutParams.MatchParent,
                        LinearLayout.LayoutParams.WrapContent)
                };
                restartButton.Click += (sender, e) =>
                {
                    try
                    {
                        // Log restart attempt
                        MainActivity.LogToFile("ErrorActivity", "User requested app restart");

                        // Create an intent to restart the app
                        var currentPackageName = PackageName;
                        if (!string.IsNullOrEmpty(currentPackageName))
                        {
                            var intent = PackageManager.GetLaunchIntentForPackage(currentPackageName);
                            if (intent != null)
                            {
                                intent.AddFlags(ActivityFlags.ClearTop);
                                intent.PutExtra("safe_mode", true); // Add a flag to indicate safe mode
                                StartActivity(intent);
                                Process.KillProcess(Process.MyPid());
                            }
                            else
                            {
                                MainActivity.LogToFile("ErrorActivity", "Failed to get launch intent for restart.");
                            }
                        }
                        else
                        {
                            MainActivity.LogToFile("ErrorActivity", "PackageName was null or empty, cannot restart.");
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log restart failure
                        MainActivity.LogCrash("ErrorActivity.Restart", ex);
                        Toast.MakeText(this, "Failed to restart: " + ex.Message, ToastLength.Long).Show();
                    }
                };
                layout.AddView(restartButton);

                // Add a button to view logs
                var viewLogsButton = new Button(this)
                {
                    Text = "View Logs",
                    LayoutParameters = new LinearLayout.LayoutParams(
                        LinearLayout.LayoutParams.MatchParent,
                        LinearLayout.LayoutParams.WrapContent)
                };
                viewLogsButton.Click += (sender, e) =>
                {
                    try
                    {
                        var logsDir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal), "TDFLogs");
                        var logFile = System.IO.Path.Combine(logsDir, "app_log.txt");

                        if (System.IO.File.Exists(logFile))
                        {
                            var logContent = System.IO.File.ReadAllText(logFile);
                            var logDialog = new AlertDialog.Builder(this);
                            logDialog.SetTitle("Application Logs");
                            logDialog.SetMessage(logContent);
                            logDialog.SetPositiveButton("Close", (s, args) => { });
                            logDialog.Show();
                        }
                        else
                        {
                            Toast.MakeText(this, "No logs found", ToastLength.Short).Show();
                        }
                    }
                    catch (Exception ex)
                    {
                        Toast.MakeText(this, "Failed to view logs: " + ex.Message, ToastLength.Long).Show();
                    }
                };
                layout.AddView(viewLogsButton);

                // Set the content view
                SetContentView(layout);
            }
            catch (Exception ex)
            {
                // If we can't even show the error screen, log it and show a simple toast
                try
                {
                    MainActivity.LogCrash("ErrorActivity.OnCreate", ex);
                    if (this != null)
                    {
                        Toast.MakeText(this, "Critical error: " + ex.Message, ToastLength.Long)?.Show();
                    }
                }
                catch
                {
                    // Nothing more we can do
                }
            }
        }
    }
}
