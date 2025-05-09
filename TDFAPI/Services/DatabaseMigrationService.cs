using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using TDFAPI.Configuration;

namespace TDFAPI.Services
{
    public class DatabaseMigrationService : IHostedService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<DatabaseMigrationService> _logger;
        private readonly IHostEnvironment _environment;
        
        public DatabaseMigrationService(
            IConfiguration configuration, 
            ILogger<DatabaseMigrationService> logger,
            IHostEnvironment environment)
        {
            _configuration = configuration;
            _logger = logger;
            _environment = environment;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Try to get connection string from IniConfiguration first
                string connectionString = null;
                
                try
                {
                    connectionString = Configuration.IniConfiguration.ConnectionString;
                    _logger.LogInformation("Using connection string from IniConfiguration (config.ini)");
                }
                catch (Exception iniEx)
                {
                    _logger.LogWarning(iniEx, "Failed to get connection string from IniConfiguration, falling back to appsettings.json");
                }
                
                // Fall back to appsettings.json if needed
                if (string.IsNullOrEmpty(connectionString))
                {
                    connectionString = _configuration.GetConnectionString("DefaultConnection");
                    if (!string.IsNullOrEmpty(connectionString))
                    {
                        _logger.LogInformation("Using connection string from appsettings.json");
                    }
                }
                
                if (string.IsNullOrEmpty(connectionString))
                {
                    throw new InvalidOperationException("Database connection string not found in any configuration source");
                }
                
                // Log a sanitized version of the connection string for debugging
                var sanitizedConnectionString = connectionString;
                if (sanitizedConnectionString.Contains("Password="))
                {
                    sanitizedConnectionString = System.Text.RegularExpressions.Regex.Replace(
                        sanitizedConnectionString, "Password=([^;]*)", "Password=*****");
                }
                _logger.LogInformation("Database connection string: {ConnectionString}", sanitizedConnectionString);
                
                string migrationsPath = Path.Combine(_environment.ContentRootPath, "Data", "Migrations");
                
                // Check if migrations directory exists
                if (!Directory.Exists(migrationsPath))
                {
                    _logger.LogWarning("Migrations directory not found at {Path}", migrationsPath);
                    return;
                }
                
                var migrationFiles = Directory.GetFiles(migrationsPath, "*.sql");
                if (migrationFiles.Length == 0)
                {
                    _logger.LogInformation("No migration files found in {Path}", migrationsPath);
                    return;
                }
                
                // Order migration files by numeric prefix for proper sequencing
                Array.Sort(migrationFiles, (a, b) => 
                {
                    string fileNameA = Path.GetFileName(a);
                    string fileNameB = Path.GetFileName(b);
                    
                    // Extract numeric prefix if present (e.g., "001_", "2_")
                    var prefixRegex = new Regex(@"^(\d+)_");
                    var matchA = prefixRegex.Match(fileNameA);
                    var matchB = prefixRegex.Match(fileNameB);
                    
                    if (matchA.Success && matchB.Success)
                    {
                        // If both have numeric prefixes, compare them as integers
                        if (int.TryParse(matchA.Groups[1].Value, out int numA) && 
                            int.TryParse(matchB.Groups[1].Value, out int numB))
                        {
                            return numA.CompareTo(numB);
                        }
                    }
                    
                    // Fall back to string comparison if no numeric prefix or parsing fails
                    return string.Compare(fileNameA, fileNameB, StringComparison.Ordinal);
                });
                
                _logger.LogInformation("Found {Count} migration files to process in order: {Files}", 
                    migrationFiles.Length, string.Join(", ", migrationFiles.Select(Path.GetFileName)));
                
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync(cancellationToken);
                
                // Create migrations tracking table if it doesn't exist
                await EnsureMigrationHistoryTableExistsAsync(connection, cancellationToken);
                
                // Run each migration that hasn't been applied yet
                foreach (var migrationFile in migrationFiles)
                {
                    string fileName = Path.GetFileName(migrationFile);
                    
                    // Check if migration has already been applied
                    if (await HasMigrationBeenAppliedAsync(connection, fileName, cancellationToken))
                    {
                        _logger.LogDebug("Migration {FileName} has already been applied, skipping", fileName);
                        continue;
                    }
                    
                    _logger.LogInformation("Applying migration: {FileName}", fileName);
                    
                    // Read and execute the migration script
                    string scriptContent = await File.ReadAllTextAsync(migrationFile, cancellationToken);
                    
                    try
                    {
                        // Begin a transaction for this migration
                        using var transaction = connection.BeginTransaction();
                        
                        try
                        {
                            // Split the script by GO statements if present (for SQL Server)
                            var scriptBatches = Regex.Split(scriptContent, @"^\s*GO\s*$", 
                                RegexOptions.Multiline | RegexOptions.IgnoreCase)
                                .Where(batch => !string.IsNullOrWhiteSpace(batch))
                                .ToList();
                            
                            _logger.LogInformation("Executing migration {FileName} with {Count} batch(es)", fileName, scriptBatches.Count);
                            
                            foreach (var batch in scriptBatches)
                            {
                                try
                                {
                                    using var command = new SqlCommand(batch, connection, transaction);
                                    command.CommandTimeout = 60; // Set a longer timeout for migrations
                                    await command.ExecuteNonQueryAsync(cancellationToken);
                                }
                                catch (SqlException sqlEx)
                                {
                                    _logger.LogError(sqlEx, "SQL error in migration {FileName}, batch: {Batch}", fileName, batch);
                                    throw;
                                }
                            }
                            
                            // Record successful migration within the same transaction
                            await RecordMigrationAsync(connection, fileName, transaction, cancellationToken);
                            
                            // Commit the transaction
                            transaction.Commit();
                            _logger.LogInformation("Successfully applied migration: {FileName}", fileName);
                        }
                        catch (Exception ex)
                        {
                            // Roll back the transaction on any error
                            _logger.LogError(ex, "Error executing migration {FileName}. Rolling back transaction. Error: {ErrorMessage}", 
                                fileName, ex.Message);
                            transaction.Rollback();
                            
                            // Add more detailed error information, including SQL Server error codes for diagnosis
                            if (ex is SqlException sqlEx)
                            {
                                _logger.LogError("SQL Server Error: Code={ErrorCode}, State={State}, Error={ErrorMessage}",
                                    sqlEx.Number, sqlEx.State, sqlEx.Message);
                                
                                foreach (SqlError err in sqlEx.Errors)
                                {
                                    _logger.LogError("  - Detail: Server={Server}, Procedure={Procedure}, Line={LineNumber}, Error={Message}",
                                        err.Server, err.Procedure, err.LineNumber, err.Message);
                                }
                            }
                            
                            throw new Exception($"Error in migration {fileName}. Transaction rolled back.", ex);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error applying migration {FileName}: {Message}", 
                            fileName, ex.Message);
                        
                        // Only throw in development to prevent application startup failure in production
                        if (_environment.IsDevelopment())
                        {
                            throw;
                        }
                    }
                }
                
                _logger.LogInformation("Database migrations completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running database migrations: {Message}", ex.Message);
                
                // Only throw in development to prevent application startup failure in production
                if (_environment.IsDevelopment())
                {
                    throw;
                }
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
        
        private async Task EnsureMigrationHistoryTableExistsAsync(SqlConnection connection, CancellationToken cancellationToken)
        {
            try
            {
                // First check if table exists to avoid errors
                string checkTableSql = @"
                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MigrationHistory')
                    SELECT 0 AS TableExists
                    ELSE 
                    SELECT 1 AS TableExists";
                    
                using (var checkCommand = new SqlCommand(checkTableSql, connection))
                {
                    var tableExists = (int)await checkCommand.ExecuteScalarAsync(cancellationToken) == 1;
                    
                    if (!tableExists)
                    {
                        // Create table with transaction
                        using var transaction = connection.BeginTransaction();
                        try
                        {
                            const string createTableSql = @"
                                CREATE TABLE MigrationHistory (
                                    Id INT IDENTITY(1,1) PRIMARY KEY,
                                    MigrationName NVARCHAR(255) NOT NULL,
                                    AppliedOn DATETIME2 NOT NULL
                                );
                                
                                CREATE UNIQUE INDEX IX_MigrationHistory_MigrationName 
                                ON MigrationHistory(MigrationName);";
                                
                            using var command = new SqlCommand(createTableSql, connection, transaction);
                            await command.ExecuteNonQueryAsync(cancellationToken);
                            
                            transaction.Commit();
                            _logger.LogInformation("Created MigrationHistory table");
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            _logger.LogError(ex, "Error creating migration history table: {Message}", ex.Message);
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for migration history table: {Message}", ex.Message);
                throw;
            }
        }
        
        private async Task<bool> HasMigrationBeenAppliedAsync(SqlConnection connection, string migrationName, CancellationToken cancellationToken)
        {
            try
            {
                // First check if MigrationHistory table exists
                string checkTableSql = @"
                    IF EXISTS (SELECT * FROM sys.tables WHERE name = 'MigrationHistory')
                    SELECT 1 AS TableExists
                    ELSE 
                    SELECT 0 AS TableExists";
                    
                using (var checkCommand = new SqlCommand(checkTableSql, connection))
                {
                    var tableExists = (int)await checkCommand.ExecuteScalarAsync(cancellationToken) == 1;
                    
                    if (!tableExists)
                    {
                        return false; // Table doesn't exist, so migration hasn't been applied
                    }
                }
                
                // Check if migration has been applied
                const string sql = @"
                    SELECT COUNT(*) FROM MigrationHistory 
                    WHERE MigrationName = @MigrationName";
                    
                using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@MigrationName", migrationName);
                
                int count = (int)await command.ExecuteScalarAsync(cancellationToken);
                return count > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if migration has been applied: {Message}", ex.Message);
                return false; // Assume not applied on error to be safe
            }
        }
        
        private async Task RecordMigrationAsync(SqlConnection connection, string migrationName, SqlTransaction transaction, CancellationToken cancellationToken)
        {
            const string sql = @"
                INSERT INTO MigrationHistory (MigrationName, AppliedOn)
                VALUES (@MigrationName, @AppliedOn)";
                
            using var command = new SqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("@MigrationName", migrationName);
            command.Parameters.AddWithValue("@AppliedOn", DateTime.UtcNow);
            
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
} 