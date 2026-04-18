using System;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TDFShared.Constants;

namespace TDFAPI.Extensions.Startup
{
    /// <summary>
    /// Health-check registration and endpoint mapping. Reports both
    /// self-liveness and database connectivity in a single JSON payload.
    /// </summary>
    public static class HealthCheckStartupExtensions
    {
        public static IServiceCollection AddTdfHealthChecks(this IServiceCollection services, string connectionString)
        {
            services.AddHealthChecks()
                .AddCheck("self", () => HealthCheckResult.Healthy())
                .AddSqlServer(
                    connectionString,
                    name: "database",
                    tags: new[] { "db", "sql", "sqlserver" });

            return services;
        }

        public static WebApplication MapTdfHealthChecks(this WebApplication app)
        {
            app.MapHealthChecks($"/{ApiRoutes.Health.GetDefault}", new HealthCheckOptions
            {
                ResultStatusCodes =
                {
                    [HealthStatus.Healthy] = StatusCodes.Status200OK,
                    [HealthStatus.Degraded] = StatusCodes.Status200OK,
                    [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
                },
                ResponseWriter = async (context, report) =>
                {
                    context.Response.ContentType = "application/json";

                    var response = new
                    {
                        Status = report.Status.ToString(),
                        Duration = report.TotalDuration,
                        Timestamp = DateTime.UtcNow,
                        Environment = app.Environment.EnvironmentName,
                        Info = report.Entries.Select(e => new
                        {
                            Key = e.Key,
                            Status = e.Value.Status.ToString(),
                            Description = e.Value.Description,
                            Duration = e.Value.Duration,
                            Exception = e.Value.Exception?.Message,
                            Data = e.Value.Data
                        })
                    };

                    await context.Response.WriteAsync(TDFShared.Helpers.JsonSerializationHelper.SerializePretty(response));
                }
            });

            return app;
        }
    }
}
