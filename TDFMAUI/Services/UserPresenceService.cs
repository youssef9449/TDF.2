using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using TDFMAUI.Helpers;
using TDFShared.Enums;
using TDFShared.DTOs;
using TDFShared.DTOs.Users;


namespace TDFMAUI.Services
{
    public class UserPresenceService : IUserPresenceService
    {
        private readonly ApiService _apiService;
        private readonly WebSocketService _webSocketService;
        private readonly ILogger<UserPresenceService> _logger;
        private readonly ConcurrentDictionary<int, UserPresenceInfo> _userStatuses;
        private readonly Timer _activityTimer;

        public event EventHandler<UserStatusChangedEventArgs> UserStatusChanged;
        public event EventHandler<UserAvailabilityChangedEventArgs> UserAvailabilityChanged;
        public event EventHandler<AvailabilitySetEventArgs> AvailabilityConfirmed;
        public event EventHandler<StatusUpdateConfirmedEventArgs> StatusUpdateConfirmed;
        public event EventHandler<WebSocketErrorEventArgs> PresenceErrorReceived;

        public UserPresenceService(
            ApiService apiService,
            WebSocketService webSocketService,
            ILogger<UserPresenceService> logger)
        {
            // Log constructor entry
            logger?.LogInformation("UserPresenceService constructor started.");

            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            _webSocketService = webSocketService ?? throw new ArgumentNullException(nameof(webSocketService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _userStatuses = new ConcurrentDictionary<int, UserPresenceInfo>();

            // Listen for WebSocket events
            _webSocketService.UserStatusChanged += OnUserStatusChanged;
            _webSocketService.UserAvailabilityChanged += OnUserAvailabilityChanged;
            _webSocketService.AvailabilitySet += OnAvailabilitySet;
            _webSocketService.StatusUpdateConfirmed += OnStatusUpdateConfirmed;
            _webSocketService.ErrorReceived += OnErrorReceived;

            // Start activity tracking timer (send activity ping every 60 seconds)
            _activityTimer = new Timer(SendActivityPing, null, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));

            // Log constructor exit
            logger?.LogInformation("UserPresenceService constructor finished.");
        }

        public async Task<UserPresenceStatus> GetUserStatusAsync(int userId)
        {
            if (_userStatuses.TryGetValue(userId, out var status))
            {
                return status.Status;
            }

            try
            {
                // Fetch from API if not cached
                var response = await _apiService.GetAsync<UserPresenceInfo>($"users/{userId}/status");
                if (response != null)
                {
                    _userStatuses.TryAdd(userId, response);
                    return response.Status;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching user status for user {UserId}", userId);
            }

            return UserPresenceStatus.Offline;
        }

        public async Task<Dictionary<int, UserPresenceInfo>> GetOnlineUsersAsync()
        {
            try
            {
                // Get online users from the API
                var onlineUsersResponse = await _apiService.GetAsync<ApiResponse<IEnumerable<UserDto>>>("users/online");
                if (onlineUsersResponse != null && onlineUsersResponse.Success && onlineUsersResponse.Data != null)
                {
                    var onlineUsers = new List<UserPresenceInfo>();

                    // Map UserDto to UserPresenceInfo
                    foreach (var userDto in onlineUsersResponse.Data)
                    {
                        var userInfo = new UserPresenceInfo
                        {
                            UserId = userDto.UserID,
                            Username = userDto.Username,
                            FullName = userDto.FullName,
                            Department = userDto.Department,
                            Status = userDto.PresenceStatus,
                            StatusMessage = userDto.StatusMessage,
                            IsAvailableForChat = userDto.IsAvailableForChat,
                            LastActivityTime = userDto.LastActivityTime ?? DateTime.UtcNow,
                            ProfilePictureData = userDto.ProfilePictureData
                        };

                        onlineUsers.Add(userInfo);
                        _userStatuses[userInfo.UserId] = userInfo;
                    }

                    return onlineUsers.ToDictionary(u => u.UserId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching online users");
            }

            return _userStatuses.ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        public async Task UpdateStatusAsync(UserPresenceStatus status, string statusMessage)
        {
            if (App.CurrentUser == null)
            {
                _logger.LogWarning("Cannot update status: Current user is null");
                return;
            }

            try
            {
                await _webSocketService.UpdatePresenceStatusAsync(status.ToString(), statusMessage);

                // Update local cache
                int userId = App.CurrentUser.UserID;
                if (_userStatuses.TryGetValue(userId, out var userInfo))
                {
                    userInfo.Status = status;
                    userInfo.StatusMessage = statusMessage;
                }
                else
                {
                    _userStatuses[userId] = new UserPresenceInfo
                    {
                        UserId = userId,
                        FullName = App.CurrentUser.FullName,
                        Username = App.CurrentUser.Username,
                        Status = status,
                        StatusMessage = statusMessage,
                        LastActivityTime = DateTime.UtcNow
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating presence status to {Status}", status);
            }
        }

        public async Task SetAvailabilityForChatAsync(bool isAvailable)
        {
            if (App.CurrentUser == null)
            {
                _logger.LogWarning("Cannot update chat availability: Current user is null");
                return;
            }

            try
            {
                await _webSocketService.SetAvailableForChatAsync(isAvailable);

                // Update local cache
                int userId = App.CurrentUser.UserID;
                if (_userStatuses.TryGetValue(userId, out var userInfo))
                {
                    userInfo.IsAvailableForChat = isAvailable;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating chat availability to {IsAvailable}", isAvailable);
            }
        }

        private async void SendActivityPing(object? state)
        {
            if (App.CurrentUser != null && _webSocketService.IsConnected)
            {
                try
                {
                    await _webSocketService.SendActivityPingAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending activity ping");
                }
            }
        }

        private void OnUserStatusChanged(object? sender, UserStatusEventArgs e)
        {
            if (_userStatuses.TryGetValue(e.UserId, out var userInfo))
            {
                userInfo.Status = ParseStatus(e.PresenceStatus);
                userInfo.StatusMessage = e.StatusMessage;
            }
            else
            {
                _userStatuses.TryAdd(e.UserId, new UserPresenceInfo {
                    UserId = e.UserId,
                    Username = e.Username,
                    Status = ParseStatus(e.PresenceStatus),
                    StatusMessage = e.StatusMessage,
                    LastActivityTime = e.Timestamp
                });
            }

            var args = new UserStatusChangedEventArgs
            {
                UserId = e.UserId,
                Username = e.Username,
                Status = ParseStatus(e.PresenceStatus)
            };
            UserStatusChanged?.Invoke(this, args);
        }

        private void OnUserAvailabilityChanged(object? sender, UserAvailabilityEventArgs e)
        {
            if (_userStatuses.TryGetValue(e.UserId, out var userInfo))
            {
                userInfo.IsAvailableForChat = e.IsAvailableForChat;
            }
            else
            {
                _userStatuses.TryAdd(e.UserId, new UserPresenceInfo {
                    UserId = e.UserId,
                    Username = e.Username,
                    IsAvailableForChat = e.IsAvailableForChat,
                    LastActivityTime = e.Timestamp
                });
            }

            var args = new UserAvailabilityChangedEventArgs
            {
                UserId = e.UserId,
                Username = e.Username,
                IsAvailableForChat = e.IsAvailableForChat,
                Timestamp = e.Timestamp
            };
            UserAvailabilityChanged?.Invoke(this, args);
        }

        private void OnAvailabilitySet(object? sender, AvailabilitySetEventArgs e)
        {
            _logger.LogInformation("Confirmation received: Availability set to {IsAvailable}", e.IsAvailable);
            if (App.CurrentUser != null && _userStatuses.TryGetValue(App.CurrentUser.UserID, out var userInfo))
            {
                userInfo.IsAvailableForChat = e.IsAvailable;
            }
            AvailabilityConfirmed?.Invoke(this, e);
        }

        private void OnStatusUpdateConfirmed(object? sender, StatusUpdateConfirmedEventArgs e)
        {
            _logger.LogInformation("Confirmation received: Status updated to {Status} ({StatusMessage})", e.Status, e.StatusMessage ?? "null");
            if (App.CurrentUser != null && _userStatuses.TryGetValue(App.CurrentUser.UserID, out var userInfo))
            {
                userInfo.Status = ParseStatus(e.Status);
                userInfo.StatusMessage = e.StatusMessage;
            }
            StatusUpdateConfirmed?.Invoke(this, e);
        }

        private void OnErrorReceived(object? sender, WebSocketErrorEventArgs e)
        {
            // Forward to PresenceErrorReceived event
            PresenceErrorReceived?.Invoke(this, e);
        }

        public UserPresenceStatus ParseStatus(string status)
        {
            if (Enum.TryParse(status, true, out UserPresenceStatus result))
            {
                return result;
            }

            // Default to offline if string can't be parsed
            _logger.LogWarning("Failed to parse status string: {Status}", status);
            return UserPresenceStatus.Offline;
        }

        public void Dispose()
        {
            _webSocketService.UserStatusChanged -= OnUserStatusChanged;
            _webSocketService.UserAvailabilityChanged -= OnUserAvailabilityChanged;
            _webSocketService.AvailabilitySet -= OnAvailabilitySet;
            _webSocketService.StatusUpdateConfirmed -= OnStatusUpdateConfirmed;
            _webSocketService.ErrorReceived -= OnErrorReceived;

            _activityTimer?.Dispose();
        }

        public async Task<Dictionary<int, UserPresenceStatus>> GetUsersStatusAsync(IEnumerable<int> userIds)
        {
            var result = new Dictionary<int, UserPresenceStatus>();

            foreach (var userId in userIds)
            {
                // First check if we have it cached
                if (_userStatuses.TryGetValue(userId, out var userInfo))
                {
                    result[userId] = userInfo.Status;
                }
                else
                {
                    // Otherwise fetch from the server
                    try
                    {
                        var status = await GetUserStatusAsync(userId);
                        result[userId] = status;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error fetching status for user {UserId}", userId);
                        result[userId] = UserPresenceStatus.Offline; // Default to offline
                    }
                }
            }

            return result;
        }

        public async Task RecordUserActivityAsync(int userId)
        {
            if (userId <= 0)
            {
                _logger.LogWarning("Cannot record activity for invalid user ID: {UserId}", userId);
                return;
            }

            try
            {
                // Update the last activity time in our local cache
                if (_userStatuses.TryGetValue(userId, out var userInfo))
                {
                    userInfo.LastActivityTime = DateTime.UtcNow;
                }

                // If this is the current user, send an activity ping via WebSocket
                if (App.CurrentUser != null && App.CurrentUser.UserID == userId)
                {
                    await _webSocketService.SendActivityPingAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording activity for user {UserId}", userId);
            }
        }

        public async Task UpdateUserStatusAsync(int userId, UserPresenceStatus status)
        {
            if (userId <= 0)
            {
                _logger.LogWarning("Cannot update status for invalid user ID: {UserId}", userId);
                return;
            }

            try
            {
                // If this is the current user, update via WebSocket
                if (App.CurrentUser != null && App.CurrentUser.UserID == userId)
                {
                    await UpdateStatusAsync(status, string.Empty); // Use the existing method for the current user
                }
                else
                {
                    // For other users, just update the local cache
                    if (_userStatuses.TryGetValue(userId, out var userInfo))
                    {
                        userInfo.Status = status;

                        // Raise the status changed event
                        UserStatusChanged?.Invoke(this, new UserStatusChangedEventArgs
                        {
                            UserId = userId,
                            Username = userInfo.Username,
                            Status = status
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating status for user {UserId} to {Status}", userId, status);
            }
        }
    }
}