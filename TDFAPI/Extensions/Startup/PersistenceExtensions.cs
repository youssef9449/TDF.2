using System;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TDFAPI.Configuration.Options;
using TDFAPI.Data;
using TDFAPI.Repositories;
using TDFAPI.Services;

namespace TDFAPI.Extensions.Startup
{
    /// <summary>
    /// Database connectivity registration: EF Core <see cref="ApplicationDbContext"/>,
    /// raw ADO.NET <see cref="SqlConnectionFactory"/>, and an optional
    /// pre-startup connectivity probe.
    /// </summary>
    public static class PersistenceExtensions
    {
        public static IServiceCollection AddTdfPersistence(this IServiceCollection services, string connectionString)
        {
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString));

            services.AddScoped(provider =>
            {
                var dbOptions = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
                return new SqlConnectionFactory(dbOptions.BuildConnectionString());
            });

            return services;
        }

        /// <summary>
        /// Best-effort synchronous connection probe. Failures are logged and
        /// swallowed so the API can still come up and advertise the failure
        /// via the health-check endpoint.
        /// </summary>
        public static void ProbeDatabaseConnection(string connectionString, ILogger logger)
        {
            try
            {
                logger.LogInformation("Testing database connection");
                using var connection = new SqlConnection(connectionString);
                connection.Open();
                logger.LogInformation("Database connection test successful");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Database connection test failed: {Message}", ex.Message);
            }
        }
    }
}
