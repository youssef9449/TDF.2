using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using TDFMAUI.Helpers;
using TDFShared.Enums;
using TDFShared.DTOs;
using TDFShared.DTOs.Users;
using TDFShared.DTOs.Common;
using TDFShared.Exceptions;
using TDFShared.Constants;

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
                _logger.LogInformation("Attempting to fetch all users with status from API endpoint: users/all");
                // Get all users with their status from the API as a paginated result (wrapped in ApiResponse)
                var apiResponse = await _apiService.GetAsync<ApiResponse<PaginatedResult<UserDto>>>(ApiRoutes.Users.GetAllWithStatus);
                if (apiResponse == null || !apiResponse.Success || apiResponse.Data == null || apiResponse.Data.Items == null)
                {
                    _logger.LogWarning("API returned null or unsuccessful response for users");
                    return new Dictionary<int, UserPresenceInfo>();
                }

                var result = new Dictionary<int, UserPresenceInfo>();
                foreach (var user in apiResponse.Data.Items)
                {
                    result[user.UserID] = new UserPresenceInfo
                    {
                        UserId = user.UserID,
                        Username = user.UserName,
                        FullName = user.FullName,
                        Department = user.Department,
                        Status = user.PresenceStatus,
                        StatusMessage = user.StatusMessage,
                        IsAvailableForChat = user.IsAvailableForChat ?? false,
                        ProfilePictureData = user.Picture
                    };
                }

                // Update the cache
                _userStatuses.Clear();
                foreach (var kvp in result)
                {
                    _userStatuses[kvp.Key] = kvp.Value;
                }

                _logger.LogInformation("Successfully fetched {Count} users with status", result.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching users from API");
                throw;
            }
        }

        private Dictionary<int, UserPresenceInfo> GetCachedUsers()
        {
            _logger.LogInformation("Returning cached user statuses. Cache contains {Count} users", _userStatuses.Count);
            return _userStatuses.ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        public async Task UpdateStatusAsync(UserPresenceStatus status, string statusMessage, CancellationToken cancellationToken = default)
        {
            if (App.CurrentUser == null)
            {
                _logger.LogWarning("Cannot update status: Current user is null");
                return;
            }

            if (App.CurrentUser.UserID <= 0)
            {
                _logger.LogWarning("Cannot update status: Current user has invalid ID ({UserId})", App.CurrentUser.UserID);
                return;
            }

            try
            {
                // Check for cancellation before making the call
                cancellationToken.ThrowIfCancellationRequested();

                // Use a timeout if none is provided
                var timeoutToken = CancellationToken.None;
                if (cancellationToken == CancellationToken.None)
                {
                    var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    timeoutToken = cts.Token;
                }

                // Use the provided token or the timeout token
                var effectiveToken = cancellationToken != CancellationToken.None ? cancellationToken : timeoutToken;

                // Send the status update via WebSocket, only if connected
                if (_webSocketService.IsConnected)
                {
                    await _webSocketService.UpdatePresenceStatusAsync(status.ToString(), statusMessage);
                    _logger.LogInformation("Status update sent via WebSocket");
                }
                else
                {
                    _logger.LogWarning("WebSocket is not connected, will update via API instead");
                }

                // Always make an API call to ensure the database is updated, especially when WebSocket is not available
                int userId = App.CurrentUser.UserID;
                try
                {
                    var updateData = new { isConnected = status != UserPresenceStatus.Offline };
                    await _apiService.PutAsync<object, object>(
                        string.Format(ApiRoutes.Users.UpdateConnection, userId), 
                        updateData, 
                        false); // Don't queue if unavailable during shutdown
                    _logger.LogInformation("Status update sent via API for user {UserId}", userId);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("API status update was cancelled");
                    // Don't rethrow cancellation for API calls during shutdown
                }
                catch (Exception apiEx)
                {
                    _logger.LogError(apiEx, "Failed to update status via API, but continuing with local update");
                    // Don't fail the entire operation if API call fails
                }

                // Update local cache
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
                        Username = App.CurrentUser.UserName,
                        Status = status,
                        StatusMessage = statusMessage,
                        LastActivityTime = DateTime.UtcNow
                    };
                }

                _logger.LogInformation("Successfully updated user status to {Status}", status);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Status update operation was cancelled");
                throw; // Rethrow to allow caller to handle cancellation
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating presence status to {Status}", status);
                throw; // Rethrow to allow caller to handle the error
            }
        }

        public async Task SetAvailabilityForChatAsync(bool isAvailable)
        {
            if (App.CurrentUser == null)
            {
                _logger.LogWarning("Cannot update chat availability: Current user is null");
                return;
            }

            if (App.CurrentUser.UserID <= 0)
            {
                _logger.LogWarning("Cannot update chat availability: Current user has invalid ID ({UserId})", App.CurrentUser.UserID);
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

        /// <summary>
        /// Gets the currently cached online users information for offline usage
        /// </summary>
        /// <returns>Dictionary of user IDs mapped to their presence information</returns>
        public Dictionary<int, UserPresenceInfo> GetCachedOnlineUsers()
        {
            _logger.LogInformation("Returning cached user statuses for offline mode");
            return _userStatuses.ToDictionary(kv => kv.Key, kv => kv.Value);
        }
    }
}