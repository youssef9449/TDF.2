using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using TDFAPI.Configuration.Options;

namespace TDFAPI.Extensions.Startup
{
    /// <summary>
    /// Aggregates the strongly-typed configuration snapshots that are needed by
    /// bootstrap-time code (configuration callbacks, minimal APIs, pre-DI
    /// validation). Services resolved from the DI container should inject
    /// <c>IOptions&lt;T&gt;</c> directly instead of consuming this snapshot.
    /// </summary>
    public class StartupOptionsSnapshot
    {
        public JwtOptions Jwt { get; init; } = new();
        public RateLimitOptions RateLimit { get; init; } = new();
        public CorsOptions Cors { get; init; } = new();
        public WebSocketSettings WebSockets { get; init; } = new();
        public DatabaseOptions Database { get; init; } = new();

        /// <summary>Pre-built effective SQL connection string, taking any override into account.</summary>
        public string ConnectionString { get; init; } = string.Empty;

        /// <summary>
        /// Resolve a snapshot from the current <see cref="IConfiguration"/>.
        /// Applies the same back-compat fall-backs the old Program.cs did for
        /// CORS (legacy root-level arrays) and the connection string
        /// (<c>ConnectionStrings:DefaultConnection</c>).
        /// </summary>
        public static StartupOptionsSnapshot FromConfiguration(IConfiguration config)
        {
            var jwt = config.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
            var rateLimit = config.GetSection(RateLimitOptions.SectionName).Get<RateLimitOptions>() ?? new RateLimitOptions();
            var webSockets = config.GetSection(WebSocketSettings.SectionName).Get<WebSocketSettings>() ?? new WebSocketSettings();

            var cors = config.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new CorsOptions();
            if (cors.AllowedOrigins.Count == 0)
            {
                var legacy = config.GetSection("AllowedOrigins").Get<List<string>>();
                if (legacy != null) cors.AllowedOrigins.AddRange(legacy);
            }
            if (cors.DevelopmentAllowedOrigins.Count == 0)
            {
                var legacyDev = config.GetSection("DevelopmentAllowedOrigins").Get<List<string>>();
                if (legacyDev != null) cors.DevelopmentAllowedOrigins.AddRange(legacyDev);
            }

            var database = config.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();
            if (string.IsNullOrWhiteSpace(database.ConnectionString))
            {
                database.ConnectionString = config.GetConnectionString("DefaultConnection");
            }

            return new StartupOptionsSnapshot
            {
                Jwt = jwt,
                RateLimit = rateLimit,
                Cors = cors,
                WebSockets = webSockets,
                Database = database,
                ConnectionString = database.BuildConnectionString()
            };
        }
    }
}
