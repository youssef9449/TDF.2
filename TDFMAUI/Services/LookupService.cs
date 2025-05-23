using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using Microsoft.Maui.ApplicationModel;
using TDFShared.Constants;
using TDFShared.DTOs.Common;
using Microsoft.Extensions.Logging;

namespace TDFMAUI.Services
{
    public class LookupService : ILookupService, IDisposable
    {
        private readonly ApiService _apiService;
        private readonly ILogger<LookupService> _logger;
        private List<LookupItem> _departments;
        private bool _disposed = false;

        public LookupService(ApiService apiService, ILogger<LookupService> logger)
        {
            _logger?.LogInformation("LookupService constructor started.");
            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _departments = new List<LookupItem>();
            _logger?.LogInformation("LookupService constructor finished.");
        }

        public async Task<List<LookupItem>> GetDepartmentsAsync()
        {
            try
            {
                _logger.LogInformation("DIAGNOSTIC: GetDepartmentsAsync called in TDFMAUI LookupService");

                // Always reload departments to ensure fresh data
                await LoadDataAsync();

                string debugInfo = $"Returning {_departments?.Count ?? 0} departments";
                if (_departments != null && _departments.Count > 0)
                {
                    debugInfo += $"\nFirst department: {_departments[0].Id} - {_departments[0].Name}";
                    _logger.LogInformation("DIAGNOSTIC: First department in result: Id={Id}, Name={Name}, Value={Value}",
                        _departments[0].Id, _departments[0].Name, _departments[0].Value);
                }

                System.Diagnostics.Debug.WriteLine($"[LookupService CONSOLE] Departments Result: {debugInfo}");
                _logger.LogInformation("DIAGNOSTIC: GetDepartmentsAsync returning {Count} departments", _departments?.Count ?? 0);

                // Make a defensive copy to avoid potential issues with the shared list
                return _departments != null ? new List<LookupItem>(_departments) : new List<LookupItem>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LookupService CONSOLE Error] Error in GetDepartmentsAsync: {ex.Message}");
                _logger.LogError(ex, "DIAGNOSTIC: Error in GetDepartmentsAsync: {Message}", ex.Message);
                throw; // Re-throw to let calling code handle the error
            }
        }

        public async Task<List<string>> GetTitlesForDepartmentAsync(string departmentId)
        {
            if (string.IsNullOrEmpty(departmentId))
            {
                _logger.LogWarning("GetTitlesForDepartmentAsync called with null or empty department ID.");
                return new List<string>();
            }

            try
            {
                _logger.LogInformation("DIAGNOSTIC: Fetching titles for department {DepartmentId} on demand.", departmentId);
                var titlesResponse = await _apiService.GetAsync<ApiResponse<List<string>>>(string.Format(ApiRoutes.Lookups.GetTitlesByDepartment, departmentId));

                if (titlesResponse != null && titlesResponse.Success && titlesResponse.Data != null)
                {
                    _logger.LogInformation("DIAGNOSTIC: Loaded {Count} titles for department {DepartmentId}",
                                        titlesResponse.Data.Count, departmentId);
                    return titlesResponse.Data;
                }
                else
                {
                    _logger.LogWarning("DIAGNOSTIC: No titles found or API error for department {DepartmentId}. Message: {ApiMessage}",
                                        departmentId, titlesResponse?.ErrorMessage ?? "N/A");
                    return new List<string>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DIAGNOSTIC: Exception fetching titles for department {DepartmentId}", departmentId);
            return new List<string>();
            }
        }

        public async Task LoadDataAsync()
        {
            if (_disposed) return;

            try
            {
                System.Diagnostics.Debug.WriteLine("[LookupService CONSOLE] LoadDataAsync: Simplified - Loading only departments.");
                _logger.LogInformation("DIAGNOSTIC: LoadDataAsync - Simplified - Loading only departments.");

                // Check network connectivity before making API call
                var networkAccess = Connectivity.NetworkAccess;
                _logger.LogInformation("DIAGNOSTIC: Network connectivity status: {NetworkStatus}", networkAccess);

                if (networkAccess != NetworkAccess.Internet)
                {
                    _logger.LogWarning("DIAGNOSTIC: No internet connectivity. Network status: {NetworkStatus}", networkAccess);
                    throw new Exception($"No internet connection. Current network status: {networkAccess}");
                }

                // Log the API endpoint URL
                string departmentsEndpoint = ApiRoutes.Lookups.GetDepartments;
                _logger.LogInformation("DIAGNOSTIC: Calling departments endpoint: {Endpoint}", departmentsEndpoint);

                // Make the API call with detailed logging
                _logger.LogInformation("DIAGNOSTIC: Directly calling departments endpoint");

                try
                {
                    var departments = await _apiService.GetDepartmentsAsync();

                    // Enhanced logging for department data received by LookupService
                    if (departments == null)
                    {
                        _logger.LogWarning("DIAGNOSTIC: _apiService.GetDepartmentsAsync() returned a NULL list to LookupService.");
                    }
                    else
                    {
                        _logger.LogInformation("DIAGNOSTIC: _apiService.GetDepartmentsAsync() returned a list with {Count} items to LookupService.", departments.Count);
                        if (departments.Any())
                        {
                            // Log details of the first department to inspect deserialized values
                            var firstDept = departments[0];
                            _logger.LogInformation("DIAGNOSTIC: First department received by LookupService - Id: {Id}, Name: {Name}, Value (from JSON 'category'): {Value}, Description: {Description}, SortOrder: {SortOrder}",
                                                   firstDept.Id, firstDept.Name, firstDept.Value, firstDept.Description, firstDept.SortOrder);

                            // Log all departments for debugging
                            for (int i = 0; i < departments.Count; i++)
                            {
                                var dept = departments[i];
                                _logger.LogDebug("DIAGNOSTIC: Department[{Index}] - Id: {Id}, Value: {Value}",
                                    i, dept.Id, dept.Value);
                            }
                        }
                    }

                    if (departments != null) { // Allow empty list as a valid response
                        _logger.LogInformation("DIAGNOSTIC: Successfully processed departments list (Count: {Count})", departments.Count);
                        _departments = departments;

                        System.Diagnostics.Debug.WriteLine($"[LookupService CONSOLE] Final State: Departments loaded: {_departments?.Count ?? 0}");
                        _logger.LogInformation("DIAGNOSTIC: Final loaded state - Departments: {DeptCount}", _departments?.Count ?? 0);
                    }
                    else {
                        _logger.LogError("DIAGNOSTIC: Department data was null after API call. No fallback departments will be provided.");
                        _departments = new List<LookupItem>(); // Empty list, no fallback
                    }
                }
                catch (Exception apiEx)
                {
                    _logger.LogError(apiEx, "DIAGNOSTIC: Error calling GetDepartmentsAsync API: {Message}", apiEx.Message);
                    _departments = new List<LookupItem>(); // Empty list, no fallback
                    throw; // Re-throw to let calling code handle the error
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LookupService CONSOLE Error] Failed to load data: {ex.Message}\n\nStack: {ex.StackTrace}");
                _logger.LogError(ex, "DIAGNOSTIC: Failed to load lookup data");
                _departments = new List<LookupItem>(); // Empty list, no fallback
                throw; // Re-throw to let calling code handle the error
            }
        }

        public async Task<IEnumerable<TitleDTO>> GetTitlesAsync()
        {
            _logger.LogWarning("GetTitlesAsync called, but titles are loaded on demand. This method may not function as expected.");
                return new List<TitleDTO>();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _departments?.Clear();
                }
                _disposed = true;
            }
        }

        ~LookupService()
        {
            Dispose(false);
        }
    }
}