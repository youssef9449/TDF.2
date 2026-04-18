using System;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using TDFAPI.Configuration.Options;

namespace TDFAPI.Extensions.Startup
{
    /// <summary>
    /// Per-minute fixed-window rate limiters for the global pipeline plus
    /// named policies for auth / api / static endpoints.
    /// </summary>
    public static class RateLimitingExtensions
    {
        public static IServiceCollection AddTdfRateLimiting(this IServiceCollection services, RateLimitOptions rateLimit)
        {
            var globalLimit = rateLimit.GlobalLimitPerMinute;
            var authLimit = rateLimit.AuthLimitPerMinute;
            var apiLimit = rateLimit.ApiLimitPerMinute;
            var staticLimit = rateLimit.StaticLimitPerMinute;

            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            AutoReplenishment = true,
                            PermitLimit = globalLimit,
                            Window = TimeSpan.FromMinutes(1)
                        }));

                options.AddPolicy("auth", httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            AutoReplenishment = true,
                            PermitLimit = authLimit,
                            Window = TimeSpan.FromMinutes(1)
                        }));

                options.AddPolicy("api", httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            AutoReplenishment = true,
                            PermitLimit = apiLimit,
                            Window = TimeSpan.FromMinutes(1)
                        }));

                options.AddPolicy("static", httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            AutoReplenishment = true,
                            PermitLimit = staticLimit,
                            Window = TimeSpan.FromMinutes(1)
                        }));
            });

            return services;
        }
    }
}
