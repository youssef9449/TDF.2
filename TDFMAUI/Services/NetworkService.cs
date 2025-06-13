using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TDFShared.Services;
using TDFShared.Models;

namespace TDFMAUI.Services
{
    public class NetworkService
    {
        private readonly IConnectivityService _connectivityService;
        private readonly ILogger<NetworkService> _logger;

        public NetworkService(IConnectivityService connectivityService, ILogger<NetworkService> logger)
        {
            _connectivityService = connectivityService;
            _logger = logger;
        }

        public async Task<bool> IsNetworkAvailableAsync()
        {
            var info = await _connectivityService.GetConnectivityInfoAsync();
            return info.IsConnected;
        }

        public async Task<bool> IsApiReachableAsync()
        {
            return await _connectivityService.TestConnectivityAsync("health", TimeSpan.FromSeconds(5));
        }

        public async Task<ConnectivityInfo> GetConnectivityInfoAsync()
        {
            return await _connectivityService.GetConnectivityInfoAsync();
        }
    }
}