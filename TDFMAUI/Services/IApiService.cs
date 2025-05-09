using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TDFShared.DTOs.Auth;
using TDFShared.DTOs.Common;
using TDFShared.DTOs.Requests;
using TDFShared.DTOs.Users;
using TDFShared.Models.User;

namespace TDFMAUI.Services
{
    public interface IApiService
    {
        // General HTTP methods
        Task<string> GetRawResponseAsync(string endpoint);
        Task<T> GetAsync<T>(string endpoint, bool queueIfUnavailable = false);
        
        // Auth
        Task<LoginResponseDto> LoginAsync(LoginRequestDto loginRequest);
        Task<bool> LogoutAsync();
        Task<RegisterResponseDto> RegisterAsync(RegisterRequestDto registerRequest);
        Task<bool> SignupAsync(SignupModel signupModel);
        
        // Users
        Task<UserDto> GetCurrentUserAsync();
        Task<int> GetCurrentUserIdAsync();
        Task<bool> IsAuthenticatedAsync();
        Task<UserProfileDto> GetUserProfileAsync(int userId);
        
        // Requests
        Task<RequestResponseDto> GetRequestByIdAsync(Guid requestId, bool queueIfUnavailable = true);
        Task<RequestResponseDto> CreateRequestAsync(RequestCreateDto requestDto);
        Task<RequestResponseDto> UpdateRequestAsync(Guid requestId, RequestUpdateDto requestDto);
        Task<bool> DeleteRequestAsync(Guid requestId);
        Task<bool> ApproveRequestAsync(Guid requestId, RequestApprovalDto approvalDto);
        Task<bool> RejectRequestAsync(Guid requestId, RequestRejectDto rejectDto);
        Task<PaginatedResult<RequestResponseDto>> GetRequestsAsync(RequestPaginationDto pagination, int? userId = null, string department = null, bool queueIfUnavailable = true);
        
        // Leaderboard and balances
        Task<Dictionary<string, int>> GetLeaveBalancesAsync(int userId, bool queueIfUnavailable = true);
        
        // Common
        Task<List<LookupItem>> GetDepartmentsAsync(bool queueIfUnavailable = true);
        Task<List<LookupItem>> GetLeaveTypesAsync(bool queueIfUnavailable = true);
        
        // Connectivity
        Task<bool> TestConnectivityAsync();
    }
} 