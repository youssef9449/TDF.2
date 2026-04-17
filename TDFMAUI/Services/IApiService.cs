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
    public interface IApiService : IAuthApiService, IRequestApiService, IUserApiService, IMessageService, ILookupApiService
    {
        // General HTTP methods
        Task<string> GetRawResponseAsync(string endpoint);
        Task<T> GetAsync<T>(string endpoint, bool queueIfUnavailable = false);
        
        // Connectivity
        Task<bool> TestConnectivityAsync();
    }
}
