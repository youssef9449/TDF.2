using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using TDFAPI.Extensions;

namespace TDFAPI.Middleware
{
    /// <summary>
    /// Middleware for monitoring and detecting potential security threats
    /// </summary>
    public class SecurityMonitoringMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<SecurityMonitoringMiddleware> _logger;
        
        // Use concurrent dictionaries to track potential threat sources
        private static readonly ConcurrentDictionary<string, ClientTracker> _ipTracking = new();
        private static readonly ConcurrentDictionary<string, int> _blockedIps = new();
        
        // Regular expressions to detect common attack patterns
        private static readonly Regex _sqlInjectionPattern = new(
            @"(?:'|--|\b(select|union|insert|drop|alter|declare|xp_)\b)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
            
        private static readonly Regex _xssPattern = new(
            @"(?:<script|<img|alert\(|on(?:load|click|mouseover|error|focus)=)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
            
        private static readonly Regex _pathTraversalPattern = new(
            @"(?:\.\.\/|\.\.\\|%2e%2e%2f|%252e%252e%252f)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        
        private const int SUSPICIOUS_THRESHOLD = 5;
        private const int BLOCK_THRESHOLD = 10;
        private static Timer _cleanupTimer;
        
        public SecurityMonitoringMiddleware(RequestDelegate next, ILogger<SecurityMonitoringMiddleware> logger)
        {
            _next = next;
            _logger = logger;
            
            // Initialize cleanup timer if not already running
            if (_cleanupTimer == null)
            {
                _cleanupTimer = new Timer(_ => CleanupTrackers(), null, 
                    TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(15));
            }
        }
        
        public async Task InvokeAsync(HttpContext context)
        {
            // Get client IP
            var clientIp = context.GetRealIpAddress();
            
            // Check if IP is blocked
            if (_blockedIps.ContainsKey(clientIp))
            {
                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                await context.Response.WriteAsync("Access denied due to suspicious activity.");
                
                _logger.LogWarning("Blocked request from previously flagged IP: {IP}", clientIp);
                return;
            }
            
            // Get or create tracker for this IP
            var tracker = _ipTracking.GetOrAdd(clientIp, ip => new ClientTracker(ip));
            
            // Check request for suspicious patterns
            var suspiciousScore = CalculateSuspiciousScore(context);
            
            if (suspiciousScore > 0)
            {
                tracker.SuspiciousActivityCount += suspiciousScore;
                tracker.LastSuspiciousActivity = DateTime.UtcNow;
                
                _logger.LogWarning(
                    "Suspicious activity detected from {IP}. " +
                    "Method: {Method}, Path: {Path}, " +
                    "Score: {Score}, Total: {Total}", 
                    clientIp,
                    context.Request.Method,
                    context.Request.Path,
                    suspiciousScore,
                    tracker.SuspiciousActivityCount);
                
                // Block IP if threshold is exceeded
                if (tracker.SuspiciousActivityCount >= BLOCK_THRESHOLD)
                {
                    _blockedIps.TryAdd(clientIp, 1);
                    
                    _logger.LogWarning(
                        "IP {IP} has been blocked due to suspicious activity count: {Count}", 
                        clientIp, 
                        tracker.SuspiciousActivityCount);
                        
                    context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                    await context.Response.WriteAsync("Access denied due to suspicious activity.");
                    return;
                }
                
                // Add delay for suspicious requests to slow down potential attacks
                if (tracker.SuspiciousActivityCount >= SUSPICIOUS_THRESHOLD)
                {
                    await Task.Delay(1000); // 1 second delay
                }
            }
            
            // Update request count
            tracker.RequestCount++;
            tracker.LastRequestTime = DateTime.UtcNow;
            
            // Continue with the request
            await _next(context);
        }
        
        /// <summary>
        /// Calculate a suspicious score based on request patterns
        /// </summary>
        private int CalculateSuspiciousScore(HttpContext context)
        {
            int score = 0;
            
            // Check URL path for suspicious patterns
            var path = context.Request.Path.Value ?? string.Empty;
            
            if (_sqlInjectionPattern.IsMatch(path))
            {
                score += 3;
            }
            
            if (_xssPattern.IsMatch(path))
            {
                score += 3;
            }
            
            if (_pathTraversalPattern.IsMatch(path))
            {
                score += 4;
            }
            
            // Check query string
            var query = context.Request.QueryString.Value ?? string.Empty;
            
            if (_sqlInjectionPattern.IsMatch(query))
            {
                score += 3;
            }
            
            if (_xssPattern.IsMatch(query))
            {
                score += 3;
            }
            
            // Check common attack vectors in headers
            if (context.Request.Headers.TryGetValue("User-Agent", out var userAgent))
            {
                var userAgentValue = userAgent.ToString();
                
                if (userAgentValue.Contains("sqlmap", StringComparison.OrdinalIgnoreCase) ||
                    userAgentValue.Contains("nikto", StringComparison.OrdinalIgnoreCase) ||
                    userAgentValue.Contains("nessus", StringComparison.OrdinalIgnoreCase) ||
                    userAgentValue.Contains("vulnerability", StringComparison.OrdinalIgnoreCase) ||
                    userAgentValue.Contains("scanner", StringComparison.OrdinalIgnoreCase))
                {
                    score += 5;
                }
            }
            
            // Check for unusual HTTP methods
            var method = context.Request.Method.ToUpperInvariant();
            if (method != "GET" && method != "POST" && method != "PUT" && method != "DELETE" && method != "OPTIONS")
            {
                score += 2;
            }
            
            // Check for unusual headers
            foreach (var header in context.Request.Headers)
            {
                if (_sqlInjectionPattern.IsMatch(header.Value.ToString()) ||
                    _xssPattern.IsMatch(header.Value.ToString()))
                {
                    score += 3;
                    break;
                }
            }
            
            return score;
        }
        
        /// <summary>
        /// Periodically clean up old trackers to prevent memory leaks
        /// </summary>
        private void CleanupTrackers()
        {
            try
            {
                var now = DateTime.UtcNow;
                var cleanupTime = TimeSpan.FromHours(24);
                
                foreach (var ipEntry in _ipTracking.ToArray())
                {
                    var tracker = ipEntry.Value;
                    
                    // Clean up entries that haven't had activity in 24 hours
                    if (now - tracker.LastRequestTime > cleanupTime && 
                        now - tracker.LastSuspiciousActivity > cleanupTime)
                    {
                        _ipTracking.TryRemove(ipEntry.Key, out _);
                    }
                }
                
                // Log cleanup results
                _logger.LogInformation(
                    "Security tracker cleanup completed. Remaining tracked IPs: {Count}, Blocked IPs: {BlockedCount}", 
                    _ipTracking.Count, 
                    _blockedIps.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during security tracker cleanup: {Message}", ex.Message);
            }
        }
        
        /// <summary>
        /// Class to track client behavior for security monitoring
        /// </summary>
        private class ClientTracker
        {
            public string ClientIp { get; }
            public int RequestCount { get; set; }
            public int SuspiciousActivityCount { get; set; }
            public DateTime FirstSeen { get; }
            public DateTime LastRequestTime { get; set; }
            public DateTime LastSuspiciousActivity { get; set; }
            
            public ClientTracker(string clientIp)
            {
                ClientIp = clientIp;
                FirstSeen = DateTime.UtcNow;
                LastRequestTime = DateTime.UtcNow;
                LastSuspiciousActivity = DateTime.MinValue;
            }
        }
    }
    
    // Extension method for easy middleware registration
    public static class SecurityMonitoringMiddlewareExtensions
    {
        public static IApplicationBuilder UseSecurityMonitoring(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<SecurityMonitoringMiddleware>();
        }
    }
} 