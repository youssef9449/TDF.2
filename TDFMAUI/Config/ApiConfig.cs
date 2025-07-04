using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Networking;
using TDFMAUI.Helpers;
using TDFMAUI.Services;
using TDFShared.Constants;

namespace TDFMAUI.Config
{
    /// <summary>
    /// Centralized configuration for API communication.
    /// Handles loading settings from appsettings.json and provides runtime configuration.
    /// </summary>
    public static class ApiConfig
    {
        // Server configuration - URLs will be loaded from appsettings.json
        // Default URLs (will be overridden by appsettings.json)
        public static string BaseUrl { get; set; } = null;
        public static string WebSocketUrl { get; set; } = null;

        // Development mode flag - controls certificate validation
        public static bool DevelopmentMode { get; set; } = false;

        // Alias for DevelopmentMode for compatibility
        public static bool IsDevelopmentMode
        {
            get => DevelopmentMode;
            set => DevelopmentMode = value;
        }

        // Safe mode flag - used when recovering from crashes
        public static bool SafeMode { get; set; } = false;

        // API request timeout in seconds
        public static int Timeout { get; set; } = 30;

        // Retry configuration
        public static int MaxRetries { get; set; } = 3;
        public static int RetryDelay { get; set; } = 1000; // milliseconds
        public static double RetryMultiplier { get; set; } = 2.0; // exponential backoff multiplier

        // Authentication configuration
        public static string AuthTokenKey { get; set; } = "auth_token";
        public static string RefreshTokenKey { get; set; } = "refresh_token";

        // Token storage - now backed by UserSessionService when available
        private static IUserSessionService? _userSessionService;
        private static string? _fallbackCurrentToken;
        private static DateTime _fallbackTokenExpiration = DateTime.MinValue;
        private static string? _fallbackCurrentRefreshToken;
        private static DateTime _fallbackRefreshTokenExpiration = DateTime.MinValue;

        public static string? CurrentToken 
        { 
            get => _userSessionService?.CurrentToken ?? _fallbackCurrentToken;
            set 
            {
                if (_userSessionService != null)
                {
                    // Update via session service if available
                    _userSessionService.SetTokens(value, TokenExpiration, CurrentRefreshToken, RefreshTokenExpiration);
                }
                else
                {
                    // Fallback to local storage
                    _fallbackCurrentToken = value;
                }
            }
        }

        public static DateTime TokenExpiration 
        { 
            get => _userSessionService?.TokenExpiration ?? _fallbackTokenExpiration;
            set => _fallbackTokenExpiration = value;
        }

        public static string? CurrentRefreshToken 
        { 
            get => _userSessionService?.CurrentRefreshToken ?? _fallbackCurrentRefreshToken;
            set 
            {
                if (_userSessionService != null)
                {
                    // Update via session service if available
                    _userSessionService.SetTokens(CurrentToken, TokenExpiration, value, RefreshTokenExpiration);
                }
                else
                {
                    // Fallback to local storage
                    _fallbackCurrentRefreshToken = value;
                }
            }
        }

        public static DateTime RefreshTokenExpiration 
        { 
            get => _userSessionService?.RefreshTokenExpiration ?? _fallbackRefreshTokenExpiration;
            set => _fallbackRefreshTokenExpiration = value;
        }

        public static bool IsTokenValid => _userSessionService?.IsTokenValid ?? 
            (!string.IsNullOrEmpty(_fallbackCurrentToken) && _fallbackTokenExpiration > DateTime.UtcNow.AddMinutes(5));

        public static bool IsRefreshTokenValid => _userSessionService?.IsRefreshTokenValid ?? 
            (!string.IsNullOrEmpty(_fallbackCurrentRefreshToken) && _fallbackRefreshTokenExpiration > DateTime.UtcNow.AddMinutes(5));

        // Flag to track if initialization was attempted
        private static bool _initialized = false;

        // Secure certificate validation callback for development mode only
        public static readonly RemoteCertificateValidationCallback DevelopmentCertificateValidationCallback =
            (sender, certificate, chain, sslPolicyErrors) => {
                // Log the certificate details for debugging
                var cert = certificate as X509Certificate2;
                if (cert != null) {
                    DebugService.LogInfo("CertValidation", $"Subject: {cert.Subject}");
                    DebugService.LogInfo("CertValidation", $"Issuer: {cert.Issuer}");
                    DebugService.LogInfo("CertValidation", $"Valid from {cert.NotBefore} to {cert.NotAfter}");
                }

                DebugService.LogWarning("CertValidation", $"SSL Policy Errors: {sslPolicyErrors}");

                // Only allow self-signed certificates in development mode for localhost/development servers
                if (DevelopmentMode && cert != null)
                {
                    // Allow localhost and development server certificates
                    var subject = cert.Subject.ToLowerInvariant();
                    if (subject.Contains("localhost") || subject.Contains("127.0.0.1") || subject.Contains("192.168."))
                    {
                        DebugService.LogWarning("CertValidation", "Allowing development certificate for local/development server");
                        return true;
                    }
                }

                // Use default validation for all other cases
                return sslPolicyErrors == SslPolicyErrors.None;
            };

        // Diagnostic information
        public static bool IsNetworkAvailable => CheckNetworkAvailability();
        public static List<string> InitializationErrors { get; private set; } = new List<string>();

        // Crash recovery information
        public static int CrashCount { get; private set; } = 0;
        public static DateTime LastCrashTime { get; private set; } = DateTime.MinValue;

        /// <summary>
        /// Connects the UserSessionService to ApiConfig for centralized token management
        /// </summary>
        /// <param name="userSessionService">The user session service instance</param>
        public static void ConnectUserSessionService(IUserSessionService userSessionService)
        {
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));
            DebugService.LogInfo("ApiConfig", "UserSessionService connected for centralized token management");
        }

        /// <summary>
        /// Configure global SSL/TLS settings for the application
        /// </summary>
        public static void ConfigureGlobalSslSettings()
        {
            try
            {
                // For development mode, set up global certificate validation
                if (DevelopmentMode)
                {
                    // Set ServicePointManager default settings
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
                    ServicePointManager.ServerCertificateValidationCallback = DevelopmentCertificateValidationCallback;

                    DebugService.LogWarning("ApiConfig", "Configured global SSL settings with secure development certificate validation");
                }
                else
                {
                    // Use default validation in production
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
                    DebugService.LogInfo("ApiConfig", "Using default certificate validation in production mode");
                }
            }
            catch (Exception ex)
            {
                DebugService.LogError("ApiConfig", $"Error configuring global SSL settings: {ex.Message}");
                InitializationErrors.Add($"SSL Configuration Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Initialize API configuration with environment-specific settings and/or from appsettings.json
        /// </summary>
        public static void Initialize(bool isDevelopment = false, bool safeMode = false)
        {
            if (_initialized) return;

            // Set safe mode flag
            SafeMode = safeMode;

            // If in safe mode, log it prominently
            if (SafeMode)
            {
                DebugService.LogWarning("ApiConfig", "*** INITIALIZING IN SAFE MODE ***");
                DebugService.LogWarning("ApiConfig", "Some features may be disabled for stability");
            }

            try
            {
                // Set development mode flag
                DevelopmentMode = isDevelopment;

                // Development mode will be used to determine which URLs to load from appsettings.json
                DebugService.LogInfo("ApiConfig", isDevelopment ?
                    "Will load DEVELOPMENT server URLs from appsettings.json" :
                    "Will load PRODUCTION server URLs from appsettings.json");

                // Try to load settings from appsettings.json (may override the URLs)
                LoadFromAppSettings();

                // Apply additional environment-specific settings
                if (isDevelopment)
                {
                    // Development-specific overrides
                    if (!DevelopmentMode) DevelopmentMode = true; // Ensure dev mode is on
                    Timeout = 60; // Longer timeout for development

                    DebugService.LogInfo("ApiConfig", "Running in DEVELOPMENT mode");
                }
                else
                {
                    DebugService.LogInfo("ApiConfig", "Running in PRODUCTION mode");
                }

                // Ensure WebSocketUrl is properly set based on BaseUrl
                if (string.IsNullOrEmpty(WebSocketUrl))
                {
                    WebSocketUrl = ConstructWebSocketUrl(BaseUrl);
                }

                // Log configuration
                DebugService.LogInfo("ApiConfig", $"Initialized with BaseUrl: {BaseUrl}");
                DebugService.LogInfo("ApiConfig", $"WebSocketUrl: {WebSocketUrl}");
                DebugService.LogInfo("ApiConfig", $"DevelopmentMode: {DevelopmentMode}");
                DebugService.LogInfo("ApiConfig", $"Timeout: {Timeout} seconds");
                DebugService.LogInfo("ApiConfig", $"Retry configuration: MaxRetries={MaxRetries}, Delay={RetryDelay}ms, Multiplier={RetryMultiplier}");
            }
            catch (Exception ex)
            {
                InitializationErrors.Add(ex.Message);
                DebugService.LogError("ApiConfig", ex);
            }

            _initialized = true;
        }

        /// <summary>
        /// Load API settings from appsettings.json if available
        /// </summary>
        private static void LoadFromAppSettings()
        {
            try
            {
                // Try to get the appsettings.json from output directory
                string filePath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
                if (!File.Exists(filePath))
                {
                    DebugService.LogWarning("ApiConfig", $"Could not find appsettings.json at {filePath}");
                    return;
                }

                string json = File.ReadAllText(filePath);

                // Use centralized JSON options for consistency
                var settings = JsonSerializer.Deserialize<AppSettings>(json, TDFShared.Helpers.JsonSerializationHelper.BasicOptions);

                if (settings?.ApiSettings != null)
                {
                    // Apply environment-specific URLs if available
                    if (DevelopmentMode && settings.ApiSettings.Development != null)
                    {
                        // Apply development URLs
                        if (!string.IsNullOrEmpty(settings.ApiSettings.Development.BaseUrl))
                            BaseUrl = settings.ApiSettings.Development.BaseUrl;

                        if (!string.IsNullOrEmpty(settings.ApiSettings.Development.WebSocketUrl))
                            WebSocketUrl = settings.ApiSettings.Development.WebSocketUrl;
                        else
                            WebSocketUrl = ConstructWebSocketUrl(BaseUrl);

                        DebugService.LogInfo("ApiConfig", $"Loaded DEVELOPMENT URLs from appsettings.json: {BaseUrl}");
                    }
                    else if (!DevelopmentMode && settings.ApiSettings.Production != null)
                    {
                        // Apply production URLs
                        if (!string.IsNullOrEmpty(settings.ApiSettings.Production.BaseUrl))
                            BaseUrl = settings.ApiSettings.Production.BaseUrl;

                        if (!string.IsNullOrEmpty(settings.ApiSettings.Production.WebSocketUrl))
                            WebSocketUrl = settings.ApiSettings.Production.WebSocketUrl;
                        else
                            WebSocketUrl = ConstructWebSocketUrl(BaseUrl);

                        DebugService.LogInfo("ApiConfig", $"Loaded PRODUCTION URLs from appsettings.json: {BaseUrl}");
                    }

                    // Apply other settings
                    Timeout = settings.ApiSettings.Timeout > 0 ? settings.ApiSettings.Timeout : Timeout;
                    MaxRetries = settings.ApiSettings.MaxRetries > 0 ? settings.ApiSettings.MaxRetries : MaxRetries;
                    RetryDelay = settings.ApiSettings.RetryDelay > 0 ? settings.ApiSettings.RetryDelay : RetryDelay;
                    RetryMultiplier = settings.ApiSettings.RetryMultiplier > 0 ? settings.ApiSettings.RetryMultiplier : RetryMultiplier;

                    DebugService.LogInfo("ApiConfig", "Loaded settings from appsettings.json");
                }
                else
                {
                    DebugService.LogWarning("ApiConfig", "ApiSettings section not found in appsettings.json");
                }
            }
            catch (Exception ex)
            {
                DebugService.LogError("ApiConfig", $"Error loading settings: {ex.Message}");
                // Continue with default settings rather than throwing
            }
        }

        /// <summary>
        /// Constructs WebSocket URL from API base URL
        /// </summary>
        private static string ConstructWebSocketUrl(string baseUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(baseUrl))
                    throw new ArgumentNullException(nameof(baseUrl), "BaseUrl is null when constructing WebSocket URL.");
                // Convert http/https to ws/wss
                var url = baseUrl.TrimEnd('/');

                // Remove /api path segment if present
                if (url.EndsWith("/" + ApiRoutes.Base, StringComparison.OrdinalIgnoreCase))
                {
                    url = url.Substring(0, url.Length - (ApiRoutes.Base.Length + 1));
                }

                // Replace http/https protocol with ws/wss
                url = url.Replace("http://", "ws://").Replace("https://", "wss://");

                // Add WebSocket path from ApiRoutes
                url = $"{url}{ApiRoutes.WebSocket.Connect}";

                DebugService.LogInfo("ApiConfig", $"Derived WebSocket URL: {url} from Base URL: {baseUrl}");
                return url;
            }
            catch (Exception ex)
            {
                DebugService.LogError("ApiConfig", $"Error constructing WebSocket URL: {ex.Message}");
                return $"wss://localhost:7079{ApiRoutes.WebSocket.Connect}"; // Fallback to default
            }
        }

        /// <summary>
        /// Check if network is available
        /// </summary>
        private static bool CheckNetworkAvailability()
        {
            try
            {
                var current = Connectivity.NetworkAccess;
                return current == NetworkAccess.Internet || current == NetworkAccess.ConstrainedInternet;
            }
            catch (Exception ex)
            {
                DebugService.LogError("ApiConfig", $"Error checking network: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Run a simple API connectivity test with detailed diagnostics
        /// </summary>
        public static async Task<bool> TestApiConnectivityAsync(TDFShared.Services.IHttpClientService httpClientService = null)
        {
            if (string.IsNullOrEmpty(BaseUrl))
            {
                 DebugService.LogError("ApiConfig", "Cannot test connectivity: BaseUrl is not configured.");
                 return false;
            }

            // First check basic network connectivity
            if (!IsNetworkAvailable)
            {
                DebugService.LogError("ApiConfig", "Network is not available. Check device connectivity.");
                return false;
            }

            // Try multiple health check endpoints with different protocols
            var baseUrlHttp = BaseUrl.Replace("https://", "http://");
            var baseUrlHttps = BaseUrl.Replace("http://", "https://");

            string[] healthCheckUrls = new[] {
                // Try the configured URL first with ApiRoutes
                BaseUrl.TrimEnd('/') + "/" + ApiRoutes.Health.Ping,
                // Fallback to legacy paths
                BaseUrl.TrimEnd('/') + $"/{ApiRoutes.Health.Base}/ping",
                BaseUrl.TrimEnd('/') + $"/{ApiRoutes.Health.GetDefault}",
                // Then try alternative protocols
                baseUrlHttp.TrimEnd('/') + "/" + ApiRoutes.Health.Ping,
                baseUrlHttps.TrimEnd('/') + "/" + ApiRoutes.Health.Ping,
                // Try root path as last resort
                BaseUrl.TrimEnd('/') + "/"
            };

            DebugService.LogInfo("ApiConfig", $"Testing API connectivity at multiple endpoints");

            // Try DNS resolution first to diagnose network issues
            try
            {
                var uri = new Uri(BaseUrl);
                DebugService.LogInfo("ApiConfig", $"Resolving DNS for {uri.Host}...");

                var hostEntry = await Dns.GetHostEntryAsync(uri.Host);
                if (hostEntry != null && hostEntry.AddressList.Length > 0)
                {
                    DebugService.LogInfo("ApiConfig", $"DNS resolution successful. IP addresses: {string.Join(", ", hostEntry.AddressList.Select(ip => ip.ToString()))}");
                }
                else
                {
                    DebugService.LogWarning("ApiConfig", $"DNS resolution returned no IP addresses for {uri.Host}");
                }
            }
            catch (Exception dnsEx)
            {
                DebugService.LogError("ApiConfig", $"DNS resolution failed: {dnsEx.Message}");
            }

            foreach (var healthCheckUrl in healthCheckUrls)
            {
                try
                {
                    DebugService.LogInfo("ApiConfig", $"Trying endpoint: {healthCheckUrl}");

                    // Use shared HTTP client service if available, otherwise fall back to custom implementation
                    if (httpClientService != null)
                    {
                        DebugService.LogInfo("ApiConfig", "Using shared HTTP client service for connectivity test");

                        // Add diagnostic headers to the shared service
                        httpClientService.AddDefaultHeader("User-Agent", $"TDFMAUI-DiagnosticTest/{AppInfo.VersionString}");
                        httpClientService.AddDefaultHeader("X-Diagnostic-Mode", "true");

                        // Use DeviceHelper to determine platform
                        string platform = "Unknown";
                        if (DeviceHelper.IsWindows) platform = "Windows";
                        else if (DeviceHelper.IsMacOS) platform = "MacOS";
                        else if (DeviceHelper.IsIOS) platform = "iOS";
                        else if (DeviceHelper.IsAndroid) platform = "Android";

                        httpClientService.AddDefaultHeader("X-Device-Platform", platform);
                        httpClientService.AddDefaultHeader("X-Device-Model", DeviceInfo.Model);

                        // Log the request details
                        DebugService.LogInfo("ApiConfig", $"Sending GET request to {healthCheckUrl}");
                        DebugService.LogInfo("ApiConfig", $"User-Agent: TDFMAUI-DiagnosticTest/{AppInfo.VersionString}");

                        // Make the request using shared service
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                        var responseContent = await httpClientService.GetRawAsync(healthCheckUrl, cts.Token);

                        // If we get here, the request was successful
                        DebugService.LogInfo("ApiConfig", $"Response content: {(responseContent.Length > 100 ? responseContent.Substring(0, 100) + "..." : responseContent)}");
                        DebugService.LogInfo("ApiConfig", $"Connectivity test SUCCESSFUL for {healthCheckUrl}");

                        // Clean up diagnostic headers
                        httpClientService.RemoveDefaultHeader("User-Agent");
                        httpClientService.RemoveDefaultHeader("X-Diagnostic-Mode");
                        httpClientService.RemoveDefaultHeader("X-Device-Platform");
                        httpClientService.RemoveDefaultHeader("X-Device-Model");

                        return true;
                    }
                    else
                    {
                        // Fallback to custom HttpClient for diagnostic purposes when shared service is not available
                        DebugService.LogWarning("ApiConfig", "Shared HTTP client service not available, using fallback diagnostic client");

                        var handler = new HttpClientHandler();

                        // Always enable these settings for better compatibility
                        handler.SslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13;
                        handler.CheckCertificateRevocationList = false;
                        handler.AllowAutoRedirect = true;

                        if (DevelopmentMode)
                        {
                            // Use our global certificate validation callback
                            handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => DevelopmentCertificateValidationCallback(message, cert, chain, errors);
                            DebugService.LogWarning("ApiConfig", "Connectivity test using secure development certificate validation.");
                        }

                        using var client = new HttpClient(handler);
                        client.Timeout = TimeSpan.FromSeconds(30); // Longer timeout for thorough testing

                        // Add headers to help with debugging
                        client.DefaultRequestHeaders.Add("User-Agent", $"TDFMAUI-FallbackDiagnosticTest/{AppInfo.VersionString}");
                        client.DefaultRequestHeaders.Add("X-Diagnostic-Mode", "true");

                        // Use DeviceHelper to determine platform
                        string platform = "Unknown";
                        if (DeviceHelper.IsWindows) platform = "Windows";
                        else if (DeviceHelper.IsMacOS) platform = "MacOS";
                        else if (DeviceHelper.IsIOS) platform = "iOS";
                        else if (DeviceHelper.IsAndroid) platform = "Android";

                        client.DefaultRequestHeaders.Add("X-Device-Platform", platform);
                        client.DefaultRequestHeaders.Add("X-Device-Model", DeviceInfo.Model);

                        // Log the request details
                        DebugService.LogInfo("ApiConfig", $"Sending GET request to {healthCheckUrl}");
                        DebugService.LogInfo("ApiConfig", $"User-Agent: TDFMAUI-FallbackDiagnosticTest/{AppInfo.VersionString}");

                        // Make the request with a cancellation token
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                        var response = await client.GetAsync(healthCheckUrl, cts.Token);

                        // Log detailed response information
                        DebugService.LogInfo("ApiConfig", $"Response status: {(int)response.StatusCode} {response.StatusCode}");
                        DebugService.LogInfo("ApiConfig", $"Response headers: {string.Join(", ", response.Headers.Select(h => $"{h.Key}={string.Join(",", h.Value)}"))}");

                        if (response.IsSuccessStatusCode)
                        {
                            // Try to read the response content for additional diagnostics
                            try
                            {
                                var content = await response.Content.ReadAsStringAsync();
                                DebugService.LogInfo("ApiConfig", $"Response content: {(content.Length > 100 ? content.Substring(0, 100) + "..." : content)}");
                            }
                            catch (Exception contentEx)
                            {
                                DebugService.LogWarning("ApiConfig", $"Could not read response content: {contentEx.Message}");
                            }

                            DebugService.LogInfo("ApiConfig", $"Connectivity test SUCCESSFUL for {healthCheckUrl}");
                            return true;
                        }
                        else
                        {
                            DebugService.LogWarning("ApiConfig", $"Received non-success status code: {response.StatusCode}");
                        }
                    }
                }
                catch (HttpRequestException hex)
                {
                    DebugService.LogError("ApiConfig", $"Connectivity test failed for {healthCheckUrl} (HttpRequestException): {hex.Message}");
                    if (hex.InnerException != null)
                    {
                        DebugService.LogError("ApiConfig", $"Inner Exception: {hex.InnerException.GetType().Name}: {hex.InnerException.Message}");
                    }
                    // Continue to next URL
                }
                catch (TaskCanceledException tcex) // Catches timeouts
                {
                    DebugService.LogError("ApiConfig", $"Connectivity test failed for {healthCheckUrl} (Timeout): {tcex.Message}");
                    // Continue to next URL
                }
                catch (Exception ex)
                {
                    DebugService.LogError("ApiConfig", $"Connectivity test failed for {healthCheckUrl} (General Exception): {ex.GetType().Name}: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        DebugService.LogError("ApiConfig", $"Inner Exception: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
                    }
                    // Continue to next URL
                }
            }

            // If we get here, all URLs failed
            DebugService.LogError("ApiConfig", "All connectivity tests FAILED. API server may be unreachable.");
            return false;
        }

        /// <summary>
        /// Record a crash for crash recovery tracking
        /// </summary>
        public static void RecordCrash()
        {
            CrashCount++;
            LastCrashTime = DateTime.Now;

            // Log the crash count
            DebugService.LogWarning("ApiConfig", $"Crash recorded. Total crash count: {CrashCount}");

            // If we've had multiple crashes in a short time, enable safe mode
            if (CrashCount >= 3 && (DateTime.Now - LastCrashTime).TotalMinutes < 5)
            {
                SafeMode = true;
                DebugService.LogWarning("ApiConfig", "Multiple crashes detected. Safe mode enabled.");
            }
        }
    }
}