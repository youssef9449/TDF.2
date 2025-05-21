using System;
using System.Collections.Generic;
using System.IO;
using TDFAPI.Utilities;

namespace TDFAPI.Configuration
{
    public class IniConfiguration
    {
        private static IniFile _iniFile = null!;
        private static string _connectionString = string.Empty;
        private static string _jwtSecretKey = string.Empty;
        private static int _tokenValidityInMinutes;
        private static int _refreshTokenValidityInDays;
        private static string _jwtIssuer = string.Empty;
        private static string _jwtAudience = string.Empty;
        private static List<string> _allowedOrigins = new();
        private static List<string> _developmentAllowedOrigins = new();
        private static Dictionary<string, object> _securitySettings = new();
        private static Dictionary<string, object> _webSocketSettings = new();
        private static Dictionary<string, object> _rateLimitSettings = new();
        private static bool _isInitialized = false;

        private const string ConfigFileName = "config.ini";

        public static void Initialize()
        {
            if (_isInitialized)
                return;

            try
            {
                // Get the actual application's execution directory
                string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string projectDirectory = AppContext.BaseDirectory;
                string iniFilePath = Path.Combine(appDirectory, ConfigFileName);

                Console.WriteLine($"Looking for configuration at: {iniFilePath}");

                // If config file doesn't exist in the execution directory but exists in the project directory,
                // copy it to the execution directory
                if (!File.Exists(iniFilePath))
                {
                    string projectConfigPath = Path.Combine(projectDirectory, ConfigFileName);
                    if (File.Exists(projectConfigPath))
                    {
                        try
                        {
                            Console.WriteLine($"Copying config file from {projectConfigPath} to {iniFilePath}");
                            File.Copy(projectConfigPath, iniFilePath);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to copy config file: {ex.Message}");
                        }
                    }

                    // If the file still doesn't exist after trying to copy, create a new one
                    if (!File.Exists(iniFilePath))
                    {
                        Console.WriteLine($"Config file not found. Will create a new one at: {iniFilePath}");

                        try
                        {
                            // Ensure the directory exists
                            Directory.CreateDirectory(Path.GetDirectoryName(iniFilePath));

                            // Create default configuration with server URLs
                            _iniFile = new IniFile(iniFilePath);
                            _iniFile.Write("Server", "Urls", "http://localhost:5000,https://localhost:5001");
                            _iniFile.Write("Jwt", "SecretKey", Guid.NewGuid().ToString());
                            _iniFile.Write("Jwt", "Issuer", "TDFAPI");
                            _iniFile.Write("Jwt", "Audience", "TDFClient");
                            _iniFile.Write("App", "AllowedOrigins", "");
                            _iniFile.Write("App", "DevelopmentAllowedOrigins", "http://localhost:3000,http://localhost:8080");
                            _iniFile.Write("Security", "MaxFailedLoginAttempts", "5");
                            _iniFile.Write("Security", "LockoutDurationMinutes", "15");
                            _iniFile.Write("WebSockets", "TimeoutMinutes", "30");
                            _iniFile.Write("WebSockets", "KeepAliveMinutes", "2");
                            _iniFile.Write("RateLimiting", "GlobalLimitPerMinute", "100");

                            Console.WriteLine("Created new configuration file with default values");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to create config file: {ex.Message}");
                            Console.WriteLine("Will use default values for configuration");

                            // Create an in-memory IniFile with default values
                            _iniFile = new IniFile(":memory:");
                            _iniFile.Write("Server", "Urls", "http://localhost:5000,https://localhost:5001");
                            _iniFile.Write("Jwt", "SecretKey", Guid.NewGuid().ToString());
                            _iniFile.Write("Jwt", "Issuer", "TDFAPI");
                            _iniFile.Write("Jwt", "Audience", "TDFClient");
                            _iniFile.Write("Security", "MaxFailedLoginAttempts", "5");
                            _iniFile.Write("Security", "LockoutDurationMinutes", "15");
                        }
                    }
                    else
                    {
                        _iniFile = new IniFile(iniFilePath);
                    }
                }

                _iniFile = new IniFile(iniFilePath);

                // Validate required settings
                _connectionString = BuildConnectionString(); // Build connection string first
                _jwtSecretKey = _iniFile.Read("Jwt", "SecretKey", Guid.NewGuid().ToString()); // Use a secure default if missing
                _tokenValidityInMinutes = int.TryParse(_iniFile.Read("Jwt", "TokenValidityInMinutes", "60"), out int tokenValidity) ? tokenValidity : 60;
                _refreshTokenValidityInDays = int.TryParse(_iniFile.Read("Jwt", "RefreshTokenValidityInDays", "7"), out int refreshTokenValidity) ? refreshTokenValidity : 7;
                _jwtIssuer = _iniFile.Read("Jwt", "Issuer", "TDFAPI");
                _jwtAudience = _iniFile.Read("Jwt", "Audience", "TDFClient");

                // Allowed Origins
                string originsValue = _iniFile.Read("App", "AllowedOrigins", "");
                _allowedOrigins = new List<string>();
                if (!string.IsNullOrWhiteSpace(originsValue))
                {
                    foreach (string origin in originsValue.Split(','))
                    {
                        string trimmedOrigin = origin.Trim();
                        if (!string.IsNullOrWhiteSpace(trimmedOrigin))
                        {
                            _allowedOrigins.Add(trimmedOrigin);
                        }
                    }
                }
                else
                {
                     // Add a default origin if none are specified, e.g., the API's own origin or localhost for development
                     Console.WriteLine("Warning: No AllowedOrigins specified in config.ini. Add allowed client origins.");
                     // _allowedOrigins.Add("http://localhost:some_port"); // Example default
                }

                // Development Allowed Origins
                string devOriginsValue = _iniFile.Read("App", "DevelopmentAllowedOrigins", "");
                _developmentAllowedOrigins = new List<string>();

                if (!string.IsNullOrWhiteSpace(devOriginsValue))
                {
                    foreach (string origin in devOriginsValue.Split(','))
                    {
                        string trimmedOrigin = origin.Trim();
                        if (!string.IsNullOrWhiteSpace(trimmedOrigin))
                        {
                            _developmentAllowedOrigins.Add(trimmedOrigin);
                        }
                    }
                }
                else
                {
                    // Default development origins
                    _developmentAllowedOrigins.AddRange(new[] {
                        "http://localhost:3000",
                        "http://localhost:8080",
                        "http://localhost:4200",
                        "http://localhost:5173"
                    });
                }

                // Security settings
                _securitySettings = new Dictionary<string, object>
                {
                    ["MaxFailedLoginAttempts"] = int.TryParse(_iniFile.Read("Security", "MaxFailedLoginAttempts", "5"),
                        out int maxAttempts) ? maxAttempts : 5,
                    ["LockoutDurationMinutes"] = int.TryParse(_iniFile.Read("Security", "LockoutDurationMinutes", "15"),
                        out int lockoutMinutes) ? lockoutMinutes : 15
                };

                // Parse password requirements
                var passwordRequirements = new Dictionary<string, object>
                {
                    ["MinimumLength"] = int.TryParse(_iniFile.Read("Security", "PasswordMinimumLength", "12"),
                        out int minLength) ? minLength : 12,
                    ["RequireUppercase"] = bool.TryParse(_iniFile.Read("Security", "PasswordRequireUppercase", "true"),
                        out bool requireUpper) && requireUpper,
                    ["RequireLowercase"] = bool.TryParse(_iniFile.Read("Security", "PasswordRequireLowercase", "true"),
                        out bool requireLower) && requireLower,
                    ["RequireDigit"] = bool.TryParse(_iniFile.Read("Security", "PasswordRequireDigit", "true"),
                        out bool requireDigit) && requireDigit,
                    ["RequireSpecialCharacter"] = bool.TryParse(_iniFile.Read("Security", "PasswordRequireSpecialCharacter", "true"),
                        out bool requireSpecial) && requireSpecial
                };

                _securitySettings["PasswordRequirements"] = passwordRequirements;

                // WebSocket settings
                _webSocketSettings = new Dictionary<string, object>
                {
                    ["TimeoutMinutes"] = int.TryParse(_iniFile.Read("WebSockets", "TimeoutMinutes", "30"),
                        out int timeoutMinutes) ? timeoutMinutes : 30,
                    ["KeepAliveMinutes"] = double.TryParse(_iniFile.Read("WebSockets", "KeepAliveMinutes", "2"),
                        out double keepAliveMinutes) ? keepAliveMinutes : 2,
                    ["MaxMessagesPerMinute"] = int.TryParse(_iniFile.Read("WebSockets", "MaxMessagesPerMinute", "120"),
                        out int maxMessages) ? maxMessages : 120,
                    ["ReceiveBufferSize"] = int.TryParse(_iniFile.Read("WebSockets", "ReceiveBufferSize", "65536"),
                        out int bufferSize) ? bufferSize : 65536
                };

                // Rate limiting settings
                _rateLimitSettings = new Dictionary<string, object>
                {
                    ["GlobalLimitPerMinute"] = int.TryParse(_iniFile.Read("RateLimiting", "GlobalLimitPerMinute", "100"),
                        out int globalLimit) ? globalLimit : 100,
                    ["AuthLimitPerMinute"] = int.TryParse(_iniFile.Read("RateLimiting", "AuthLimitPerMinute", "10"),
                        out int authLimit) ? authLimit : 10,
                    ["ApiLimitPerMinute"] = int.TryParse(_iniFile.Read("RateLimiting", "ApiLimitPerMinute", "60"),
                        out int apiLimit) ? apiLimit : 60,
                    ["StaticLimitPerMinute"] = int.TryParse(_iniFile.Read("RateLimiting", "StaticLimitPerMinute", "200"),
                        out int staticLimit) ? staticLimit : 200
                };

                _isInitialized = true;

                Console.WriteLine("Configuration loaded successfully from INI file");
                Console.WriteLine($"Connection String: {MaskConnectionString(_connectionString)}");
                Console.WriteLine($"JWT Configuration: Issuer=${_jwtIssuer}, Audience=${_jwtAudience}, " +
                    $"Token Validity=${_tokenValidityInMinutes} minutes, Refresh Token Validity=${_refreshTokenValidityInDays} days");
                Console.WriteLine($"Allowed Origins: {string.Join(", ", _allowedOrigins)}");
                Console.WriteLine("Security, WebSocket, and Rate Limiting settings loaded");
            } // End of try block
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing configuration: {ex.Message}");
                // Consider logging the full exception details here
                // Log.Error(ex, "Configuration initialization failed.");
                throw new InvalidOperationException("Failed to initialize configuration. See inner exception for details.", ex);
            }
            // Removed redundant catch block here
        }

        // Public properties to access configuration values
        public static string ConnectionString
        {
            get
            {
                EnsureInitialized();
                return _connectionString;
            }
        }

        public static string JwtSecretKey
        {
            get
            {
                EnsureInitialized();
                return _jwtSecretKey;
            }
        }

        public static int TokenValidityInMinutes
        {
            get
            {
                EnsureInitialized();
                return _tokenValidityInMinutes;
            }
        }

        public static int RefreshTokenValidityInDays
        {
            get
            {
                EnsureInitialized();
                return _refreshTokenValidityInDays;
            }
        }

        public static string JwtIssuer
        {
            get
            {
                EnsureInitialized();
                return _jwtIssuer;
            }
        }

        public static string JwtAudience
        {
            get
            {
                EnsureInitialized();
                return _jwtAudience;
            }
        }

        public static void UpdateConfigFile()
        {
            try
            {
                if (_iniFile == null)
                {
                    Initialize();
                }

                if (_iniFile == null)
                {
                    throw new InvalidOperationException("Failed to initialize configuration.");
                }

                // Ensure Server section exists
                var urlsValue = _iniFile.Read("Server", "Urls", "");
                if (string.IsNullOrEmpty(urlsValue))
                {
                    _iniFile.Write("Server", "Urls", "http://localhost:5000,https://localhost:5001");
                }

                // Update ini file with current configuration
                var currentSettings = _iniFile.GetSafeConfiguration();

                // Check if any new settings need to be added
                if (!currentSettings.ContainsKey("WebSockets"))
                {
                    Console.WriteLine("Adding WebSockets section to config.ini");
                    _iniFile.Write("WebSockets", "TimeoutMinutes", "30");
                    _iniFile.Write("WebSockets", "KeepAliveMinutes", "2");
                    _iniFile.Write("WebSockets", "MaxMessagesPerMinute", "120");
                    _iniFile.Write("WebSockets", "ReceiveBufferSize", "65536");
                }

                EnsureInitialized();
                // This method seems intended to update/ensure sections exist, not return a value.
                // Removed: return _jwtAudience;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating config file: {ex.Message}");
                // Optionally re-throw or handle more gracefully
            }
        }

        public static List<string> AllowedOrigins
        {
            get
            {
                EnsureInitialized();
                return _allowedOrigins;
            }
        }

        public static List<string> DevelopmentAllowedOrigins
        {
            get
            {
                EnsureInitialized();
                return _developmentAllowedOrigins;
            }
        }

        public static T GetSecuritySetting<T>(string key, T defaultValue)
        {
            EnsureInitialized();
            if (_securitySettings.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }

            if (key == "PasswordRequirements" && _securitySettings.TryGetValue(key, out var pwdRequirements))
            {
                return (T)pwdRequirements;
            }

            return defaultValue;
        }

        public static T GetWebSocketSetting<T>(string key, T defaultValue)
        {
            EnsureInitialized();
            if (_webSocketSettings.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }
            return defaultValue;
        }

        public static T GetRateLimitSetting<T>(string key, T defaultValue)
        {
            EnsureInitialized();
            if (_rateLimitSettings.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }
            return defaultValue;
        }

        public static T GetRedisSetting<T>(string key, T defaultValue)
        {
            EnsureInitialized();
            return GetConfigurationValue("Redis", key, defaultValue);
        }

        private static T GetConfigurationValue<T>(string section, string key, T defaultValue)
        {
            var value = _iniFile.Read(section, key, null);
            if (string.IsNullOrEmpty(value))
            {
                return defaultValue;
            }

            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }

        // Add this method to the IniConfiguration class
        public static List<string> GetServerUrls()
        {
            EnsureInitialized();
            var urls = new List<string>();

            // Read from Server section in config.ini
            string urlsValue = _iniFile.Read("Server", "Urls", "");
            if (!string.IsNullOrWhiteSpace(urlsValue))
            {
                foreach (string url in urlsValue.Split(','))
                {
                    string trimmedUrl = url.Trim();
                    if (!string.IsNullOrWhiteSpace(trimmedUrl))
                    {
                        urls.Add(trimmedUrl);
                    }
                }
            }

            return urls;
        }
        // Add validation methods
        public static void ValidateConfiguration()
        {
            var errors = new List<string>();

            // Validate database connection
            if (string.IsNullOrEmpty(ConnectionString))
            {
                errors.Add("Database connection string is missing");
            }

            // Validate JWT settings
            if (string.IsNullOrEmpty(JwtIssuer))
            {
                errors.Add("JWT Issuer is missing");
            }

            if (string.IsNullOrEmpty(JwtAudience))
            {
                errors.Add("JWT Audience is missing");
            }

            // Validate server URLs
            var urls = GetServerUrls();
            if (urls.Count == 0)
            {
                errors.Add("No server URLs configured");
            }

            // In production, validate CORS origins
            if (!IsRunningInDevelopment() && AllowedOrigins.Count == 0)
            {
                errors.Add("No CORS origins configured for production");
            }

            // If there are validation errors, throw an exception
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Configuration validation failed with the following errors:\n- {string.Join("\n- ", errors)}");
            }
        }

        // Helper method to determine if running in development
        private static bool IsRunningInDevelopment()
        {
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            return string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase);
        }

        private static void EnsureInitialized()
        {
            if (!_isInitialized)
            {
                Initialize();
            }
        }

        private static string BuildConnectionString()
        {
            // Read connection method from the config file
            string connectionMethod = _iniFile.Read("Database", "ConnectionMethod", "").ToLower();
            if (string.IsNullOrWhiteSpace(connectionMethod) || (connectionMethod != "namedpipes" && connectionMethod != "tcp"))
            {
                Console.WriteLine("Connection method is missing or invalid in the config file. Using default SQL connection.");
                connectionMethod = "namedpipes";
            }

            // Check for server IP
            string serverIP = _iniFile.Read("Database", "ServerIP", "");
            if (string.IsNullOrWhiteSpace(serverIP))
            {
                Console.WriteLine("Server IP is missing in the config file. Using local server.");
                serverIP = ".";
            }

            // Check for database name
            string databaseName = _iniFile.Read("Database", "Database", "");
            if (string.IsNullOrWhiteSpace(databaseName))
            {
                Console.WriteLine("Database name is missing in the config file. Using 'Users'.");
                databaseName = "Users";
            }

            // Check for trusted connection
            string trustedConnection = _iniFile.Read("Database", "Trusted_Connection", "").ToLower();
            if (string.IsNullOrWhiteSpace(trustedConnection) || (trustedConnection != "true" && trustedConnection != "false"))
            {
                Console.WriteLine("Trusted_Connection is missing or invalid in the config file. Using 'true'.");
                trustedConnection = "true";
            }

            // Check for TrustServerCertificate
            string trustServerCertificate = _iniFile.Read("Database", "TrustServerCertificate", "").ToLower();
            if (string.IsNullOrWhiteSpace(trustServerCertificate) || (trustServerCertificate != "true" && trustServerCertificate != "false"))
            {
                Console.WriteLine("TrustServerCertificate is missing or invalid in the config file. Using 'true'.");
                trustServerCertificate = "true";
            }

            // Initialize connection string
            string connectionString;

            if (trustedConnection == "true")
            {
                if (connectionMethod == "namedpipes")
                {
                    connectionString = $"Server={serverIP};Database={databaseName};Trusted_Connection=True;TrustServerCertificate={trustServerCertificate};";
                }
                else // tcp
                {
                    string port = _iniFile.Read("Database", "Port", "1433"); // Default port is 1433
                    connectionString = $"Server={serverIP},{port};Database={databaseName};Trusted_Connection=True;TrustServerCertificate={trustServerCertificate};";
                }
            }
            else
            {
                string userId = _iniFile.Read("Database", "User_Id", "");
                string password = _iniFile.Read("Database", "Password", "");

                if (string.IsNullOrWhiteSpace(userId))
                {
                    Console.WriteLine("User_Id is missing in the config file for SQL authentication. Using 'sa'.");
                    userId = "sa";
                }

                if (connectionMethod == "namedpipes")
                {
                    connectionString = $"Server={serverIP};Database={databaseName};User Id={userId};Password={password};TrustServerCertificate={trustServerCertificate};";
                }
                else // tcp
                {
                    string port = _iniFile.Read("Database", "Port", "1433"); // Default port is 1433
                    connectionString = $"Server={serverIP},{port};Database={databaseName};User Id={userId};Password={password};TrustServerCertificate={trustServerCertificate};";
                }
            }

            // Add connection timeout
            string timeout = _iniFile.Read("Database", "ConnectionTimeout", "30");
            if (!string.IsNullOrWhiteSpace(timeout) && int.TryParse(timeout, out int timeoutValue) && timeoutValue > 0)
            {
                connectionString += $"Connection Timeout={timeoutValue};";
            }

            // Add additional connection parameters
            string minPoolSize = _iniFile.Read("Database", "MinPoolSize", "10");
            if (!string.IsNullOrWhiteSpace(minPoolSize) && int.TryParse(minPoolSize, out int minPoolSizeValue) && minPoolSizeValue > 0)
            {
                connectionString += $"Min Pool Size={minPoolSizeValue};";
            }

            string maxPoolSize = _iniFile.Read("Database", "MaxPoolSize", "200");
            if (!string.IsNullOrWhiteSpace(maxPoolSize) && int.TryParse(maxPoolSize, out int maxPoolSizeValue) && maxPoolSizeValue > 0)
            {
                connectionString += $"Max Pool Size={maxPoolSizeValue};";
            }

            // Add application name for monitoring
            connectionString += "Application Name=TDFAPI;";

            return connectionString;
        }

        private static string MaskConnectionString(string connectionString)
        {
            // Simple masking for security - don't show full connection string in logs
            return connectionString.Contains("Password=")
                ? System.Text.RegularExpressions.Regex.Replace(connectionString, "Password=([^;]*)", "Password=******")
                : connectionString;
        }
    }
}