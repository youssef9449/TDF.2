using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using TDFAPI.Configuration;
using TDFAPI.Exceptions;

namespace TDFAPI.Repositories
{
    /// <summary>
    /// Base repository using Dapper for performance-critical queries
    /// </summary>
    public class DapperRepository
    {
        private readonly string _connectionString;
        protected readonly ILogger _logger;

        public DapperRepository(ILogger logger)
        {
            _connectionString = IniConfiguration.ConnectionString;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Creates a new database connection
        /// </summary>
        /// <returns>An open SqlConnection</returns>
        protected async Task<IDbConnection> CreateConnectionAsync()
        {
            var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            return connection;
        }

        /// <summary>
        /// Executes a query and returns a list of results
        /// </summary>
        /// <typeparam name="T">The type of results to return</typeparam>
        /// <param name="sql">The SQL query</param>
        /// <param name="param">The parameters</param>
        /// <param name="transaction">Optional transaction</param>
        /// <param name="commandTimeout">Command timeout in seconds</param>
        /// <param name="commandType">Command type</param>
        /// <returns>A list of results</returns>
        protected async Task<IEnumerable<T>> QueryAsync<T>(
            string sql, 
            object? param = null, 
            IDbTransaction? transaction = null,
            int? commandTimeout = null,
            CommandType? commandType = null)
        {
            try
            {
                using var connection = await CreateConnectionAsync();
                return await connection.QueryAsync<T>(sql, param, transaction, commandTimeout, commandType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing query: {Sql}", sql);
                throw;
            }
        }

        /// <summary>
        /// Executes a query and returns a single result
        /// </summary>
        /// <typeparam name="T">The type of result to return</typeparam>
        /// <param name="sql">The SQL query</param>
        /// <param name="param">The parameters</param>
        /// <param name="transaction">Optional transaction</param>
        /// <param name="commandTimeout">Command timeout in seconds</param>
        /// <param name="commandType">Command type</param>
        /// <returns>A single result or default if no result</returns>
        protected async Task<T> QueryFirstOrDefaultAsync<T>(
            string sql, 
            object? param = null, 
            IDbTransaction? transaction = null,
            int? commandTimeout = null,
            CommandType? commandType = null)
        {
            try
            {
                using var connection = await CreateConnectionAsync();
                return await connection.QueryFirstOrDefaultAsync<T>(sql, param, transaction, commandTimeout, commandType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing query: {Sql}", sql);
                throw;
            }
        }

        /// <summary>
        /// Executes a query and returns a single result, throwing if no result exists
        /// </summary>
        /// <typeparam name="T">The type of result to return</typeparam>
        /// <param name="sql">The SQL query</param>
        /// <param name="param">The parameters</param>
        /// <param name="entityName">The name of the entity being queried</param>
        /// <param name="entityIdParamName">The name of the parameter containing the entity ID</param>
        /// <param name="transaction">Optional transaction</param>
        /// <param name="commandTimeout">Command timeout in seconds</param>
        /// <param name="commandType">Command type</param>
        /// <returns>A single result</returns>
        protected async Task<T> QueryFirstOrThrowAsync<T>(
            string sql, 
            object param, 
            string entityName,
            string entityIdParamName,
            IDbTransaction? transaction = null,
            int? commandTimeout = null,
            CommandType? commandType = null)
        {
            var result = await QueryFirstOrDefaultAsync<T>(sql, param, transaction, commandTimeout, commandType);
            
            if (result == null)
            {
                if (param is IDictionary<string, object> dictionary && dictionary.TryGetValue(entityIdParamName, out var entityId))
                {
                    throw new EntityNotFoundException(entityName, entityId?.ToString() ?? "unknown");
                }

                var paramType = param.GetType();
                var property = paramType.GetProperty(entityIdParamName);
                if (property != null)
                {
                    var entityIdValue = property.GetValue(param);
                    var entityIdString = entityIdValue?.ToString() ?? "unknown";
                    throw new EntityNotFoundException(entityName, entityIdString);
                }

                throw new EntityNotFoundException(entityName, "unknown");
            }
            
            return result;
        }

        /// <summary>
        /// Executes a query and returns a paged result
        /// </summary>
        /// <typeparam name="T">The type of results to return</typeparam>
        /// <param name="sql">The SQL query (should include ORDER BY)</param>
        /// <param name="param">The parameters</param>
        /// <param name="pageNumber">The page number (1-based)</param>
        /// <param name="pageSize">The page size</param>
        /// <param name="transaction">Optional transaction</param>
        /// <param name="commandTimeout">Command timeout in seconds</param>
        /// <returns>A tuple containing the items and total count</returns>
        protected async Task<(IEnumerable<T> Items, int TotalCount)> QueryPagedAsync<T>(
            string sql, 
            object param,
            int pageNumber,
            int pageSize,
            IDbTransaction? transaction = null,
            int? commandTimeout = null)
        {
            try
            {
                var offset = (pageNumber - 1) * pageSize;
                var pagedSql = $@"
                    SELECT COUNT(*) FROM ({sql}) AS CountQuery;
                    {sql}
                    ORDER BY (SELECT NULL)
                    OFFSET {offset} ROWS
                    FETCH NEXT {pageSize} ROWS ONLY;";

                using var connection = await CreateConnectionAsync();
                using var multi = await connection.QueryMultipleAsync(pagedSql, param, transaction, commandTimeout);
                
                var count = await multi.ReadFirstAsync<int>();
                var items = await multi.ReadAsync<T>();
                
                return (items, count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing paged query: {Sql}", sql);
                throw;
            }
        }

        /// <summary>
        /// Executes a non-query command
        /// </summary>
        /// <param name="sql">The SQL command</param>
        /// <param name="param">The parameters</param>
        /// <param name="transaction">Optional transaction</param>
        /// <param name="commandTimeout">Command timeout in seconds</param>
        /// <param name="commandType">Command type</param>
        /// <returns>The number of affected rows</returns>
        protected async Task<int> ExecuteAsync(
            string sql, 
            object? param = null, 
            IDbTransaction? transaction = null,
            int? commandTimeout = null,
            CommandType? commandType = null)
        {
            try
            {
                using var connection = await CreateConnectionAsync();
                return await connection.ExecuteAsync(sql, param, transaction, commandTimeout, commandType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing command: {Sql}", sql);
                throw;
            }
        }

        /// <summary>
        /// Executes a scalar command
        /// </summary>
        /// <typeparam name="T">The type of scalar result</typeparam>
        /// <param name="sql">The SQL command</param>
        /// <param name="param">The parameters</param>
        /// <param name="transaction">Optional transaction</param>
        /// <param name="commandTimeout">Command timeout in seconds</param>
        /// <param name="commandType">Command type</param>
        /// <returns>The scalar result</returns>
        protected async Task<T> ExecuteScalarAsync<T>(
            string sql, 
            object? param = null, 
            IDbTransaction? transaction = null,
            int? commandTimeout = null,
            CommandType? commandType = null)
        {
            try
            {
                using var connection = await CreateConnectionAsync();
                return await connection.ExecuteScalarAsync<T>(sql, param, transaction, commandTimeout, commandType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing scalar command: {Sql}", sql);
                throw;
            }
        }
    }
} 