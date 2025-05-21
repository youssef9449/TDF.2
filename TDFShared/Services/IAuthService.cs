using System.Collections.Generic;
using System.Threading.Tasks;
using TDFShared.DTOs.Auth;
using TDFShared.DTOs.Users;

namespace TDFShared.Services
{
    public interface IAuthService
    {
        Task<UserDetailsDto?> LoginAsync(string username, string password);
        Task LogoutAsync();
        Task<int> GetCurrentUserIdAsync();
        Task<List<string>> GetUserRolesAsync();
        Task<string?> GetCurrentUserDepartmentAsync();
        Task<UserDto> GetCurrentUserAsync();
    }
}