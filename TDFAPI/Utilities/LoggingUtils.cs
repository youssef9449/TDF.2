using System;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace TDFAPI.Utilities
{
    public static class LoggingUtils
    {
        private const string LOG_FOLDER = "logs";
        
        /// <summary>
        /// Logs database errors to an INI file in the logs directory
        /// </summary>
        public static void LogDatabaseError(Exception ex, string connectionInfo, ILogger logger)
        {
            try
            {
                // Ensure logs directory exists
                var logsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                if (!Directory.Exists(logsPath))
                {
                    Directory.CreateDirectory(logsPath);
                }
                
                // Create INI file name with timestamp
                var fileName = $"db_error_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                var filePath = Path.Combine(logsPath, fileName);
                
                // Build the INI content
                var sb = new StringBuilder();
                sb.AppendLine("[DatabaseError]");
                sb.AppendLine($"Timestamp={DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"Message={ex.Message.Replace("\r\n", " ").Replace("\n", " ")}");
                sb.AppendLine($"Source={ex.Source}");
                
                // Sanitize connection string for logging to remove sensitive data
                var sanitizedConnection = SanitizeConnectionString(connectionInfo);
                sb.AppendLine($"ConnectionInfo={sanitizedConnection}");
                
                // Add inner exception details if available
                if (ex.InnerException != null)
                {
                    sb.AppendLine("[InnerException]");
                    sb.AppendLine($"Message={ex.InnerException.Message.Replace("\r\n", " ").Replace("\n", " ")}");
                    sb.AppendLine($"Source={ex.InnerException.Source}");
                }
                
                // Add stack trace
                sb.AppendLine("[StackTrace]");
                sb.AppendLine(ex.StackTrace?.Replace("\r\n", "\n").Replace("\n", "\\n") ?? "Not available");
                
                // Add environment info
                sb.AppendLine("[Environment]");
                sb.AppendLine($"MachineName={Environment.MachineName}");
                sb.AppendLine($"OSVersion={Environment.OSVersion}");
                sb.AppendLine($"AppDomain={AppDomain.CurrentDomain.FriendlyName}");
                sb.AppendLine($"BaseDirectory={AppDomain.CurrentDomain.BaseDirectory}");
                sb.AppendLine($"AppContextBaseDirectory={AppContext.BaseDirectory}");
                
                // Add config file information
                sb.AppendLine("[ConfigInfo]");
                string appConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");
                string projectConfigPath = Path.Combine(AppContext.BaseDirectory, "config.ini");
                string appSettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                
                sb.AppendLine($"AppConfigPath={appConfigPath}");
                sb.AppendLine($"AppConfigExists={File.Exists(appConfigPath)}");
                
                sb.AppendLine($"ProjectConfigPath={projectConfigPath}");
                sb.AppendLine($"ProjectConfigExists={File.Exists(projectConfigPath)}");
                
                sb.AppendLine($"AppSettingsPath={appSettingsPath}");
                sb.AppendLine($"AppSettingsExists={File.Exists(appSettingsPath)}");
                
                // Check config.ini in application directory
                if (File.Exists(appConfigPath))
                {
                    try
                    {
                        var dbSection = new List<string>();
                        var lines = File.ReadAllLines(appConfigPath);
                        bool inDbSection = false;
                        
                        foreach (var line in lines)
                        {
                            if (line.Trim().Equals("[Database]", StringComparison.OrdinalIgnoreCase))
                            {
                                inDbSection = true;
                                dbSection.Add(line);
                            }
                            else if (inDbSection && line.Trim().StartsWith("["))
                            {
                                inDbSection = false;
                            }
                            else if (inDbSection)
                            {
                                // Sanitize password line if present
                                if (line.Contains("Password="))
                                {
                                    dbSection.Add(line.Substring(0, line.IndexOf("=") + 1) + "******");
                                }
                                else
                                {
                                    dbSection.Add(line);
                                }
                            }
                        }
                        
                        sb.AppendLine($"AppConfig_DatabaseSection={string.Join("\\n", dbSection)}");
                    }
                    catch (Exception ex2)
                    {
                        sb.AppendLine($"AppConfig_ReadError={ex2.Message}");
                    }
                }
                
                // Check config.ini in project directory if different
                if (File.Exists(projectConfigPath) && projectConfigPath != appConfigPath)
                {
                    try
                    {
                        var dbSection = new List<string>();
                        var lines = File.ReadAllLines(projectConfigPath);
                        bool inDbSection = false;
                        
                        foreach (var line in lines)
                        {
                            if (line.Trim().Equals("[Database]", StringComparison.OrdinalIgnoreCase))
                            {
                                inDbSection = true;
                                dbSection.Add(line);
                            }
                            else if (inDbSection && line.Trim().StartsWith("["))
                            {
                                inDbSection = false;
                            }
                            else if (inDbSection)
                            {
                                // Sanitize password line if present
                                if (line.Contains("Password="))
                                {
                                    dbSection.Add(line.Substring(0, line.IndexOf("=") + 1) + "******");
                                }
                                else
                                {
                                    dbSection.Add(line);
                                }
                            }
                        }
                        
                        sb.AppendLine($"ProjectConfig_DatabaseSection={string.Join("\\n", dbSection)}");
                    }
                    catch (Exception ex2)
                    {
                        sb.AppendLine($"ProjectConfig_ReadError={ex2.Message}");
                    }
                }
                
                // Check appsettings.json
                if (File.Exists(appSettingsPath))
                {
                    try
                    {
                        var content = File.ReadAllText(appSettingsPath);
                        // Simple approach to extract connection string - could use JSON parsing for a better solution
                        var connectionMatch = Regex.Match(content, "\"DefaultConnection\"\\s*:\\s*\"([^\"]+)\"");
                        if (connectionMatch.Success)
                        {
                            var connString = connectionMatch.Groups[1].Value;
                            // Sanitize password
                            connString = SanitizeConnectionString(connString);
                            sb.AppendLine($"AppSettings_ConnectionString={connString}");
                        }
                        else
                        {
                            sb.AppendLine("AppSettings_ConnectionString=Not found in file");
                        }
                    }
                    catch (Exception ex2)
                    {
                        sb.AppendLine($"AppSettings_ReadError={ex2.Message}");
                    }
                }
                
                // Add troubleshooting info
                sb.AppendLine("[Troubleshooting]");
                sb.AppendLine("1. Ensure SQL Server is running and accessible");
                sb.AppendLine("2. Check that the 'Users' database exists on your SQL Server");
                sb.AppendLine("3. Verify the ServerIP in config.ini points to your SQL Server instance");
                sb.AppendLine("4. Ensure the account running the application has access to the database");
                sb.AppendLine("5. Try setting TrustServerCertificate=true if there are SSL issues");
                sb.AppendLine("6. If using SQL authentication, verify User_Id and Password are correct");
                sb.AppendLine("7. Make sure config.ini is located in the application's startup directory");
                sb.AppendLine("8. The application prioritizes config.ini over appsettings.json - check both files");
                
                // Write to file
                File.WriteAllText(filePath, sb.ToString());
                
                // Log that we wrote the error to file
                logger.LogInformation("Database error logged to file: {FilePath}", filePath);
            }
            catch (Exception logEx)
            {
                // If we fail to log to file, at least log the error through the logger
                logger.LogError(logEx, "Failed to log database error to file: {ErrorMessage}", logEx.Message);
                logger.LogError(ex, "Original database error: {ErrorMessage}", ex.Message);
            }
        }
        
        /// <summary>
        /// Sanitizes a connection string by removing sensitive information like passwords
        /// </summary>
        private static string SanitizeConnectionString(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                return "Not available";
                
            // Replace password
            return connectionString
                .Replace(";Password=", ";Password=*****")
                .Replace(";Pwd=", ";Pwd=*****")
                .Replace("password=", "password=*****")
                .Replace("pwd=", "pwd=*****");
        }
    }
} 