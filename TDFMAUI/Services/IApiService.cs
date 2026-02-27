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
        Task<ApiResponse<LoginResponseDto>> LoginAsync(LoginRequestDto loginRequest);
        Task<ApiResponse<bool>> LogoutAsync();
        Task<ApiResponse<RegisterResponseDto>> RegisterAsync(RegisterRequestDto registerRequest);
        
        // Users
        Task<ApiResponse<UserDto>> GetCurrentUserAsync();
        Task<ApiResponse<int>> GetCurrentUserIdAsync();
        Task<ApiResponse<bool>> IsAuthenticatedAsync();
        Task<ApiResponse<UserProfileDto>> GetUserProfileAsync(int userId);
        
        // Requests
        Task<ApiResponse<RequestResponseDto>> GetRequestByIdAsync(int requestId, bool queueIfUnavailable = true);
        Task<ApiResponse<RequestResponseDto>> CreateRequestAsync(RequestCreateDto requestDto);
        Task<ApiResponse<RequestResponseDto>> UpdateRequestAsync(int requestId, RequestUpdateDto requestDto);
        Task<ApiResponse<bool>> DeleteRequestAsync(int requestId);
        Task<ApiResponse<bool>> ManagerApproveRequestAsync(int requestId, ManagerApprovalDto approvalDto);
        Task<ApiResponse<bool>> HRApproveRequestAsync(int requestId, HRApprovalDto approvalDto);
        Task<ApiResponse<bool>> ManagerRejectRequestAsync(int requestId, ManagerRejectDto rejectDto);
        Task<ApiResponse<bool>> HRRejectRequestAsync(int requestId, HRRejectDto rejectDto);
        Task<ApiResponse<PaginatedResult<RequestResponseDto>>> GetRequestsAsync(RequestPaginationDto pagination, int? userId = null, string department = null, bool queueIfUnavailable = true);
        
        // Leaderboard and balances
        Task<ApiResponse<Dictionary<string, int>>> GetLeaveBalancesAsync(int userId, bool queueIfUnavailable = true);
        
        // Common
        Task<ApiResponse<List<LookupItem>>> GetDepartmentsAsync(bool queueIfUnavailable = true);
        Task<List<LookupItem>> GetLeaveTypesAsync(bool queueIfUnavailable = true);
        
        // Push Token
        Task<ApiResponse<bool>> RegisterPushTokenAsync(PushTokenRegistrationDto registration);
        Task<ApiResponse<bool>> UnregisterPushTokenAsync(string token);

        // Connectivity
        Task<bool> TestConnectivityAsync();
    }
}