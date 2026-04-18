using System;
using System.Net;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebSockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TDFAPI.Configuration.Options;
using TDFAPI.Middleware;
using TDFAPI.Services;
using TDFShared.Models.Message;

namespace TDFAPI.Extensions.Startup
{
    /// <summary>
    /// WebSocket server registration plus the authenticated <c>/ws</c>
    /// endpoint that delegates to <see cref="TDFShared.Services.INotificationService"/>
    /// for the actual connection lifecycle.
    /// </summary>
    public static class WebSocketStartupExtensions
    {
        public static IServiceCollection AddTdfWebSockets(
            this IServiceCollection services,
            WebSocketSettings webSocketSettings)
        {
            services.AddWebSockets(options =>
            {
                options.KeepAliveInterval = TimeSpan.FromMinutes(webSocketSettings.KeepAliveMinutes);
            });
            return services;
        }

        public static WebApplication MapTdfWebSocketEndpoint(
            this WebApplication app,
            JwtOptions jwtOptions,
            ILogger logger)
        {
            var keyBytes = string.IsNullOrEmpty(jwtOptions.SecretKey)
                ? Array.Empty<byte>()
                : Encoding.ASCII.GetBytes(jwtOptions.SecretKey);

            var issuer = string.IsNullOrEmpty(jwtOptions.Issuer) ? "tdfapi" : jwtOptions.Issuer;
            var audience = string.IsNullOrEmpty(jwtOptions.Audience) ? "tdfapp" : jwtOptions.Audience;
            var requireHttps = !app.Environment.IsDevelopment();

            app.Map("/ws", async context =>
            {
                if (!context.WebSockets.IsWebSocketRequest)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    await context.Response.WriteAsync("Expected a WebSocket request");
                    return;
                }

                var wsAuthHelper = new WebSocketAuthenticationHelper(
                    logger, keyBytes, issuer, audience, requireHttps);

                var authToken = wsAuthHelper.ExtractTokenFromHeader(context);
                if (string.IsNullOrEmpty(authToken))
                {
                    await wsAuthHelper.WriteErrorResponse(
                        context,
                        HttpStatusCode.Unauthorized,
                        "Authentication token must be provided via Authorization header");
                    return;
                }

                try
                {
                    var validationResult = wsAuthHelper.ValidateToken(authToken);
                    var isValid = validationResult.isValid;
                    var principal = validationResult.principal;
                    var errorReason = validationResult.errorReason;

                    if (!isValid || principal == null)
                    {
                        await wsAuthHelper.WriteErrorResponse(context, HttpStatusCode.Unauthorized, errorReason);
                        return;
                    }

                    var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    var username = principal.FindFirst(ClaimTypes.Name)?.Value;

                    if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(username))
                    {
                        await wsAuthHelper.WriteErrorResponse(
                            context,
                            HttpStatusCode.Unauthorized,
                            "Invalid user information in token");
                        return;
                    }

                    logger.LogInformation(
                        "WebSocket connection authenticated for user {Username} (ID: {UserId})",
                        username, userId);

                    using var webSocket = await context.WebSockets.AcceptWebSocketAsync();

                    var wsManager = app.Services.GetRequiredService<WebSocketConnectionManager>();
                    var notificationService = app.Services.GetRequiredService<TDFShared.Services.INotificationService>();

                    var connection = new WebSocketConnectionEntity
                    {
                        ConnectionId = Guid.NewGuid().ToString(),
                        UserId = int.Parse(userId),
                        Username = username,
                        IsConnected = true,
                        ConnectedAt = DateTime.UtcNow,
                        MachineName = Environment.MachineName
                    };

                    try
                    {
                        await notificationService.HandleUserConnectionAsync(connection, webSocket);
                    }
                    catch (Exception wsEx)
                    {
                        logger.LogError(
                            wsEx,
                            "Error in WebSocket connection handling for user {Username}: {Message}",
                            username, wsEx.Message);

                        if (webSocket.State == WebSocketState.Open)
                        {
                            try
                            {
                                await webSocket.CloseAsync(
                                    WebSocketCloseStatus.InternalServerError,
                                    "Internal server error",
                                    System.Threading.CancellationToken.None);
                            }
                            catch
                            {
                                // Ignore errors during close
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    await context.Response.WriteAsync("Error processing WebSocket request");
                    logger.LogError(ex, "Error processing WebSocket request: {Message}", ex.Message);
                }
            });

            return app;
        }
    }
}
