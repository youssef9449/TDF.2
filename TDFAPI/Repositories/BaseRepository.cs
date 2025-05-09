using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using TDFAPI.Services;

namespace TDFAPI.Repositories
{
    public abstract class BaseRepository
    {
        protected readonly SqlConnectionFactory _connectionFactory;
        protected readonly ILogger _logger;

        protected BaseRepository(SqlConnectionFactory connectionFactory, ILogger logger)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected async Task<T> ExecuteScalarAsync<T>(string sql, object parameters = null)
        {
            try
            {
                using var connection = _connectionFactory.CreateConnection();
                await connection.OpenAsync();
                using var command = CreateCommand(connection, sql, parameters);
                var result = await command.ExecuteScalarAsync();
                return (T)Convert.ChangeType(result, typeof(T));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing scalar query: {Sql}", sql);
                throw;
            }
        }

        // Add other common methods like ExecuteNonQueryAsync, QueryAsync, etc.
        
        private SqlCommand CreateCommand(SqlConnection connection, string sql, object parameters)
        {
            var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandType = CommandType.Text;
            
            if (parameters != null)
            {
                // Add parameters from anonymous object
                foreach (var prop in parameters.GetType().GetProperties())
                {
                    var value = prop.GetValue(parameters) ?? DBNull.Value;
                    command.Parameters.AddWithValue($"@{prop.Name}", value);
                }
            }
            
            return command;
        }
    }
}