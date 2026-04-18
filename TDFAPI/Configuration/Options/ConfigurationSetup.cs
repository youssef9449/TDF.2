using System.Collections.Generic;
using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TDFAPI.Configuration.Options
{
    /// <summary>
    /// Centralizes binding of strongly-typed option classes
    /// (<see cref="JwtOptions"/>, <see cref="CorsOptions"/>, <see cref="SecurityOptions"/>,
    /// <see cref="RateLimitOptions"/>, <see cref="WebSocketSettings"/>, <see cref="DatabaseOptions"/>)
    /// from the merged <see cref="IConfiguration"/>.
    ///
    /// To keep behavior compatible with the legacy <see cref="IniConfiguration"/> static,
    /// values previously read from <c>config.ini</c> are bridged into
    /// <see cref="WebApplicationBuilder.Configuration"/> as an in-memory source with
    /// higher precedence than <c>appsettings.json</c> but lower precedence than
    /// environment variables. This means operators who already drive the server from
    /// <c>config.ini</c> keep doing so, while new code binds cleanly through
    /// <see cref="Microsoft.Extensions.Options.IOptions{T}"/>.
    /// </summary>
    public static class ConfigurationSetup
    {
        /// <summary>
        /// Bridges values already loaded into <see cref="IniConfiguration"/> into the
        /// builder's <see cref="IConfiguration"/> so that <c>Configure&lt;T&gt;</c>
        /// binding sees them alongside <c>appsettings.json</c>.
        /// </summary>
        /// <remarks>
        /// Must be called AFTER <see cref="IniConfiguration.Initialize"/>.
        /// </remarks>
        public static void BridgeIniIntoConfiguration(WebApplicationBuilder builder)
        {
            var inv = CultureInfo.InvariantCulture;
            var values = new Dictionary<string, string?>
            {
                // Jwt — only Issuer/Audience come from the INI file; SecretKey is
                // read from the JWT_SECRET_KEY env var by IniConfiguration and should
                // also be available via the Jwt:SecretKey configuration key used by
                // the JWT middleware.
                ["Jwt:Issuer"] = IniConfiguration.JwtIssuer,
                ["Jwt:Audience"] = IniConfiguration.JwtAudience,
                ["Jwt:SecretKey"] = IniConfiguration.JwtSecretKey,
                ["Jwt:TokenValidityInMinutes"] = IniConfiguration.TokenValidityInMinutes.ToString(inv),
                ["Jwt:RefreshTokenValidityInDays"] = IniConfiguration.RefreshTokenValidityInDays.ToString(inv),

                // Database — expose the composed connection string under both the
                // ConnectionStrings:DefaultConnection slot (EF / health checks
                // convention) and under Database:ConnectionString.
                ["ConnectionStrings:DefaultConnection"] = IniConfiguration.ConnectionString,
                ["Database:ConnectionString"] = IniConfiguration.ConnectionString,

                // WebSockets
                ["WebSockets:TimeoutMinutes"] = IniConfiguration.GetWebSocketSetting("TimeoutMinutes", 30).ToString(inv),
                ["WebSockets:KeepAliveMinutes"] = IniConfiguration.GetWebSocketSetting("KeepAliveMinutes", 2.0).ToString(inv),
                ["WebSockets:MaxMessagesPerMinute"] = IniConfiguration.GetWebSocketSetting("MaxMessagesPerMinute", 120).ToString(inv),
                ["WebSockets:ReceiveBufferSize"] = IniConfiguration.GetWebSocketSetting("ReceiveBufferSize", 65536).ToString(inv),

                // Rate limiting
                ["RateLimiting:GlobalLimitPerMinute"] = IniConfiguration.GetRateLimitSetting("GlobalLimitPerMinute", 100).ToString(inv),
                ["RateLimiting:AuthLimitPerMinute"] = IniConfiguration.GetRateLimitSetting("AuthLimitPerMinute", 10).ToString(inv),
                ["RateLimiting:ApiLimitPerMinute"] = IniConfiguration.GetRateLimitSetting("ApiLimitPerMinute", 60).ToString(inv),
                ["RateLimiting:StaticLimitPerMinute"] = IniConfiguration.GetRateLimitSetting("StaticLimitPerMinute", 200).ToString(inv),

                // Security — top-level + nested password policy
                ["Security:MaxFailedLoginAttempts"] = IniConfiguration.GetSecuritySetting("MaxFailedLoginAttempts", 5).ToString(inv),
                ["Security:LockoutDurationMinutes"] = IniConfiguration.GetSecuritySetting("LockoutDurationMinutes", 15).ToString(inv),
            };

            // Password policy lives inside a nested dictionary on IniConfiguration.
            var passwordPolicy = IniConfiguration.GetSecuritySetting<Dictionary<string, object>>(
                "PasswordRequirements", new Dictionary<string, object>());
            if (passwordPolicy != null)
            {
                foreach (var kv in passwordPolicy)
                {
                    values[$"Security:PasswordRequirements:{kv.Key}"] = kv.Value?.ToString();
                }
            }

            // CORS — IniConfiguration stores origins as string lists; map them into
            // the index-keyed shape IConfiguration uses for array binding.
            for (int i = 0; i < IniConfiguration.AllowedOrigins.Count; i++)
            {
                values[$"Cors:AllowedOrigins:{i}"] = IniConfiguration.AllowedOrigins[i];
            }
            for (int i = 0; i < IniConfiguration.DevelopmentAllowedOrigins.Count; i++)
            {
                values[$"Cors:DevelopmentAllowedOrigins:{i}"] = IniConfiguration.DevelopmentAllowedOrigins[i];
            }

            // Drop empty values so appsettings.json fallbacks still win when the INI
            // file doesn't explicitly configure something.
            var nonEmpty = new Dictionary<string, string?>(values.Count);
            foreach (var kv in values)
            {
                if (!string.IsNullOrEmpty(kv.Value))
                {
                    nonEmpty[kv.Key] = kv.Value;
                }
            }

            builder.Configuration.AddInMemoryCollection(nonEmpty);
        }

        /// <summary>
        /// Registers all strongly-typed option classes with the DI container.
        /// Callers should invoke <see cref="BridgeIniIntoConfiguration"/> first.
        /// </summary>
        public static void AddTypedOptions(WebApplicationBuilder builder)
        {
            var config = builder.Configuration;

            builder.Services.Configure<JwtOptions>(config.GetSection(JwtOptions.SectionName));
            builder.Services.Configure<SecurityOptions>(config.GetSection(SecurityOptions.SectionName));
            builder.Services.Configure<RateLimitOptions>(config.GetSection(RateLimitOptions.SectionName));
            builder.Services.Configure<WebSocketSettings>(config.GetSection(WebSocketSettings.SectionName));
            builder.Services.Configure<DatabaseOptions>(options =>
            {
                config.GetSection(DatabaseOptions.SectionName).Bind(options);
                // Fall back to the conventional ConnectionStrings:DefaultConnection slot
                // if the Database section didn't supply an explicit ConnectionString.
                if (string.IsNullOrWhiteSpace(options.ConnectionString))
                {
                    options.ConnectionString = config.GetConnectionString("DefaultConnection");
                }
            });
            builder.Services.Configure<CorsOptions>(options =>
            {
                // Prefer a dedicated Cors section; fall back to the legacy top-level
                // AllowedOrigins / DevelopmentAllowedOrigins arrays used by the current
                // appsettings.json for back-compat.
                var corsSection = config.GetSection(CorsOptions.SectionName);
                if (corsSection.Exists())
                {
                    corsSection.Bind(options);
                }

                var legacyAllowed = config.GetSection("AllowedOrigins").Get<List<string>>();
                if (options.AllowedOrigins.Count == 0 && legacyAllowed != null)
                {
                    options.AllowedOrigins.AddRange(legacyAllowed);
                }

                var legacyDev = config.GetSection("DevelopmentAllowedOrigins").Get<List<string>>();
                if (options.DevelopmentAllowedOrigins.Count == 0 && legacyDev != null)
                {
                    options.DevelopmentAllowedOrigins.AddRange(legacyDev);
                }
            });
        }
    }
}
