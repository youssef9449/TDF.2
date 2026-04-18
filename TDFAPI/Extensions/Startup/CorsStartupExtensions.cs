using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TDFAPI.Configuration.Options;

namespace TDFAPI.Extensions.Startup
{
    /// <summary>
    /// CORS registration. The policy is always named <c>CorsPolicy</c>; the
    /// set of allowed origins varies per environment with sensible defaults
    /// for local development.
    /// </summary>
    public static class CorsStartupExtensions
    {
        public static IServiceCollection AddTdfCors(
            this IServiceCollection services,
            IHostEnvironment env,
            CorsOptions corsOptions,
            ILogger logger)
        {
            if (env.IsDevelopment())
            {
                var developmentOrigins = corsOptions.DevelopmentAllowedOrigins;

                if (developmentOrigins.Count == 0)
                {
                    developmentOrigins.AddRange(new[]
                    {
                        "http://localhost:3000",
                        "http://localhost:8080",
                        "http://localhost:4200",
                        "https://localhost:5001",
                        "http://localhost:5000",
                        "http://localhost:5173",
                        "http://127.0.0.1:5173",
                        "http://localhost:8000"
                    });
                }

                services.AddCors(options =>
                {
                    options.AddPolicy("CorsPolicy", policy =>
                    {
                        policy.WithOrigins(developmentOrigins.ToArray())
                              .AllowAnyMethod()
                              .AllowAnyHeader()
                              .AllowCredentials();
                    });
                });

                logger.LogWarning(
                    "CORS is configured for development environment with origins: {Origins}",
                    string.Join(", ", developmentOrigins));
            }
            else
            {
                var allowedOrigins = corsOptions.AllowedOrigins;

                if (allowedOrigins.Count == 0)
                {
                    throw new InvalidOperationException(
                        "No allowed origins configured for CORS in production environment. " +
                        "Please configure AllowedOrigins in config.ini or the Cors section of appsettings.json.");
                }

                services.AddCors(options =>
                {
                    options.AddPolicy("CorsPolicy", policy =>
                    {
                        policy.WithOrigins(allowedOrigins.ToArray())
                              .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
                              .WithHeaders("Authorization", "Content-Type")
                              .AllowCredentials()
                              .SetIsOriginAllowedToAllowWildcardSubdomains();
                    });
                });

                logger.LogInformation(
                    "CORS configured with the following origins: {Origins}",
                    string.Join(", ", allowedOrigins));
            }

            return services;
        }
    }
}
