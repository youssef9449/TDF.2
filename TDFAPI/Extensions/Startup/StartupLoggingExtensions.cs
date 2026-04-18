using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;

namespace TDFAPI.Extensions.Startup
{
    /// <summary>
    /// Bootstrap helpers that configure host-wide logging and install the
    /// global unhandled-exception / unobserved-task-exception handlers used to
    /// capture crashes to disk.
    /// </summary>
    public static class StartupLoggingExtensions
    {
        /// <summary>
        /// Creates the startup logger used before <see cref="WebApplication.Logger"/>
        /// is available, and replaces the default logging providers on the
        /// supplied <see cref="WebApplicationBuilder"/> with a single-line
        /// console formatter.
        /// </summary>
        public static ILogger ConfigureConsoleLogging(this WebApplicationBuilder builder)
        {
            var loggerFactory = LoggerFactory.Create(config =>
            {
                config.ClearProviders();
                config.AddSimpleConsole(options =>
                {
                    options.SingleLine = true;
                    options.TimestampFormat = "HH:mm:ss ";
                    options.UseUtcTimestamp = false;
                    options.IncludeScopes = false;
                    options.ColorBehavior = Microsoft.Extensions.Logging.Console.LoggerColorBehavior.Enabled;
                });
            });
            var logger = loggerFactory.CreateLogger("TDF");

            builder.Logging.ClearProviders();
            builder.Logging.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = "HH:mm:ss ";
                options.UseUtcTimestamp = false;
                options.IncludeScopes = false;
                options.ColorBehavior = Microsoft.Extensions.Logging.Console.LoggerColorBehavior.Enabled;
            });
            builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
            builder.Logging.AddFilter("Microsoft.AspNetCore.Mvc", LogLevel.Warning);
            builder.Logging.AddFilter("Microsoft.AspNetCore.Routing", LogLevel.Warning);
            builder.Logging.AddFilter("Microsoft.AspNetCore.StaticFiles", LogLevel.Warning);
            builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Warning);

            return logger;
        }

        /// <summary>
        /// Installs process-wide handlers that persist unhandled exceptions
        /// and unobserved task exceptions to <c>logs/crash_*.txt</c> /
        /// <c>logs/task_exception_*.txt</c>. The handlers rotate to keep the
        /// most recent 10 files of each kind.
        /// </summary>
        public static void RegisterGlobalExceptionHandlers(ILogger logger)
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                var sanitizedMessage = (ex?.Message ?? "Unknown error").Replace("{", "{{").Replace("}", "}}");
                logger.LogCritical(
                    ex,
                    "UNHANDLED EXCEPTION CAUSING APPLICATION CRASH: {Message}. IsTerminating: {IsTerminating}",
                    sanitizedMessage,
                    args.IsTerminating);

                WriteCrashFile(logger, "crash_", ex);
            };

            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                var exception = args.Exception;
                var sanitizedMessage = exception.Message.Replace("{", "{{").Replace("}", "}}");
                logger.LogError(exception, "UNOBSERVED TASK EXCEPTION: {Message}", sanitizedMessage);

                WriteCrashFile(logger, "task_exception_", exception);

                // Mark as observed to prevent application crash
                args.SetObserved();
            };
        }

        /// <summary>
        /// Ensures the <c>logs/</c> directory exists next to the application,
        /// falling back to the per-machine CommonApplicationData folder if
        /// the primary location is not writable.
        /// </summary>
        public static void EnsureLogsDirectory(ILogger logger)
        {
            try
            {
                var logsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                logger.LogInformation("Creating logs directory at: {LogsPath}", logsPath);

                if (!Directory.Exists(logsPath))
                {
                    Directory.CreateDirectory(logsPath);
                    logger.LogInformation("Successfully created logs directory at: {LogsPath}", logsPath);
                }
                else
                {
                    logger.LogInformation("Logs directory already exists at: {LogsPath}", logsPath);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create logs directory: {ErrorMessage}", ex.Message);
                try
                {
                    var altLogsPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                        "TDFAPI", "logs");
                    logger.LogInformation("Attempting to create logs in alternative location: {LogsPath}", altLogsPath);
                    Directory.CreateDirectory(altLogsPath);
                    logger.LogInformation("Successfully created alternative logs directory at: {LogsPath}", altLogsPath);
                }
                catch (Exception ex2)
                {
                    logger.LogError(ex2, "Failed to create alternative logs directory: {ErrorMessage}", ex2.Message);
                }
            }
        }

        private static void WriteCrashFile(ILogger logger, string filePrefix, Exception? exception)
        {
            try
            {
                var logsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                if (!Directory.Exists(logsPath))
                {
                    Directory.CreateDirectory(logsPath);
                }

                // Rotate: keep only the most recent 10 files of this kind.
                var existing = Directory.GetFiles(logsPath, $"{filePrefix}*.txt")
                    .OrderByDescending(f => f)
                    .Skip(9)
                    .ToList();

                foreach (var oldLog in existing)
                {
                    try { File.Delete(oldLog); } catch { /* Best effort deletion */ }
                }

                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var crashLogPath = Path.Combine(logsPath, $"{filePrefix}{timestamp}.txt");

                var crashDetails =
                    $"Exception captured: {DateTime.Now}\n\n" +
                    $"Exception: {exception?.GetType().FullName}\n" +
                    $"Message: {exception?.Message}\n\n" +
                    $"Stack Trace:\n{exception?.StackTrace}\n\n";

                if (exception?.InnerException != null)
                {
                    crashDetails +=
                        $"Inner Exception: {exception.InnerException.GetType().FullName}\n" +
                        $"Inner Message: {exception.InnerException.Message}\n\n";
                }

                if (crashDetails.Length > 500 * 1024)
                {
                    crashDetails = crashDetails.Substring(0, 500 * 1024) + "\n...[truncated]";
                }

                File.WriteAllText(crashLogPath, crashDetails);
            }
            catch
            {
                // Best effort; nothing useful we can do if writing the file fails too.
            }
        }
    }
}
