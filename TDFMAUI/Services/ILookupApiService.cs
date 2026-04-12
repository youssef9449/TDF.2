using System.Collections.Generic;
using System.Threading.Tasks;
using TDFShared.DTOs.Common;

namespace TDFMAUI.Services
{
    public interface ILookupApiService
    {
        Task<ApiResponse<List<LookupItem>>> GetDepartmentsAsync(bool queueIfUnavailable = true);
        Task<List<LookupItem>> GetLeaveTypesAsync(bool queueIfUnavailable = true);
    }
}
