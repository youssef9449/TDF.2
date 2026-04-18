using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using Microsoft.Maui.ApplicationModel;
using TDFShared.Constants;
using TDFShared.DTOs.Common;
using Microsoft.Extensions.Logging;
using System.Linq;
using TDFShared.Services;

namespace TDFMAUI.Services
{
    public class LookupService : ILookupService, IDisposable
    {
        private readonly ILookupApiService _lookupApiService;
        private readonly IHttpClientService _httpClientService;
        private readonly ILogger<LookupService> _logger;
        private List<LookupItem> _departments;
        private bool _disposed;

        public LookupService(
            ILookupApiService lookupApiService,
            IHttpClientService httpClientService,
            ILogger<LookupService> logger)
        {
            _lookupApiService = lookupApiService ?? throw new ArgumentNullException(nameof(lookupApiService));
            _httpClientService = httpClientService ?? throw new ArgumentNullException(nameof(httpClientService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _departments = new List<LookupItem>();
            _logger.LogInformation("LookupService initialized");
        }

        public async Task<List<LookupItem>> GetDepartmentsAsync()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(LookupService));

            try
            {
                _logger.LogInformation("GetDepartmentsAsync called");

                // Ensure we have a non-null departments list
                _departments ??= new List<LookupItem>();

                // Always reload departments to ensure fresh data
                await LoadDataAsync();

                _logger.LogInformation("GetDepartmentsAsync returning {Count} departments", _departments.Count);

                if (_departments.Any())
                {
                    _logger.LogDebug("First department - Id: {Id}, Name: {Name}", 
                        _departments[0].Id, _departments[0].Name);
                }

                // Make a defensive copy to avoid potential issues with the shared list
                return new List<LookupItem>(_departments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetDepartmentsAsync: {Message}", ex.Message);
                throw;
            }
        }

        public async Task<List<string>> GetTitlesForDepartmentAsync(string departmentId)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(LookupService));
            if (string.IsNullOrEmpty(departmentId))
            {
                _logger.LogWarning("GetTitlesForDepartmentAsync called with null or empty department ID");
                return new List<string>();
            }

            try
            {
                _logger.LogInformation("Fetching titles for department {DepartmentId}", departmentId);
                // ILookupApiService has no GetTitlesForDepartmentAsync; call the endpoint directly.
                var response = await _httpClientService.GetAsync<ApiResponse<List<string>>>(
                    string.Format(ApiRoutes.Lookups.GetTitlesByDepartment, departmentId));

                if (response?.Success == true && response.Data != null)
                {
                    _logger.LogInformation("Loaded {Count} titles for department {DepartmentId}", 
                        response.Data.Count, departmentId);
                    return response.Data;
                }
                else
                {
                    _logger.LogWarning("No titles found or API error for department {DepartmentId}", departmentId);
                    return new List<string>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception fetching titles for department {DepartmentId}", departmentId);
                return new List<string>();
            }
        }

        public async Task LoadDataAsync()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(LookupService));

            try
            {
                _logger.LogInformation("LoadDataAsync - Loading departments");

                var networkAccess = Connectivity.NetworkAccess;
                _logger.LogInformation("Network connectivity status: {NetworkStatus}", networkAccess);

                if (networkAccess != NetworkAccess.Internet)
                {
                    throw new Exception($"No internet connection. Current network status: {networkAccess}");
                }

                var departmentsResponse = await _lookupApiService.GetDepartmentsAsync();
                _logger.LogInformation("GetDepartmentsAsync completed");

                if (!departmentsResponse.Success)
                {
                    throw new Exception($"Failed to get departments: {departmentsResponse.Message}");
                }

                var departments = departmentsResponse.Data ?? new List<LookupItem>();
                _logger.LogInformation("Received {Count} departments", departments.Count);

                // Ensure all departments have an Id
                foreach (var dept in departments.Where(d => string.IsNullOrEmpty(d.Id)))
                {
                    dept.Id = dept.Name;
                    _logger.LogDebug("Set empty Id to Name for department: {Name}", dept.Name);
                }

                _departments = departments;
                _logger.LogInformation("Departments loaded successfully: {Count} items", _departments.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load departments");
                _departments = new List<LookupItem>();
                throw;
            }
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
                    _departments.Clear();
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