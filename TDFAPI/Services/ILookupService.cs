using TDFShared.DTOs.Common;

namespace TDFAPI.Services
{
    public interface ILookupService
    {
        Task<List<LookupItem>> GetDepartmentsAsync();
        Task<List<string>> GetTitlesByDepartmentAsync(string department);
        Task<LookupResponseDto> GetAllLookupsAsync();
        Task<List<LookupItem>> GetLeaveTypesAsync();
        Task<List<LookupItem>> GetStatusCodesAsync();
        Task<List<string>> GetRequestTypesAsync();
    }
} 