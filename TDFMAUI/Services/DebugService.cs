using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;
using TDFMAUI.Helpers;

namespace TDFMAUI.Services
{
    public static class DebugService
    {
        private static readonly List<LogEntry> _logBuffer = new List<LogEntry>();
        private static readonly int _maxBufferSize = 100;
        private static bool _isInitialized = false;
        private static readonly Dictionary<string, Stopwatch> _timers = new Dictionary<string, Stopwatch>();
        
        public static void Initialize()
        {
            if (_isInitialized) return;
            
            // Set up global unhandled exception handlers
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                LogError("UNHANDLED EXCEPTION", args.ExceptionObject as Exception);
            };
            
            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                LogError("UNOBSERVED TASK EXCEPTION", args.Exception);
                args.SetObserved(); // Prevent the application from crashing
            };
            
            _isInitialized = true;
            LogInfo("DebugService", "Initialized debugging service");
        }
        
        public static void LogInfo(string tag, string message)
        {
            Log(LogLevel.Info, tag, message);
        }
        
        public static void LogWarning(string tag, string message)
        {
            Log(LogLevel.Warning, tag, message);
        }
        
        public static void LogError(string tag, string message)
        {
            Log(LogLevel.Error, tag, message);
        }
        
        public static void LogError(string tag, Exception ex)
        {
            var message = new StringBuilder();
            message.AppendLine(ex.Message);
            message.AppendLine(ex.StackTrace);
            
            if (ex.InnerException != null)
            {
                message.AppendLine("--- Inner Exception ---");
                message.AppendLine(ex.InnerException.Message);
                message.AppendLine(ex.InnerException.StackTrace);
            }
            
            Log(LogLevel.Error, tag, message.ToString());
        }
        
        private static void Log(LogLevel level, string tag, string message)
        {
            var entry = new LogEntry
            {
                Level = level,
                Tag = tag,
                Message = message,
                Timestamp = DateTime.Now
            };
            
            // Add to buffer with overflow protection
            lock (_logBuffer)
            {
                _logBuffer.Add(entry);
                if (_logBuffer.Count > _maxBufferSize)
                {
                    _logBuffer.RemoveAt(0);
                }
            }
            
            // Format for console output
            var logMessage = $"[{entry.Timestamp:HH:mm:ss.fff}] [{entry.Level}] [{entry.Tag}] {entry.Message}";
            
            // Write to debug console (only once)
            System.Diagnostics.Debug.WriteLine(logMessage);
        }
        
        public static List<LogEntry> GetLogEntries()
        {
            lock (_logBuffer)
            {
                return new List<LogEntry>(_logBuffer);
            }
        }
        
        public static string GetFormattedLogs()
        {
            var sb = new StringBuilder();
            
            lock (_logBuffer)
            {
                foreach (var entry in _logBuffer)
                {
                    sb.AppendLine($"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{entry.Level}] [{entry.Tag}] {entry.Message}");
                }
            }
            
            return sb.ToString();
        }
        
        public static async Task<bool> SaveLogsToFile()
        {
            try
            {
                // First try to save to public Downloads folder (accessible to user)
                string logFileName = $"TDF_log_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                string logContent = GetFormattedLogs();
                
                // First attempt: Use public storage for user access
                try 
                {
                    // Get public Downloads directory (Android 10+ compatible)
                    var publicDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

#if ANDROID

                        // On Android, use the Download directory if possible
                        publicDir = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads).AbsolutePath;
#elif IOS || MACCATALYST
                        // On iOS/MacCatalyst, use the Documents directory
                        publicDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
#endif
                    
                    var publicLogFile = Path.Combine(publicDir, logFileName);
                    File.WriteAllText(publicLogFile, logContent);
                    Debug.WriteLine($"Logs saved to public location: {publicLogFile}");
                    return true;
                }
                catch (Exception publicEx)
                {
                    // Fallback to app's private directory if public storage fails
                    Debug.WriteLine($"Failed to save to public storage: {publicEx.Message}, falling back to private storage");
                    
                    var logsDir = Path.Combine(FileSystem.AppDataDirectory, "Logs");
                    Directory.CreateDirectory(logsDir);
                    
                    var logFile = Path.Combine(logsDir, logFileName);
                    File.WriteAllText(logFile, logContent);
                    
                    // Try to share the file since we couldn't save to public location
                    await ShareLogFile(logFile);
                    
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving logs: {ex.Message}");
                return false;
            }
        }
        
        private static async Task ShareLogFile(string logFilePath)
        {
            try
            {
                // Share the log file with other apps
                await Share.RequestAsync(new ShareFileRequest
                {
                    Title = "Share TDF Log File",
                    File = new ShareFile(logFilePath)
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error sharing log file: {ex.Message}");
            }
        }
        
        // Method to start tracking performance of an operation
        public static void StartTimer(string operationName)
        {
            if (string.IsNullOrEmpty(operationName))
                return;
            
            lock (_timers)
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();
                _timers[operationName] = stopwatch;
                LogInfo("Perf", $"Started timer for '{operationName}'");
            }
        }
        
        // Method to stop tracking performance and get elapsed time
        public static TimeSpan StopTimer(string operationName, bool logResult = true)
        {
            if (string.IsNullOrEmpty(operationName))
                return TimeSpan.Zero;
            
            lock (_timers)
            {
                if (_timers.TryGetValue(operationName, out var stopwatch))
                {
                    stopwatch.Stop();
                    var elapsed = stopwatch.Elapsed;
                    _timers.Remove(operationName);
                    
                    if (logResult)
                    {
                        LogInfo("Perf", $"'{operationName}' completed in {elapsed.TotalMilliseconds:0.00}ms");
                    }
                    
                    return elapsed;
                }
                
                return TimeSpan.Zero;
            }
        }
        
        // Convenience method to track a function's execution time with safe exception handling
        public static async Task<T> TrackOperationAsync<T>(string operationName, Func<Task<T>> operation)
        {
            StartTimer(operationName);
            try
            {
                return await operation();
            }
            catch (Exception)
            {
                // Make sure to stop the timer even if an exception occurs
                StopTimer(operationName);
                throw;
            }
            finally
            {
                // This is redundant if the timer was already stopped in the catch block
                // But it ensures the timer is always stopped
                if (_timers.ContainsKey(operationName))
                {
                    StopTimer(operationName);
                }
            }
        }
        
        // Overload for non-generic async operations with safe exception handling
        public static async Task TrackOperationAsync(string operationName, Func<Task> operation)
        {
            StartTimer(operationName);
            try
            {
                await operation();
            }
            catch (Exception)
            {
                // Make sure to stop the timer even if an exception occurs
                StopTimer(operationName);
                throw;
            }
            finally
            {
                // This is redundant if the timer was already stopped in the catch block
                // But it ensures the timer is always stopped
                if (_timers.ContainsKey(operationName))
                {
                    StopTimer(operationName);
                }
            }
        }
    }
    
    public enum LogLevel
    {
        Info,
        Warning,
        Error
    }
    
    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public LogLevel Level { get; set; }
        public string Tag { get; set; }
        public string Message { get; set; }
    }
} 