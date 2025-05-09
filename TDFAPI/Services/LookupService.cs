using Microsoft.Data.SqlClient;
using TDFAPI.Configuration;
using TDFShared.DTOs.Common;

namespace TDFAPI.Services
{
    public class LookupService : ILookupService
    {
        private readonly string _connectionString;
        private readonly ILogger<LookupService> _logger;
        
        public LookupService(IConfiguration configuration, ILogger<LookupService> logger)
        {
            _connectionString = IniConfiguration.ConnectionString;
            _logger = logger;
        }
        
        public async Task<List<LookupItem>> GetDepartmentsAsync()
        {
            List<LookupItem> departments = new List<LookupItem>();
            
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    
                    string query = "SELECT DISTINCT Department FROM Departments ORDER BY Department";
                    
                    using (var command = new SqlCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                string department = reader["Department"].ToString() ?? "";
                                if (!string.IsNullOrEmpty(department) && !departments.Any(d => d.Value == department))
                                {
                                    // Use department name for both value and label if no ID column exists
                                    departments.Add(new LookupItem(department, department));
                                }
                            }
                        }
                    }
                }
                
                return departments;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving departments from database");
                throw;
            }
        }
        
        public async Task<List<string>> GetTitlesByDepartmentAsync(string department)
        {
            List<string> titles = new List<string>();
            
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    
                    string query = "SELECT DISTINCT Title FROM Departments WHERE Department = @Department";
                    
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Department", department);
                        
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                string title = reader["Title"].ToString() ?? "";
                                if (!string.IsNullOrEmpty(title) && !titles.Contains(title))
                                {
                                    titles.Add(title);
                                }
                            }
                        }
                    }
                }
                
                titles.Sort();
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
                    response.TitlesByDepartment[department.Value] = await GetTitlesByDepartmentAsync(department.Value);
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