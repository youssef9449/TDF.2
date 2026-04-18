using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.WebSockets;
using TDFAPI.Middleware;
using TDFShared.Constants;

namespace TDFAPI.Extensions.Startup
{
    /// <summary>
    /// Central place where the middleware pipeline is composed. The order of
    /// these calls matters and mirrors the order previously hard-coded in
    /// Program.cs.
    /// </summary>
    public static class RequestPipelineExtensions
    {
        public static WebApplication UseTdfRequestPipeline(this WebApplication app)
        {
            // Capture real client IPs as early as possible.
            app.UseForwardedHeaders();

            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.UseResponseCompression();
            app.UseSecurityHeaders();
            app.UseRateLimiter();
            app.UseMiddleware<GlobalExceptionMiddleware>();
            app.UseSecurityMonitoring();
            app.UseMiddleware<RequestLoggingMiddleware>();

            app.UseHttpsRedirection();
            app.UseCors("CorsPolicy");
            app.UseRouting();

            var webSocketOptions = new WebSocketOptions
            {
                KeepAliveInterval = TimeSpan.FromMinutes(2)
            };
            app.UseWebSockets(webSocketOptions);
            app.UseWebSocketHandler();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            return app;
        }
    }
}
