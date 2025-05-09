using System.Collections.Generic;
using System.Threading.Tasks;
using TDFShared.DTOs.Common;

namespace TDFMAUI.Services
{
    public interface ILookupService
    {
        Task<List<LookupItem>> GetDepartmentsAsync();
        Task<List<string>> GetTitlesForDepartmentAsync(string department);
        Task<IEnumerable<TitleDTO>> GetTitlesAsync();
    }
}