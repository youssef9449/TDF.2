using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using TDFAPI.Services;
using TDFShared.Enums;
using TDFShared.Models.Message;
using Microsoft.Extensions.Hosting;
using TDFAPI.Extensions;

namespace TDFAPI.Middleware
{
    public static class WebSocketMiddlewareExtensions
    {
        public static IApplicationBuilder UseWebSocketHandler(this IApplicationBuilder app)
        {
            return app.UseMiddleware<WebSocketMiddleware>();
        }
    }

    public class WebSocketMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<WebSocketMiddleware> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IHostEnvironment _environment;

        public WebSocketMiddleware(RequestDelegate next, ILogger<WebSocketMiddleware> logger, IServiceProvider serviceProvider, IHostEnvironment environment)
        {
            _next = next;
            _logger = logger;
            _serviceProvider = serviceProvider;
            _environment = environment;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                // Extract user information from the token or query string
                var (userId, isValid, errorMessage) = GetUserIdFromRequest(context);
                if (!isValid || userId <= 0)
                {
                    context.Response.StatusCode = 401; // Unauthorized
                    await context.Response.WriteAsync(errorMessage ?? "Authentication required");
                    return;
                }

                // Additional security check - verify IP against recent login records
                if (!await IsValidUserConnection(userId, context.GetRealIpAddress()))
                {
                    context.Response.StatusCode = 403; // Forbidden
                    await context.Response.WriteAsync("Connection from unknown location detected");
                    _logger.LogWarning("Blocked suspicious WebSocket connection attempt for user {UserId} from IP {IP}",
                        userId, context.GetRealIpAddress());
                    return;
                }

                var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                _logger.LogInformation("WebSocket connection established for user {UserId}", userId);

                // Create connection info
                var connection = new WebSocketConnectionEntity
                {
                    ConnectionId = Guid.NewGuid().ToString(),
                    UserId = userId,
                    Username = await GetUsernameFromUserIdAsync(userId),
                    ConnectedAt = DateTime.UtcNow,
                    MachineName = context.Request.Headers.ContainsKey("User-Agent")
                        ? context.Request.Headers["User-Agent"].ToString()
                        : "Unknown"
                };

                // Handle the WebSocket connection
                await HandleWebSocketAsync(webSocket, connection, context);
            }
            else
            {
                await _next(context);
            }
        }

        private async Task HandleWebSocketAsync(WebSocket webSocket, WebSocketConnectionEntity connection, HttpContext context)
        {
            // Use CancellationTokenSource to manage WebSocket lifetime
            using var cts = new CancellationTokenSource();
            try
            {
                // Set up a timeout for idle connections
                cts.CancelAfter(TimeSpan.FromMinutes(30)); // 30 minutes max lifetime

                // Use the notification service to handle the WebSocket connection
                using (var scope = _serviceProvider.CreateScope())
                {
                    var notificationService = scope.ServiceProvider.GetRequiredService<TDFShared.Services.INotificationService>();

                    // Create a task to monitor for unexpected disconnection
                    var connectionTask = notificationService.HandleUserConnectionAsync(connection, webSocket);

                    // Wait for either task completion or cancellation
                    await connectionTask;
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("WebSocket connection timeout for user {UserId}", connection.UserId);
            }
            catch (WebSocketException ex)
            {
                _logger.LogWarning(ex, "WebSocket error for user {UserId}: {Message}",
                    connection.UserId, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling WebSocket connection for user {UserId}", connection.UserId);
            }
            finally
            {
                if (webSocket.State == WebSocketState.Open)
                {
                    // Always try to close gracefully
                    try
                    {
                        await webSocket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "Connection closed",
                            CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error closing WebSocket for user {UserId}", connection.UserId);
                    }
                }

                // Notify services about disconnection
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var userPresenceService = scope.ServiceProvider.GetRequiredService<IUserPresenceService>();
                    await userPresenceService.UpdateStatusAsync(connection.UserId, UserPresenceStatus.Offline);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error updating user status on WebSocket closure for user {UserId}",
                        connection.UserId);
                }
            }
        }

        private (int userId, bool isValid, string? errorMessage) GetUserIdFromRequest(HttpContext context)
        {
            try
            {
                // Try to get userId from query string only in development environment
                if (_environment.IsDevelopment() &&
                    context.Request.Query.TryGetValue("userId", out var userIdStr) &&
                    int.TryParse(userIdStr, out var testUserId))
                {
                    return (testUserId, true, null);
                }

                // In production, always use claims from the JWT token
                if (context.User?.Identity?.IsAuthenticated != true)
                {
                    return (-1, false, "Authentication required");
                }

                var userIdClaim = context.User.FindFirst("sub") ?? context.User.FindFirst("userId");
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
                {
                    return (-1, false, "Invalid user identity");
                }

                return (userId, true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting user ID from request");
                return (-1, false, "Error processing authentication");
            }
        }

        private async Task<bool> IsValidUserConnection(int userId, string? ipAddress)
        {
            if (string.IsNullOrEmpty(ipAddress))
                return false;

            // In development environment, always allow connections
            if (_environment.IsDevelopment())
            {
                _logger.LogWarning("Allowing WebSocket connection in development mode for user {UserId} from IP {IP}",
                    userId, ipAddress);
                return true;
            }

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var userRepository = scope.ServiceProvider.GetRequiredService<Repositories.IUserRepository>();

                // Check if this IP has been used by this user recently
                return await userRepository.IsKnownIpAddressAsync(userId, ipAddress);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating user connection for user {UserId} from IP {IP}",
                    userId, ipAddress);
                // Default to DENYING the connection if we can't validate for security
                return false;
            }
        }

        private async Task<string> GetUsernameFromUserIdAsync(int userId)
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var userRepository = scope.ServiceProvider.GetRequiredService<Repositories.IUserRepository>();
                    var user = await userRepository.GetByIdAsync(userId);
                    return user?.UserName ?? "Unknown";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting username for user {UserId}", userId);
                return "Unknown";
            }
        }

        internal async Task HandlePresenceUpdateAsync(JsonElement root, WebSocketConnectionEntity connection, HttpContext context)
        {
            try
            {
                string status = "Online";
                string statusMessage = null;

                if (root.TryGetProperty("status", out var statusElement) &&
                    statusElement.ValueKind == JsonValueKind.String)
                {
                    status = statusElement.GetString();
                }

                if (root.TryGetProperty("statusMessage", out var messageElement) &&
                    messageElement.ValueKind == JsonValueKind.String)
                {
                    statusMessage = messageElement.GetString();
                }

                // Parse status to enum
                if (Enum.TryParse<UserPresenceStatus>(status, true, out var presenceStatus))
                {
                    var userPresenceService = _serviceProvider.GetRequiredService<IUserPresenceService>();
                    await userPresenceService.UpdateStatusAsync(connection.UserId, presenceStatus, statusMessage);
                }
                else
                {
                    _logger.LogWarning("Invalid presence status: {Status}", status);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling presence update for user {UserId}: {Message}",
                    connection.UserId, ex.Message);
            }
        }

        internal async Task HandleChatAvailabilityAsync(JsonElement root, WebSocketConnectionEntity connection, HttpContext context)
        {
            try
            {
                bool isAvailable = true;

                if (root.TryGetProperty("isAvailable", out var availableElement) &&
                    (availableElement.ValueKind == JsonValueKind.True ||
                     availableElement.ValueKind == JsonValueKind.False))
                {
                    isAvailable = availableElement.GetBoolean();
                }

                var userPresenceService = _serviceProvider.GetRequiredService<IUserPresenceService>();
                await userPresenceService.SetAvailabilityForChatAsync(connection.UserId, isAvailable);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling chat availability update for user {UserId}: {Message}",
                    connection.UserId, ex.Message);
            }
        }
    }
}