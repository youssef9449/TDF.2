using System.Threading.Tasks;
using TDFShared.DTOs.Auth;
using TDFShared.DTOs.Common;

namespace TDFMAUI.Services
{
    public interface IAuthApiService
    {
        Task<ApiResponse<TokenResponse>> LoginAsync(LoginRequestDto loginRequest);
        Task<ApiResponse<bool>> LogoutAsync();
        Task<ApiResponse<RegisterResponseDto>> RegisterAsync(RegisterRequestDto registerRequest);
        Task<ApiResponse<bool>> IsAuthenticatedAsync();
        Task<bool> RegisterPushTokenAsync(TDFShared.DTOs.Users.PushTokenRegistrationDto registration);
        Task<bool> UnregisterPushTokenAsync(string token);
    }
}
