using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using Microsoft.Maui.ApplicationModel;
using TDFShared.DTOs.Common;
using Microsoft.Extensions.Logging;

namespace TDFMAUI.Services
{
    public class LookupService : ILookupService, IDisposable
    {
        private readonly ApiService _apiService;
        private readonly ILogger<LookupService> _logger;
        private List<LookupItem> _departments;
        private bool _isDataLoaded = false;
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
                if (!_isDataLoaded)
                {
                    await LoadDataAsync();
                }
                
                string debugInfo = $"Returning {_departments?.Count ?? 0} departments";
                if (_departments != null && _departments.Any())
                {
                    debugInfo += $"\nFirst department: {_departments[0].Id}";
                }
                System.Diagnostics.Debug.WriteLine($"[LookupService CONSOLE] Departments Result: {debugInfo}");
                
                return _departments ?? new List<LookupItem>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LookupService CONSOLE Error] Error in GetDepartmentsAsync: {ex.Message}");
                throw;
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
                var titlesResponse = await _apiService.GetAsync<ApiResponse<List<string>>>($"lookups/titles/{departmentId}");
                
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

                    _logger.LogInformation("DIAGNOSTIC: Directly calling departments endpoint");
                    var departments = await _apiService.GetDepartmentsAsync();

                    if (departments != null && departments.Any()) {
                        _logger.LogInformation("DIAGNOSTIC: Successfully got {Count} departments directly", departments.Count);
                        _departments = departments;
                        _isDataLoaded = true;
                        
                    System.Diagnostics.Debug.WriteLine($"[LookupService CONSOLE] Final State: Departments loaded: {_departments?.Count ?? 0}");
                    _logger.LogInformation("DIAGNOSTIC: Final loaded state - Departments: {DeptCount}", _departments?.Count ?? 0);
                }
                else {
                        _logger.LogWarning("DIAGNOSTIC: Department endpoint returned no data");
                     _isDataLoaded = false;
                     throw new Exception("Failed to load department lookup data from API.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LookupService CONSOLE Error] Failed to load data: {ex.Message}\n\nStack: {ex.StackTrace}");
                _logger.LogError(ex, "DIAGNOSTIC: Failed to load lookup data");
                _isDataLoaded = false;
                throw new Exception($"Failed to load lookup data from API. Please check logs or network connection. Original error: {ex.Message}", ex);
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