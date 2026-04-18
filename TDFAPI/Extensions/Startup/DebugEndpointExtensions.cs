using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using TDFAPI.Configuration.Options;
using TDFShared.Constants;

namespace TDFAPI.Extensions.Startup
{
    /// <summary>
    /// Development-only diagnostic endpoints: <c>/{ApiRoutes.Docs}</c>,
    /// <c>/debug/config</c>, and <c>/debug/database</c>. All debug endpoints
    /// reject non-loopback callers.
    /// </summary>
    public static class DebugEndpointExtensions
    {
        public static WebApplication MapTdfDevelopmentEndpoints(
            this WebApplication app,
            IReadOnlyCollection<string> serverUrls,
            StartupOptionsSnapshot options)
        {
            if (!app.Environment.IsDevelopment())
            {
                return app;
            }

            app.MapGet($"/{ApiRoutes.Docs}", () =>
            {
                var apiDocs = new
                {
                    Title = "TDF API Documentation",
                    Version = "v1.0",
                    Endpoints = new[]
                    {
                        new { Path = $"/{ApiRoutes.Auth.Login}", Method = "POST", Description = "User authentication" },
                        new { Path = $"/{ApiRoutes.Auth.RefreshToken}", Method = "POST", Description = "Refresh JWT token" },
                        new { Path = $"/{ApiRoutes.Users.Base}", Method = "GET", Description = "Get all users" },
                        new { Path = $"/{ApiRoutes.Messages.Base}", Method = "GET", Description = "Get messages" },
                    },
                    Authentication = "JWT Bearer Token"
                };

                return Results.Json(apiDocs);
            });

            app.MapGet("/debug/config", (HttpContext context) =>
            {
                if (!IsLoopback(context)) return Results.Forbid();

                var sanitizedConnectionString = Regex.Replace(
                    options.ConnectionString ?? string.Empty,
                    "Password=([^;]*)",
                    "Password=********",
                    RegexOptions.IgnoreCase);

                return Results.Json(new
                {
                    Environment = app.Environment.EnvironmentName,
                    ServerUrls = serverUrls.ToList(),
                    DatabaseConfigured = !string.IsNullOrEmpty(options.ConnectionString),
                    JwtConfigured = !string.IsNullOrEmpty(options.Jwt.Issuer) &&
                                   !string.IsNullOrEmpty(options.Jwt.Audience),
                    WebSocketsEnabled = true,
                    RateLimits = new
                    {
                        Global = options.RateLimit.GlobalLimitPerMinute,
                        Auth = options.RateLimit.AuthLimitPerMinute,
                        Api = options.RateLimit.ApiLimitPerMinute,
                        Static = options.RateLimit.StaticLimitPerMinute
                    },
                    System = new
                    {
                        OSVersion = Environment.OSVersion.ToString(),
                        ProcessorCount = Environment.ProcessorCount,
                        WorkingSet = Environment.WorkingSet / (1024 * 1024) + " MB",
                        ApplicationDirectory = AppDomain.CurrentDomain.BaseDirectory,
                        RuntimeVersion = Environment.Version.ToString()
                    },
                    Database = new
                    {
                        ConnectionString = sanitizedConnectionString,
                        Status = "Testing..."
                    }
                });
            });

            app.MapGet("/debug/database", async (HttpContext context) =>
            {
                if (!IsLoopback(context)) return Results.Forbid();

                try
                {
                    using var connection = new SqlConnection(options.ConnectionString);
                    await connection.OpenAsync();
                    return Results.Json(new
                    {
                        Status = "Connected",
                        ServerVersion = connection.ServerVersion,
                        Database = connection.Database,
                        ConnectionTime = DateTime.UtcNow
                    });
                }
                catch (Exception ex)
                {
                    return Results.Json(new
                    {
                        Status = "Failed",
                        Error = ex.Message,
                        InnerError = ex.InnerException?.Message,
                        Time = DateTime.UtcNow
                    });
                }
            });

            return app;
        }

        private static bool IsLoopback(HttpContext context)
        {
            if (context.Connection.RemoteIpAddress == null ||
                context.Connection.LocalIpAddress == null)
            {
                return false;
            }
            return context.Connection.RemoteIpAddress.Equals(context.Connection.LocalIpAddress)
                   || IPAddress.IsLoopback(context.Connection.RemoteIpAddress);
        }
    }
}
