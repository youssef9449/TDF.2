using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TDFAPI.Middleware
{
    public class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<SecurityHeadersMiddleware> _logger;
        private readonly string _allowedOrigins;

        public SecurityHeadersMiddleware(
            RequestDelegate next, 
            IWebHostEnvironment environment,
            IConfiguration configuration,
            ILogger<SecurityHeadersMiddleware> logger)
        {
            _next = next;
            _environment = environment;
            _logger = logger;
            _allowedOrigins = configuration["AllowedOrigins"] ?? string.Empty;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                // Skip applying security headers to WebSocket connections
                if (context.WebSockets.IsWebSocketRequest)
                {
                    await _next(context);
                    return;
                }

                // Basic security headers
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                context.Response.Headers["X-Frame-Options"] = "DENY";
                context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
                context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                
                // Modern security headers
                // Cross-Origin isolation headers - only apply to HTML content
                if (IsHtmlResponse(context))
                {
                    context.Response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";
                    context.Response.Headers["Cross-Origin-Embedder-Policy"] = "require-corp";
                }
                
                context.Response.Headers["Cross-Origin-Resource-Policy"] = "same-origin";
                
                // Add Strict-Transport-Security header with proper values
                var hstsValue = _environment.IsProduction()
                    ? "max-age=31536000; includeSubDomains; preload" // 1 year for production
                    : "max-age=86400"; // 1 day for non-production
                context.Response.Headers["Strict-Transport-Security"] = hstsValue;
                
                // Add comprehensive Permissions-Policy
                context.Response.Headers["Permissions-Policy"] = 
                    "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), " +
                    "microphone=(), payment=(), usb=(), interest-cohort=(), autoplay=(), " +
                    "ambient-light-sensor=(), battery=(), display-capture=(), " +
                    "document-domain=(), encrypted-media=(), execution-while-not-rendered=(), " +
                    "execution-while-out-of-viewport=(), fullscreen=(self), " +
                    "publickey-credentials-get=(), screen-wake-lock=(), " +
                    "web-share=(), xr-spatial-tracking=()";
                
                // Cache control handling based on request type
                ApplyCacheControl(context);
                
                // Content Security Policy - with better protection
                if (IsHtmlResponse(context))
                {
                    ApplyContentSecurityPolicy(context);
                }
                
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SecurityHeadersMiddleware");
                throw;
            }
        }

        private void ApplyCacheControl(HttpContext context)
        {
            // Static assets can be cached longer (1 day)
            if (context.Request.Path.StartsWithSegments("/static") || 
                context.Request.Path.Value?.EndsWith(".css") == true ||
                context.Request.Path.Value?.EndsWith(".js") == true ||
                context.Request.Path.Value?.EndsWith(".png") == true ||
                context.Request.Path.Value?.EndsWith(".jpg") == true ||
                context.Request.Path.Value?.EndsWith(".svg") == true ||
                context.Request.Path.Value?.EndsWith(".woff") == true ||
                context.Request.Path.Value?.EndsWith(".woff2") == true)
            {
                context.Response.Headers["Cache-Control"] = _environment.IsProduction()
                    ? "public, max-age=86400, must-revalidate"
                    : "public, max-age=0, must-revalidate";
                return;
            }

            // API endpoints - no caching by default
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.Headers["Cache-Control"] = "no-store, max-age=0";
                return;
            }

            // Default policy for other resources
            context.Response.Headers["Cache-Control"] = "no-store, max-age=0";
        }

        private void ApplyContentSecurityPolicy(HttpContext context)
        {
            var allowedSources = string.IsNullOrEmpty(_allowedOrigins) 
                ? "'self'" 
                : $"'self' {_allowedOrigins}";

            var cspValue = $"default-src {allowedSources}; " +
                       $"script-src {allowedSources}; " +
                       "object-src 'none'; " +
                       $"img-src {allowedSources} data:; " +
                       $"style-src {allowedSources}; " +
                       $"font-src {allowedSources}; " +
                       $"connect-src {allowedSources} wss://*." + 
                       context.Request.Host.Host.Replace("www.", "") + "; " +
                       $"media-src {allowedSources}; " +
                       "frame-src 'none'; " +
                       "frame-ancestors 'none'; " +
                       "base-uri 'self'; " +
                       "form-action 'self'; " +
                       "worker-src 'self'; " +
                       "manifest-src 'self'; " +
                       "upgrade-insecure-requests";
                
            // In development, we might need to relax some policies
            if (!_environment.IsProduction())
            {
                // Allow eval in development for debugging tools
                cspValue = cspValue.Replace($"script-src {allowedSources}", $"script-src {allowedSources} 'unsafe-eval'");
                // Allow inline styles for dev tools
                cspValue = cspValue.Replace($"style-src {allowedSources}", $"style-src {allowedSources} 'unsafe-inline'");
            }
            
            context.Response.Headers["Content-Security-Policy"] = cspValue;
        }

        private bool IsHtmlResponse(HttpContext context)
        {
            return context.Response.Headers.ContentType.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase);
        }
    }

    // Extension method to make it easier to add the middleware
    public static class SecurityHeadersMiddlewareExtensions
    {
        public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<SecurityHeadersMiddleware>();
        }
    }
}