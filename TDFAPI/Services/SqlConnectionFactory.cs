using Microsoft.Data.SqlClient;
using Polly;
using Polly.Retry;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using TDFAPI.Utilities;

namespace TDFAPI.Services
{
    public class SqlConnectionFactory
    {
        private readonly string _connectionString;
        private readonly Polly.Retry.AsyncRetryPolicy _retryPolicy;
        private static int _totalConnections = 0;
        private static int _activeConnections = 0;
        private static readonly object _connectionLock = new object();
        private static DateTime _lastConnectionWarningTime = DateTime.MinValue;
        private static readonly TimeSpan _connectionWarningThrottle = TimeSpan.FromMinutes(5);
        
        // Maximum number of concurrent connections to allow
        private const int MAX_CONCURRENT_CONNECTIONS = 100;
        
        // Regex to validate SQL inputs against injection attacks
        private static readonly Regex _sqlInjectionValidationRegex = new Regex(
            @";\s*(?:DROP|DELETE|UPDATE|INSERT|ALTER|EXEC|EXECUTE|DECLARE|CREATE)\s",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        
        public SqlConnectionFactory(string connectionString)
        {
            // Modify connection string to ensure best practices
            var connectionStringBuilder = new SqlConnectionStringBuilder(connectionString ?? 
                throw new ArgumentNullException(nameof(connectionString)));
                
            // Set optimal connection pooling parameters if not already specified
            if (!connectionString.Contains("Max Pool Size"))
                connectionStringBuilder.MaxPoolSize = 200;
            if (!connectionString.Contains("Min Pool Size"))
                connectionStringBuilder.MinPoolSize = 10;
            if (!connectionString.Contains("Connect Timeout"))
                connectionStringBuilder.ConnectTimeout = 30; // 30 seconds
            connectionStringBuilder.ApplicationName = "TDFAPI"; // Helps with tracking in SQL Server
            
            // Don't override TrustServerCertificate if it's already set in the connection string
            // This ensures we respect the config.ini setting
            if (!connectionString.Contains("TrustServerCertificate="))
            {
                connectionStringBuilder.TrustServerCertificate = true;
            }
            
            connectionStringBuilder.ConnectRetryCount = 3; // Let client retry before our policy
            connectionStringBuilder.ConnectRetryInterval = 5; // 5 seconds between retries
            
            _connectionString = connectionStringBuilder.ConnectionString;
                
            // Define a retry policy for transient SQL exceptions
            _retryPolicy = Policy
                .Handle<SqlException>(IsTransientSqlException)
                .Or<TimeoutException>()
                .Or<InvalidOperationException>(ex => ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase))
                .WaitAndRetryAsync(
                    retryCount: 5,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (exception, timeSpan, retryCount, context) =>
                    {
                        var logger = context["logger"] as ILogger<SqlConnectionFactory>;
                        logger?.LogWarning(
                            exception,
                            "Retrying database connection after {RetryCount} attempt(s). Delay: {Delay}ms. Error: {Message}",
                            retryCount,
                            timeSpan.TotalMilliseconds,
                            exception.Message);
                    });
        }
        
        public string GetConnectionString()
        {
            return new SqlConnectionStringBuilder(_connectionString)
            {
                Password = "******" // Redact password for logging
            }.ConnectionString;
        }
        
        public async Task<SqlConnection> CreateConnectionAsync(ILogger logger)
        {
            // Check for too many concurrent connections
            lock (_connectionLock)
            {
                _totalConnections++;
                _activeConnections++;
                
                if (_activeConnections > MAX_CONCURRENT_CONNECTIONS)
                {
                    // Only log warning periodically to avoid log spam
                    if (DateTime.UtcNow - _lastConnectionWarningTime > _connectionWarningThrottle)
                    {
                        logger.LogWarning(
                            "Too many concurrent database connections: {ActiveConnections}/{TotalConnections}",
                            _activeConnections, _totalConnections);
                        _lastConnectionWarningTime = DateTime.UtcNow;
                    }
                }
            }
            
            var connection = new SqlConnection(_connectionString);
            
            // Add connection tracking
            connection.Disposed += (sender, args) => 
            {
                lock (_connectionLock)
                {
                    _activeConnections--;
                }
            };
            
            // Create retry context with logger
            var context = new Context { ["logger"] = logger };
            
            try
            {
                await _retryPolicy.ExecuteAsync(async (ctx) =>
                {
                    try
                    {
                        await connection.OpenAsync();
                    }
                    catch (Exception)
                    {
                        connection.Dispose();
                        
                        lock (_connectionLock)
                        {
                            _activeConnections--;
                        }
                        
                        throw;
                    }
                }, context);
                
                return connection;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to establish database connection after multiple retry attempts");
                
                // Log to INI file in logs directory
                LoggingUtils.LogDatabaseError(ex, GetConnectionString(), logger);
                
                lock (_connectionLock)
                {
                    _activeConnections--;
                }
                
                throw new InvalidOperationException("Failed to connect to the database after multiple attempts. Please try again later.", ex);
            }
        }
        
        public SqlConnection CreateConnection()
        {
            lock (_connectionLock)
            {
                _totalConnections++;
                _activeConnections++;
            }
            
            var connection = new SqlConnection(_connectionString);
            
            // Add connection tracking
            connection.Disposed += (sender, args) => 
            {
                lock (_connectionLock)
                {
                    _activeConnections--;
                }
            };
            
            return connection;
        }
        
        // Check if a SQL string might contain injection attempts
        public static bool MightContainSqlInjection(string sql)
        {
            if (string.IsNullOrEmpty(sql))
                return false;
                
            return _sqlInjectionValidationRegex.IsMatch(sql);
        }
        
        // Helper method to identify transient SQL exceptions that are eligible for retry
        private static bool IsTransientSqlException(SqlException ex)
        {
            // These error codes represent transient errors that can be retried
            // For a complete list, see: https://docs.microsoft.com/en-us/azure/sql-database/sql-database-develop-error-messages
            int[] transientErrorNumbers =
            {
                4060, // Cannot open database
                40197, // The service has encountered an error processing your request
                40501, // The service is currently busy
                40613, // Database is unavailable
                49918, // Cannot process request
                49919, // Cannot process create or update request
                49920, // Service is busy
                4221,  // Login failed
                1205,  // Deadlock victim
                1204,  // Lock limit exceeded
                1222,  // Lock request timeout
                20,    // The instance of SQL Server is not available
                233,   // A connection was successfully established, but an error occurred during login negotiation
                64,    // A connection was successfully established, but an error occurred during login
                53,    // The network path was not found
                10053, // A transport-level error occurred
                10054, // An existing connection was forcibly closed by the remote host
                10060, // A connection attempt failed because the connected party did not properly respond
                -1     // General network error
            };
            
            return transientErrorNumbers.Contains(ex.Number);
        }
    }
}