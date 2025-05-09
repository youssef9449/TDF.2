using System.Collections.Generic;
using System.Threading.Tasks;
using TDFShared.DTOs.Auth;
using TDFShared.DTOs.Users;

namespace TDFMAUI.Services
{
    public interface IAuthService
    {
        // Assuming the user ID is an integer. Change if it's Guid, string, etc.
        Task<int> GetCurrentUserIdAsync();
        Task<UserDto> GetCurrentUserAsync();
        Task<List<string>> GetUserRolesAsync();
        Task<string?> GetCurrentUserDepartmentAsync();

        // --- Login/Logout --- 
        Task<UserDetailsDto?> LoginAsync(string username, string password);
        Task LogoutAsync();

        // Potentially other methods like:
        // Task<bool> IsUserAuthenticatedAsync();
    }
} 