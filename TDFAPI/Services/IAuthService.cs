using TDFShared.DTOs.Auth;
using TDFShared.DTOs.Users;

namespace TDFAPI.Services
{
    public interface IAuthService
    {
        Task<TokenResponse?> LoginAsync(string username, string password);
        Task<TokenResponse?> RefreshTokenAsync(string token, string refreshToken);
        string GenerateJwtToken(UserDto user);
        string HashPassword(string password, out string salt);
        bool VerifyPassword(string password, string storedHash, string salt);
        Task RevokeTokenAsync(string jti, DateTime expiryDateUtc);
        Task<bool> IsTokenRevokedAsync(string jti);
    }
} 