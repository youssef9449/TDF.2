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
                    debugInfo += $"\nFirst department: {_departments[0].Name}";
                    _logger.LogInformation("DIAGNOSTIC: First department in result: Name={Name}",
                        _departments[0].Name);
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
                var titles = await _apiService.GetAsync<List<string>>(string.Format(ApiRoutes.Lookups.GetTitlesByDepartment, departmentId));

                if (titles != null && titles.Count > 0)
                {
                    _logger.LogInformation("DIAGNOSTIC: Loaded {Count} titles for department {DepartmentId}", titles.Count, departmentId);
                    return titles;
                }
                else
                {
                    _logger.LogWarning("DIAGNOSTIC: No titles found or API error for department {DepartmentId}.", departmentId);
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
                    _logger.LogInformation("DIAGNOSTIC: About to call _apiService.GetDepartmentsAsync()");
                    var departments = await _apiService.GetDepartmentsAsync();
                    _logger.LogInformation("DIAGNOSTIC: _apiService.GetDepartmentsAsync() completed successfully");

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
                            _logger.LogInformation("DIAGNOSTIC: First department received by LookupService - Name: {Name}",
                                                   firstDept.Name);

                            // Log all departments for debugging
                            for (int i = 0; i < departments.Count; i++)
                            {
                                var dept = departments[i];
                                _logger.LogDebug("DIAGNOSTIC: Department[{Index}] - Name: {Name}",
                                    i, dept.Name);
                            }
                        }
                    }

                    _logger.LogInformation("DIAGNOSTIC: About to assign departments to _departments field");
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
                    _logger.LogInformation("DIAGNOSTIC: LoadDataAsync completed successfully");
                }
                catch (Exception apiEx)
                {
                    _logger.LogError(apiEx, "DIAGNOSTIC: Error calling GetDepartmentsAsync API: {Message}. Full exception: {Exception}", apiEx.Message, apiEx.ToString());
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