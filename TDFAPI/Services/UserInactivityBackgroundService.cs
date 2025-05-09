using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace TDFAPI.Services
{
    /// <summary>
    /// Background service that periodically checks for inactive users and updates their status
    /// </summary>
    public class UserInactivityBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<UserInactivityBackgroundService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5); // Run every 5 minutes

        public UserInactivityBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<UserInactivityBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("User inactivity check service is starting");

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogDebug("Checking for inactive users");
                
                try
                {
                    // Create a new scope for the service
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        // Get the user presence service
                        var userPresenceService = scope.ServiceProvider.GetRequiredService<IUserPresenceService>();
                        
                        // Check for inactive users and update their status
                        await userPresenceService.CheckInactiveUsersAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while checking for inactive users");
                }

                // Wait for the next interval or until cancellation is requested
                try
                {
                    await Task.Delay(_checkInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break; // Exit if task was canceled
                }
            }

            _logger.LogInformation("User inactivity check service is stopping");
        }
    }
}