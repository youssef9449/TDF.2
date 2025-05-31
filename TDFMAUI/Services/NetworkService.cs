using Microsoft.Maui.Networking;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace TDFMAUI.Services
{
    public class NetworkService
    {
        private readonly IConnectivity _connectivity;
        private readonly WebSocketService _webSocketService;
        private readonly ApiService _apiService;
        private readonly ILogger<NetworkService> _logger;
        private bool _isMonitoring = false;

        public bool IsConnected => _connectivity.NetworkAccess == NetworkAccess.Internet;

        public event EventHandler<bool> ConnectivityChanged;
        public event EventHandler<bool> NetworkStatusChanged;

        public NetworkService(IConnectivity connectivity, WebSocketService webSocketService, ApiService apiService, ILogger<NetworkService> logger)
        {
            // Log constructor entry
            logger?.LogInformation("NetworkService constructor started.");

            _connectivity = connectivity ?? throw new ArgumentNullException(nameof(connectivity));
            _webSocketService = webSocketService ?? throw new ArgumentNullException(nameof(webSocketService));
            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _connectivity.ConnectivityChanged += OnConnectivityChanged;

            // Log constructor exit
            logger?.LogInformation("NetworkService constructor finished.");
        }

        public async Task StartMonitoringAsync()
        {
            if (_isMonitoring)
                return;

            _isMonitoring = true;
            _connectivity.ConnectivityChanged += OnConnectivityChanged;

            var isConnected = IsConnected;
            _logger.LogInformation($"Network monitoring started. Initial status: {(isConnected ? "Connected" : "Disconnected")}");
            NetworkStatusChanged?.Invoke(this, isConnected);
            ConnectivityChanged?.Invoke(this, isConnected);
        }

        private async Task ConnectWebSocketAsync()
        {
            try
            {
                await _webSocketService.ConnectAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect WebSocket during startup");
            }
        }

        private async void OnConnectivityChanged(object sender, ConnectivityChangedEventArgs e)
        {
            var isConnected = e.NetworkAccess == NetworkAccess.Internet;

            ConnectivityChanged?.Invoke(this, isConnected);
            NetworkStatusChanged?.Invoke(this, isConnected);

            if (isConnected)
            {
                _logger.LogInformation("Network connectivity restored, reconnecting services");

                var apiConnected = await _apiService.ValidateApiConnectionAsync();

                // Only reconnect WebSocket if user is authenticated and token is present
                var token = TDFMAUI.Config.ApiConfig.CurrentToken;
                var tokenValid = !string.IsNullOrEmpty(token) && TDFMAUI.Config.ApiConfig.TokenExpiration > DateTime.UtcNow;
                if (apiConnected && tokenValid)
                {
                    await _webSocketService.ConnectAsync(token);
                }
                else
                {
                    _logger.LogInformation("Skipping WebSocket reconnect: user not authenticated or token missing/expired.");
                }
            }
            else
            {
                _logger.LogInformation("Network connectivity lost");
            }
        }

        public async Task<bool> WaitForNetworkAsync(TimeSpan timeout)
        {
            if (IsConnected)
                return true;

            var tcs = new TaskCompletionSource<bool>();
            var handler = new EventHandler<bool>((s, connected) => {
                if (connected)
                    tcs.TrySetResult(true);
            });

            try
            {
                ConnectivityChanged += handler;

                var timeoutTask = Task.Delay(timeout);
                var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

                return completedTask == tcs.Task && await tcs.Task;
            }
            finally
            {
                ConnectivityChanged -= handler;
            }
        }
    }
}