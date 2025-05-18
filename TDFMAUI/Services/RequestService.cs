using System;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TDFShared.DTOs.Requests;
using TDFMAUI.Config; // Added for ApiConfig
using Microsoft.Extensions.Logging; // Added for logging
using TDFShared.DTOs.Common; // Added for PaginatedResult
using TDFShared.Exceptions;
using TDFShared.Constants;

namespace TDFMAUI.Services;

public class RequestService : IRequestService
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly ILogger<RequestService> _logger;
    private readonly string _baseApiUrl; // Store base URL

    public RequestService(HttpClient httpClient, ILogger<RequestService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true // Common for web APIs
        };
        // Get Base URL from static ApiConfig
        _baseApiUrl = ApiConfig.BaseUrl ?? throw new InvalidOperationException("API Base URL is not configured.");
        _logger.LogInformation("RequestService initialized with Base URL: {BaseUrl}", _baseApiUrl);
    }

    public async Task<RequestResponseDto> CreateRequestAsync(RequestCreateDto requestDto)
    {
        // Use ApiRoutes.Requests.Base from TDFShared
        var uri = $"{_baseApiUrl}{ApiRoutes.Requests.Base}";
        _logger.LogInformation("Sending POST request to {Uri}", uri);
        var response = await _httpClient.PostAsJsonAsync(uri, requestDto, _serializerOptions);
        return await HandleApiResponse<RequestResponseDto>(response, "CreateRequestAsync");
    }

    public async Task<RequestResponseDto> UpdateRequestAsync(Guid requestId, RequestUpdateDto requestDto)
    {
        // Use ApiRoutes.Requests.GetById with string.Format
        var uri = $"{_baseApiUrl}{string.Format(ApiRoutes.Requests.GetById, requestId)}";
        _logger.LogInformation("Sending PUT request to {Uri}", uri);
        var response = await _httpClient.PutAsJsonAsync(uri, requestDto, _serializerOptions);
        return await HandleApiResponse<RequestResponseDto>(response, "UpdateRequestAsync");
    }

    // --- New Method Implementations ---

    public async Task<PaginatedResult<RequestResponseDto>> GetMyRequestsAsync(RequestPaginationDto pagination)
    {
        var queryString = BuildQueryString(pagination);
        // Use ApiRoutes.Requests.GetMy
        var uri = $"{_baseApiUrl}{ApiRoutes.Requests.GetMy}{queryString}";
        _logger.LogInformation("Sending GET request to {Uri}", uri);
        var response = await _httpClient.GetAsync(uri);
        return await HandleApiResponse<PaginatedResult<RequestResponseDto>>(response, "GetMyRequestsAsync");
    }

    public async Task<PaginatedResult<RequestResponseDto>> GetAllRequestsAsync(RequestPaginationDto pagination)
    {
        var queryString = BuildQueryString(pagination);
        // Use ApiRoutes.Requests.GetAll
        var uri = $"{_baseApiUrl}{ApiRoutes.Requests.GetAll}{queryString}";
        _logger.LogInformation("Sending GET request to {Uri}", uri);
        var response = await _httpClient.GetAsync(uri);
        return await HandleApiResponse<PaginatedResult<RequestResponseDto>>(response, "GetAllRequestsAsync");
    }

    public async Task<PaginatedResult<RequestResponseDto>> GetRequestsByDepartmentAsync(string department, RequestPaginationDto pagination)
    {
        var queryString = BuildQueryString(pagination);
        // Use ApiRoutes.Requests.GetByDepartment with string.Format
        var uri = $"{_baseApiUrl}{string.Format(ApiRoutes.Requests.GetByDepartment, Uri.EscapeDataString(department))}{queryString}";
        _logger.LogInformation("Sending GET request to {Uri}", uri);
        var response = await _httpClient.GetAsync(uri);
        return await HandleApiResponse<PaginatedResult<RequestResponseDto>>(response, "GetRequestsByDepartmentAsync");
    }

    public async Task<RequestResponseDto> GetRequestByIdAsync(Guid requestId)
    {
        // Use ApiRoutes.Requests.GetById with string.Format
        var uri = $"{_baseApiUrl}{string.Format(ApiRoutes.Requests.GetById, requestId)}";
        _logger.LogInformation("Sending GET request to {Uri}", uri);
        var response = await _httpClient.GetAsync(uri);
        return await HandleApiResponse<RequestResponseDto>(response, "GetRequestByIdAsync");
    }

    public async Task<bool> DeleteRequestAsync(Guid requestId)
    {
        // Use ApiRoutes.Requests.GetById with string.Format
        var uri = $"{_baseApiUrl}{string.Format(ApiRoutes.Requests.GetById, requestId)}";
        _logger.LogInformation("Sending DELETE request to {Uri}", uri);
        var response = await _httpClient.DeleteAsync(uri);
        _logger.LogInformation("DeleteRequestAsync: Received response status {StatusCode}", response.StatusCode);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ApproveRequestAsync(Guid requestId, RequestApprovalDto approvalDto)
    {
        // Use ApiRoutes.Requests.Approve with string.Format
        var uri = $"{_baseApiUrl}{string.Format(ApiRoutes.Requests.Approve, requestId)}";
        _logger.LogInformation("Sending POST request to {Uri}", uri);
        var response = await _httpClient.PostAsJsonAsync(uri, approvalDto, _serializerOptions);
        _logger.LogInformation("ApproveRequestAsync: Received response status {StatusCode}", response.StatusCode);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> RejectRequestAsync(Guid requestId, RequestRejectDto rejectDto)
    {
        // Use ApiRoutes.Requests.Reject with string.Format
        var uri = $"{_baseApiUrl}{string.Format(ApiRoutes.Requests.Reject, requestId)}";
        _logger.LogInformation("Sending POST request to {Uri}", uri);
        var response = await _httpClient.PostAsJsonAsync(uri, rejectDto, _serializerOptions);
        _logger.LogInformation("RejectRequestAsync: Received response status {StatusCode}", response.StatusCode);
        return response.IsSuccessStatusCode;
    }

    // Helper to build query string from pagination DTO
    private string BuildQueryString(RequestPaginationDto dto)
    {
        var queryParams = new List<string>();

        if (dto.Page > 0) queryParams.Add($"page={dto.Page}");
        if (dto.PageSize > 0) queryParams.Add($"pageSize={dto.PageSize}");
        if (!string.IsNullOrEmpty(dto.SortBy)) queryParams.Add($"sortBy={Uri.EscapeDataString(dto.SortBy)}");
        queryParams.Add($"ascending={dto.Ascending.ToString().ToLower()}"); // Add ascending bool
        if (!string.IsNullOrEmpty(dto.FilterStatus) && !dto.FilterStatus.Equals("All", StringComparison.OrdinalIgnoreCase))
            queryParams.Add($"filterStatus={Uri.EscapeDataString(dto.FilterStatus)}");
        if (!string.IsNullOrEmpty(dto.FilterType) && !dto.FilterType.Equals("All", StringComparison.OrdinalIgnoreCase))
            queryParams.Add($"filterType={Uri.EscapeDataString(dto.FilterType)}");
        if (dto.FromDate.HasValue) queryParams.Add($"fromDate={dto.FromDate.Value:yyyy-MM-dd}");
        if (dto.ToDate.HasValue) queryParams.Add($"toDate={dto.ToDate.Value:yyyy-MM-dd}");
        if (dto.UserId.HasValue) queryParams.Add($"userId={dto.UserId.Value}");
        if (!string.IsNullOrEmpty(dto.Department))
            queryParams.Add($"department={Uri.EscapeDataString(dto.Department)}");

        return queryParams.Any() ? "?" + string.Join("&", queryParams) : string.Empty;
    }

    // Helper to handle API responses (optional but recommended)
    private async Task<T> HandleApiResponse<T>(HttpResponseMessage response, string operationName)
    {
        _logger.LogInformation("{Operation}: Received response status {StatusCode}", operationName, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("{Operation}: API request failed with status {StatusCode}. Content: {Content}",
                             operationName, response.StatusCode, content);
            // Throw a specific exception based on status code or content if needed
            response.EnsureSuccessStatusCode(); // This will throw HttpRequestException
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            _logger.LogWarning("{Operation}: API returned success status but empty body.", operationName);
            // Handle cases where the API might return an empty body on success (e.g., 204 No Content for PUT/DELETE)
            return default; // Return default for T (could be null)
        }

        try
        {
            var result = JsonSerializer.Deserialize<T>(content, _serializerOptions);
            if (result == null)
            {
                _logger.LogError("{Operation}: Failed to deserialize non-empty API response to type {TypeName}. Content: {Content}",
                                 operationName, typeof(T).Name, content);
                throw new ApiException($"Failed to deserialize API response to type {typeof(T).Name}.");
            }
            _logger.LogInformation("{Operation}: Successfully deserialized response to {TypeName}.", operationName, typeof(T).Name);
            return result;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "{Operation}: JSON Deserialization Error. Content: {Content}", operationName, content);
            throw new ApiException("Error processing response from API.", ex);
        }
    }
}


// Simple custom exception class
//public class ApiException : Exception
//{
//    public ApiException(string message) : base(message) { }
//    public ApiException(string message, Exception innerException) : base(message, innerException) { }
//}