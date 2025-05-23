using System.Diagnostics;
using System.Text.RegularExpressions;
using TDFAPI.Extensions;
using TDFShared.Constants;

namespace TDFAPI.Middleware
{
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestLoggingMiddleware> _logger;
        private static readonly HashSet<string> _sensitiveRoutes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            $"/{ApiRoutes.Auth.Login}",
            $"/{ApiRoutes.Auth.Register}",
            $"/{ApiRoutes.Auth.RefreshToken}",
            $"/{ApiRoutes.Users.ChangePassword}"
        };

        // Routes that don't need detailed logging (to reduce log volume)
        private static readonly HashSet<string> _noiseRoutes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "/health",
            $"/{ApiRoutes.Health.Base}",
            $"/{ApiRoutes.Health.GetDefault}",
            "/favicon.ico"
        };

        // Request paths that might contain sensitive data in query parameters
        private static readonly HashSet<string> _pathsWithSensitiveParams = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            $"/{ApiRoutes.Auth.ResetPassword}",
            $"/{ApiRoutes.Users.Verify}",
        };

        // Parameters that should be redacted when logging
        private static readonly HashSet<string> _sensitiveParams = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "password",
            "token",
            "refresh_token",
            "access_token",
            "auth",
            "key",
            "secret",
            "code",
            "verification",
        };

        // Regex to identify auth token in headers to redact it
        private static readonly Regex _authHeaderRegex = new Regex(@"(Bearer\s+)([A-Za-z0-9-_]+\.[A-Za-z0-9-_]+\.[A-Za-z0-9-_]+)", RegexOptions.Compiled);

        // Regex to find sensitive URL parameters
        private static readonly Regex _sensitiveParamsRegex = new Regex(
            @"(\b(?:" + string.Join("|", _sensitiveParams.Select(Regex.Escape)) + @")=)([^&]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Check if this is a route without the /api prefix that should have it
            string path = context.Request.Path.Value?.ToLower();

            // Check for double api prefix and fix it
            if (path != null && path.StartsWith($"/{ApiRoutes.Base}/{ApiRoutes.Base}/"))
            {
                string correctedPath = path.Replace($"/{ApiRoutes.Base}/{ApiRoutes.Base}/", $"/{ApiRoutes.Base}/");
                _logger.LogWarning("Detected request with double API prefix: {Path}. Rewriting to: {CorrectedPath}", path, correctedPath);
                context.Request.Path = correctedPath;
            }
            // Check for routes that should have the /api prefix but don't
            else if (path != null && !path.StartsWith($"/{ApiRoutes.Base}/") &&
                (path.StartsWith("/users/") ||
                 path.StartsWith("/auth/") ||
                 path.StartsWith("/notifications/") ||
                 path.StartsWith("/messages/") ||
                 path.StartsWith("/requests/") ||
                 path.StartsWith("/healthcheck") ||
                 path.StartsWith("/lookups/") ||
                 path.StartsWith($"/{ApiRoutes.Profile.Base.Replace(ApiRoutes.Base + "/", "")}/") ||
                 path.StartsWith($"/{ApiRoutes.Settings.Base.Replace(ApiRoutes.Base + "/", "")}/") ||
                 path.StartsWith($"/{ApiRoutes.Documents.Base.Replace(ApiRoutes.Base + "/", "")}/") ||
                 path.StartsWith($"/{ApiRoutes.Reports.Base.Replace(ApiRoutes.Base + "/", "")}/")))
            {
                _logger.LogWarning("Detected request to {Path} without /{ApiBase} prefix. Rewriting path to /{ApiBase}{OriginalPath}.", path, ApiRoutes.Base, path);
                context.Request.Path = $"/{ApiRoutes.Base}{path}";
                // Let the request continue with the rewritten path and original method
            }

            // Skip detailed logging for noisy routes
            if (_noiseRoutes.Contains(context.Request.Path))
            {
                await _next(context);
                return;
            }

            var stopwatch = Stopwatch.StartNew();
            var requestId = Guid.NewGuid().ToString();

            // Add correlation ID to the response headers
            context.Response.Headers.Append("X-Correlation-ID", requestId);

            // Safely get user agent, protecting against header injection
            var userAgent = context.Request.Headers.UserAgent.ToString();
            if (userAgent.Length > 500) // Truncate excessively long user agents
            {
                userAgent = userAgent.Substring(0, 500) + "...";
            }

            // Redact sensitive headers for security
            var authHeader = context.Request.Headers.Authorization.ToString();
            var redactedAuthHeader = RedactAuthHeader(authHeader);

            // Redact sensitive query parameters if present
            var originalQueryString = context.Request.QueryString.ToString();
            var redactedQueryString = RedactSensitiveParams(originalQueryString);

            // Redact any path parameters that might contain tokens
            var pathValue = context.Request.Path.ToString();
            var redactedPath = IsSensitivePathWithParams(pathValue)
                ? RedactPathParams(pathValue)
                : pathValue;

            // Create a scope with request info for structured logging
            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = requestId,
                ["UserAgent"] = userAgent,
                ["RemoteIP"] = context.GetRealIpAddress() ?? "unknown",
                ["Authorization"] = redactedAuthHeader,
                ["QueryString"] = redactedQueryString
            });

            // Log the start of the request (with less detail for sensitive routes)
            var username = context.User?.Identity?.IsAuthenticated ?? false
                ? context.User?.Identity?.Name
                : "Anonymous";

            var isSensitiveRoute = IsSensitiveRoute(context.Request.Path);

            if (!isSensitiveRoute)
            {
                _logger.LogInformation(
                    "Request {Method} {Path} started by {Username}",
                    context.Request.Method,
                    redactedPath,
                    username);
            }
            else
            {
                _logger.LogInformation(
                    "Request to sensitive endpoint {Method} {Path} started",
                    context.Request.Method,
                    redactedPath);
            }

            try
            {
                await _next(context);
            }
            catch (Exception)
            {
                // Don't handle the exception here, let it bubble up to the global exception handler
                throw;
            }
            finally
            {
                stopwatch.Stop();

                // Log request completion but avoid logging sensitive route details
                if (!isSensitiveRoute)
                {
                    _logger.LogInformation(
                        "Request {Method} {Path} completed with status {StatusCode} in {ElapsedMs}ms",
                        context.Request.Method,
                        redactedPath,
                        context.Response.StatusCode,
                        stopwatch.ElapsedMilliseconds);
                }
                else
                {
                    _logger.LogInformation(
                        "Request to sensitive endpoint completed with status {StatusCode} in {ElapsedMs}ms",
                        context.Response.StatusCode,
                        stopwatch.ElapsedMilliseconds);
                }

                // Log performance warnings for slow requests
                if (stopwatch.ElapsedMilliseconds > 1000)
                {
                    _logger.LogWarning(
                        "Slow request detected: {Method} {Path} took {ElapsedMs}ms",
                        context.Request.Method,
                        redactedPath,
                        stopwatch.ElapsedMilliseconds);
                }
            }
        }

        private bool IsSensitiveRoute(string path)
        {
            foreach (var route in _sensitiveRoutes)
            {
                if (path.StartsWith(route, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private bool IsSensitivePathWithParams(string path)
        {
            foreach (var route in _pathsWithSensitiveParams)
            {
                if (path.StartsWith(route, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private string RedactAuthHeader(string authHeader)
        {
            if (string.IsNullOrEmpty(authHeader))
            {
                return string.Empty;
            }

            // Redact JWT token, keeping only the type (e.g., "Bearer")
            return _authHeaderRegex.Replace(authHeader, "$1[REDACTED]");
        }

        private string RedactSensitiveParams(string queryString)
        {
            if (string.IsNullOrEmpty(queryString))
            {
                return string.Empty;
            }

            return _sensitiveParamsRegex.Replace(queryString, "$1[REDACTED]");
        }

        private string RedactPathParams(string path)
        {
            // Handle common path parameter patterns
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < segments.Length; i++)
            {
                // If segment is very long (likely a token) or contains dots (like JWT)
                if (segments[i].Length > 20 || segments[i].Contains('.'))
                {
                    segments[i] = "[REDACTED]";
                }
            }

            return "/" + string.Join("/", segments);
        }
    }
}