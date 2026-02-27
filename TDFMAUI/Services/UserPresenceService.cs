using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TDFMAUI.Helpers;
using TDFShared.Enums;
using TDFShared.DTOs.Users;
using TDFShared.DTOs.Common;

namespace TDFMAUI.Services
{
    public class UserPresenceService : IUserPresenceService, IDisposable
    {
        private readonly IUserPresenceApiService _apiService;
        private readonly IUserPresenceEventsService _eventsService;
        private readonly IUserPresenceCacheService _cacheService;
        private readonly ILogger<UserPresenceService> _logger;
        private readonly Timer _activityTimer;

        public event EventHandler<UserStatusChangedEventArgs>? UserStatusChanged;
        public event EventHandler<UserAvailabilityChangedEventArgs>? UserAvailabilityChanged;
        public event EventHandler<AvailabilitySetEventArgs>? AvailabilityConfirmed;
        public event EventHandler<StatusUpdateConfirmedEventArgs>? StatusUpdateConfirmed;
        public event EventHandler<WebSocketErrorEventArgs>? PresenceErrorReceived;

        public UserPresenceService(
            IUserPresenceApiService apiService,
            IUserPresenceEventsService eventsService,
            IUserPresenceCacheService cacheService,
            ILogger<UserPresenceService> logger)
        {
            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            _eventsService = eventsService ?? throw new ArgumentNullException(nameof(eventsService));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _eventsService.UserStatusChanged += OnUserStatusChanged;
            _eventsService.UserAvailabilityChanged += OnUserAvailabilityChanged;
            _eventsService.AvailabilitySet += OnAvailabilitySet;
            _eventsService.StatusUpdateConfirmed += OnStatusUpdateConfirmed;
            _eventsService.PresenceErrorReceived += OnErrorReceived;

            _activityTimer = new Timer(SendActivityPing, null, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));
        }

        public async Task<UserPresenceStatus> GetUserStatusAsync(int userId)
        {
            if (_cacheService.TryGetUserStatus(userId, out var status))
            {
                return status.Status;
            }

            var response = await _apiService.GetUserStatusAsync(userId);
            if (response != null)
            {
                _cacheService.UpdateUserStatus(userId, response);
                return response.Status;
            }

            return UserPresenceStatus.Offline;
        }

        public async Task<PaginatedResult<UserPresenceInfo>> GetOnlineUsersAsync(int page = 1, int pageSize = 100)
        {
            var result = await _apiService.GetOnlineUsersAsync(page, pageSize);
            if (page == 1)
            {
                _cacheService.Clear();
            }

            var batchDict = new Dictionary<int, UserPresenceInfo>();
            foreach(var item in result.Items)
            {
                batchDict[item.UserId] = item;
            }
            _cacheService.UpdateBatch(batchDict);
            return result;
        }

        public Dictionary<int, UserPresenceInfo> GetCachedOnlineUsers()
        {
            return _cacheService.GetAllCachedUsers();
        }

        public async Task UpdateStatusAsync(UserPresenceStatus status, string? statusMessage = null, CancellationToken cancellationToken = default)
        {
            if (App.CurrentUser == null || App.CurrentUser.UserID <= 0)
            {
                _logger.LogWarning("Cannot update status: Current user is null or has invalid ID");
                return;
            }

            int userId = App.CurrentUser.UserID;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                // WebSocket update
                if (_eventsService.IsConnected)
                {
                    await _eventsService.UpdatePresenceStatusAsync(status.ToString(), statusMessage ?? string.Empty);
                    _logger.LogInformation("Status update sent via WebSocket");
                }
                else
                {
                    _logger.LogWarning("WebSocket is not connected, status update skipped for WebSocket");
                }

                // API update
                await _apiService.UpdateUserConnectionStatusAsync(userId, status != UserPresenceStatus.Offline);

                // Local cache update
                if (_cacheService.TryGetUserStatus(userId, out var userInfo))
                {
                    userInfo.Status = status;
                    userInfo.StatusMessage = statusMessage;
                }
                else
                {
                    _cacheService.UpdateUserStatus(userId, new UserPresenceInfo
                    {
                        UserId = userId,
                        Username = App.CurrentUser.UserName,
                        FullName = App.CurrentUser.FullName,
                        Status = status,
                        StatusMessage = statusMessage,
                        LastActivityTime = DateTime.UtcNow
                    });
                }

                _logger.LogInformation("Successfully updated user status to {Status}", status);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Status update operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating presence status to {Status}", status);
                throw;
            }
        }

        public async Task SetAvailabilityForChatAsync(bool isAvailable)
        {
            if (App.CurrentUser == null || App.CurrentUser.UserID <= 0) return;

            try
            {
                await _eventsService.SetAvailableForChatAsync(isAvailable);
                if (_cacheService.TryGetUserStatus(App.CurrentUser.UserID, out var userInfo))
                {
                    userInfo.IsAvailableForChat = isAvailable;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating chat availability");
            }
        }

        public async Task UpdateUserStatusAsync(int userId, UserPresenceStatus status, string? statusMessage = null)
        {
            if (App.CurrentUser != null && App.CurrentUser.UserID == userId)
            {
                await UpdateStatusAsync(status, statusMessage);
            }
            else
            {
                if (_cacheService.TryGetUserStatus(userId, out var userInfo))
                {
                    userInfo.Status = status;
                    userInfo.StatusMessage = statusMessage;
                }

                UserStatusChanged?.Invoke(this, new UserStatusChangedEventArgs
                {
                    UserId = userId,
                    Status = status,
                    StatusMessage = statusMessage
                });
            }
        }

        public async Task RecordUserActivityAsync(int userId)
        {
            if (App.CurrentUser != null && App.CurrentUser.UserID == userId)
            {
                await _eventsService.SendActivityPingAsync();
            }

            if (_cacheService.TryGetUserStatus(userId, out var userInfo))
            {
                userInfo.LastActivityTime = DateTime.UtcNow;
            }
        }

        public async Task SetAvailabilityForChatAsync(int userId, bool isAvailable)
        {
            if (App.CurrentUser != null && App.CurrentUser.UserID == userId)
            {
                await SetAvailabilityForChatAsync(isAvailable);
            }
        }

        public async Task<Dictionary<int, UserPresenceInfo>> GetUsersPresenceAsync(IEnumerable<int> userIds)
        {
            var result = new Dictionary<int, UserPresenceInfo>();
            foreach (var userId in userIds)
            {
                if (_cacheService.TryGetUserStatus(userId, out var info))
                {
                    result[userId] = info;
                }
                else
                {
                    var status = await _apiService.GetUserStatusAsync(userId);
                    if (status != null)
                    {
                        _cacheService.UpdateUserStatus(userId, status);
                        result[userId] = status;
                    }
                }
            }
            return result;
        }

        private async void SendActivityPing(object? state)
        {
            if (App.CurrentUser != null && _eventsService.IsConnected)
            {
                try
                {
                    await _eventsService.SendActivityPingAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending activity ping");
                }
            }
        }

        private void OnUserStatusChanged(object? sender, UserStatusChangedEventArgs e)
        {
            if (_cacheService.TryGetUserStatus(e.UserId, out var userInfo))
            {
                userInfo.Status = e.Status;
                userInfo.StatusMessage = e.StatusMessage;
            }
            else
            {
                _cacheService.UpdateUserStatus(e.UserId, new UserPresenceInfo
                {
                    UserId = e.UserId,
                    Username = e.Username,
                    Status = e.Status,
                    StatusMessage = e.StatusMessage,
                    LastActivityTime = DateTime.UtcNow
                });
            }
            UserStatusChanged?.Invoke(this, e);
        }

        private void OnUserAvailabilityChanged(object? sender, UserAvailabilityChangedEventArgs e)
        {
            if (_cacheService.TryGetUserStatus(e.UserId, out var userInfo))
            {
                userInfo.IsAvailableForChat = e.IsAvailableForChat;
            }
            else
            {
                 _cacheService.UpdateUserStatus(e.UserId, new UserPresenceInfo
                {
                    UserId = e.UserId,
                    Username = e.Username,
                    IsAvailableForChat = e.IsAvailableForChat,
                    LastActivityTime = e.Timestamp
                });
            }
            UserAvailabilityChanged?.Invoke(this, e);
        }

        private void OnAvailabilitySet(object? sender, AvailabilitySetEventArgs e)
        {
            _logger.LogInformation("Confirmation received: Availability set to {IsAvailable}", e.IsAvailable);
            if (App.CurrentUser != null && _cacheService.TryGetUserStatus(App.CurrentUser.UserID, out var userInfo))
            {
                userInfo.IsAvailableForChat = e.IsAvailable;
            }
            AvailabilityConfirmed?.Invoke(this, e);
        }

        private void OnStatusUpdateConfirmed(object? sender, StatusUpdateConfirmedEventArgs e)
        {
            _logger.LogInformation("Confirmation received: Status updated to {Status} ({StatusMessage})", e.Status, e.StatusMessage ?? "null");
            if (App.CurrentUser != null && _cacheService.TryGetUserStatus(App.CurrentUser.UserID, out var userInfo))
            {
                userInfo.Status = ParseStatus(e.Status);
                userInfo.StatusMessage = e.StatusMessage;
            }
            StatusUpdateConfirmed?.Invoke(this, e);
        }

        private void OnErrorReceived(object? sender, WebSocketErrorEventArgs e)
        {
            PresenceErrorReceived?.Invoke(this, e);
        }

        public UserPresenceStatus ParseStatus(string status)
        {
            return Enum.TryParse(status, true, out UserPresenceStatus result) ? result : UserPresenceStatus.Offline;
        }

        public void Dispose()
        {
            _eventsService.UserStatusChanged -= OnUserStatusChanged;
            _eventsService.UserAvailabilityChanged -= OnUserAvailabilityChanged;
            _eventsService.AvailabilitySet -= OnAvailabilitySet;
            _eventsService.StatusUpdateConfirmed -= OnStatusUpdateConfirmed;
            _eventsService.PresenceErrorReceived -= OnErrorReceived;
            _activityTimer?.Dispose();
        }
    }
}
