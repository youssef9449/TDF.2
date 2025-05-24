using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TDFAPI.Configuration;
using TDFAPI.Data;
using TDFShared.DTOs.Common;
using TDFShared.Models.Department;

namespace TDFAPI.Services
{
    public class LookupService : ILookupService
    {
        private readonly string _connectionString;
        private readonly ILogger<LookupService> _logger;
        private readonly ApplicationDbContext _dbContext;

        public LookupService(IConfiguration configuration, ILogger<LookupService> logger, ApplicationDbContext dbContext)
        {
            _connectionString = IniConfiguration.ConnectionString;
            _logger = logger;
            _dbContext = dbContext;
        }

        public async Task<List<LookupItem>> GetDepartmentsAsync()
        {
            List<LookupItem> departments = new List<LookupItem>();

            try
            {
                _logger.LogInformation("DIAGNOSTIC: GetDepartmentsAsync called in TDFAPI LookupService");

                // Check if we can connect to the database
                try
                {
                    var canConnect = await _dbContext.Database.CanConnectAsync();
                    _logger.LogInformation("DIAGNOSTIC: Database connection test: {CanConnect}", canConnect ? "SUCCESS" : "FAILED");

                    if (!canConnect)
                    {
                        _logger.LogWarning("DIAGNOSTIC: Cannot connect to database. Will return empty departments list.");
                        return departments;
                    }
                }
                catch (Exception dbEx)
                {
                    _logger.LogError(dbEx, "DIAGNOSTIC: Error testing database connection");
                }

                _logger.LogInformation("DIAGNOSTIC: Querying departments from Departments table using Entity Framework");

                // Get distinct department names from the Departments table
                // Use a direct SQL query to ensure we get results even if EF Core configuration isn't perfect
                var departmentNames = new List<string>();

                try
                {
                    // First try with EF Core
                    var departmentEntities = await _dbContext.Departments
                        .Select(d => d.Name)
                        .Distinct()
                        .OrderBy(d => d)
                        .ToListAsync();

                    departmentNames = departmentEntities;
                    _logger.LogInformation("DIAGNOSTIC: Found {Count} distinct department names in Departments table using EF Core",
                        departmentNames?.Count ?? 0);
                }
                catch (Exception efEx)
                {
                    _logger.LogError(efEx, "DIAGNOSTIC: Error querying departments with EF Core. Falling back to direct SQL.");

                    // Fallback to direct SQL query
                    using (var connection = _dbContext.Database.GetDbConnection())
                    {
                        await connection.OpenAsync();
                        using (var command = connection.CreateCommand())
                        {
                            command.CommandText = "SELECT DISTINCT [Department] FROM [Departments] WHERE [Department] IS NOT NULL ORDER BY [Department]";
                            using (var reader = await command.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    string deptName = reader.GetString(0);
                                    if (!string.IsNullOrEmpty(deptName))
                                    {
                                        departmentNames.Add(deptName);
                                    }
                                }
                            }
                        }
                    }

                    _logger.LogInformation("DIAGNOSTIC: Found {Count} distinct department names in Departments table using direct SQL",
                        departmentNames?.Count ?? 0);
                }

                if (departmentNames != null && departmentNames.Count > 0)
                {
                    foreach (var departmentName in departmentNames)
                    {
                        if (!string.IsNullOrEmpty(departmentName) && !departments.Any(d => d.Name == departmentName))
                        {
                            // Set both Id and Name
                            var dept = new LookupItem
                            {
                                Id = departmentName, // Use Name as Id
                                Name = departmentName
                            };
                            departments.Add(dept);
                            _logger.LogInformation("DIAGNOSTIC: Added department from Departments table: Name={Name}, Id={Id}", dept.Name, dept.Id);
                        }
                    }
                }

                _logger.LogInformation("DIAGNOSTIC: Returning {Count} departments", departments.Count);

                // Log the first few departments for debugging
                if (departments.Any())
                {
                    for (int i = 0; i < Math.Min(departments.Count, 5); i++)
                    {
                        var dept = departments[i];
                        _logger.LogInformation("DIAGNOSTIC: Department[{Index}] - Name: {Name}",
                            i, dept.Name);
                    }
                }

                return departments;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DIAGNOSTIC: Error retrieving departments from database: {Message}", ex.Message);
                throw;
            }
        }

        public async Task<List<string>> GetTitlesByDepartmentAsync(string department)
        {
            List<string> titles = new List<string>();

            try
            {
                _logger.LogInformation("Querying titles from Departments table for department: {Department}", department);

                // Get titles from the Departments table
                var departmentTitles = await _dbContext.Departments
                    .Where(d => d.Name == department && d.Title != null && d.Title != "")
                    .Select(d => d.Title)
                    .Distinct()
                    .OrderBy(t => t)
                    .ToListAsync();

                if (departmentTitles.Any())
                {
                    foreach (var title in departmentTitles)
                    {
                        if (!string.IsNullOrEmpty(title) && !titles.Contains(title))
                        {
                            titles.Add(title);
                            _logger.LogDebug("Added title from Departments table: {Title}", title);
                        }
                    }
                }
                else
                {
                    // If no titles found in Departments table, fall back to Users table
                    _logger.LogInformation("No titles found in Departments table. Falling back to Users table.");

                    var userTitles = await _dbContext.Users
                        .Where(u => u.Department == department && u.Title != null && u.Title != "")
                        .Select(u => u.Title)
                        .Distinct()
                        .OrderBy(t => t)
                        .ToListAsync();

                    foreach (var title in userTitles)
                    {
                        if (!string.IsNullOrEmpty(title) && !titles.Contains(title))
                        {
                            titles.Add(title);
                            _logger.LogDebug("Added title from Users table: {Title}", title);
                        }
                    }
                }

                titles.Sort();
                _logger.LogInformation("Returning {Count} titles for department {Department}", titles.Count, department);
                return titles;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving titles for department {Department}: {Error}", department, ex.Message);
                throw;
            }
        }

        public async Task<LookupResponseDto> GetAllLookupsAsync()
        {
            var response = new LookupResponseDto();

            try
            {
                // Get all departments
                response.Departments = await GetDepartmentsAsync();

                // Get titles for each department
                foreach (var department in response.Departments)
                {
                    if (!string.IsNullOrEmpty(department.Name))
                    {
                        response.TitlesByDepartment[department.Name] = await GetTitlesByDepartmentAsync(department.Name);
                    }
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all lookups");
                throw;
            }
        }

        public async Task<List<LookupItem>> GetLeaveTypesAsync()
        {
            List<LookupItem> leaveTypes = new List<LookupItem>();

            try
            {
                // This method still uses direct SQL since we don't have a LeaveTypes entity
                // In a future update, this could be converted to use Entity Framework
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string query = "SELECT LeaveTypeID, Name, Description FROM LeaveTypes ORDER BY SortOrder";

                    using (var command = new SqlCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var leaveType = new LookupItem
                                {
                                    Id = reader["LeaveTypeID"].ToString() ?? "",
                                    Value = reader["Name"].ToString() ?? "",
                                    Description = reader["Description"].ToString()
                                };

                                leaveTypes.Add(leaveType);
                            }
                        }
                    }
                }

                return leaveTypes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving leave types from database");
                throw;
            }
        }

        public async Task<List<LookupItem>> GetStatusCodesAsync()
        {
            List<LookupItem> statusCodes = new List<LookupItem>();

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string query = "SELECT StatusID, StatusName, Description FROM StatusCodes ORDER BY SortOrder";

                    using (var command = new SqlCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var statusCode = new LookupItem
                                {
                                    Id = reader["StatusID"].ToString() ?? "",
                                    Value = reader["StatusName"].ToString() ?? "",
                                    Description = reader["Description"].ToString()
                                };

                                statusCodes.Add(statusCode);
                            }
                        }
                    }
                }

                return statusCodes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving status codes from database");
                throw;
            }
        }

        public async Task<List<string>> GetRequestTypesAsync()
        {
            List<string> requestTypes = new List<string>();

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string query = "SELECT DISTINCT RequestType FROM RequestTypes ORDER BY SortOrder";

                    using (var command = new SqlCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                string requestType = reader["RequestType"].ToString() ?? "";
                                if (!string.IsNullOrEmpty(requestType) && !requestTypes.Contains(requestType))
                                {
                                    requestTypes.Add(requestType);
                                }
                            }
                        }
                    }

                    // If no request types are found in the database, fall back to the standard types
                    // but without using hardcoded values
                    if (requestTypes.Count == 0)
                    {
                        // Use the RequestTypes that are defined in the system
                        string fallbackQuery = @"
                            SELECT DISTINCT RequestType
                            FROM Requests
                            WHERE RequestType IS NOT NULL
                            ORDER BY RequestType";

                        using (var fallbackCommand = new SqlCommand(fallbackQuery, connection))
                        {
                            using (var reader = await fallbackCommand.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    string requestType = reader["RequestType"].ToString() ?? "";
                                    if (!string.IsNullOrEmpty(requestType) && !requestTypes.Contains(requestType))
                                    {
                                        requestTypes.Add(requestType);
                                    }
                                }
                            }
                        }

                        // Ensure External Assignment is included if it doesn't exist
                        if (!requestTypes.Contains("External Assignment"))
                        {
                            requestTypes.Add("External Assignment");
                        }
                    }
                }

                return requestTypes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving request types from database");
                throw;
            }
        }
    }
}