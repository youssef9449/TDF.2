using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using System.Text.Json.Serialization;
using TDFShared.Enums;
using TDFShared.Services;

namespace TDFAPI.Services
{
    /// <summary>
    /// Implementation of the background job service using in-memory storage
    /// </summary>
    public class BackgroundJobService : BackgroundService, IBackgroundJobService
    {
        private readonly ILogger<BackgroundJobService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);
        private readonly JsonSerializerOptions _jsonOptions;
        
        // In-memory storage for jobs
        private readonly List<BackgroundJob> _jobs = new List<BackgroundJob>();
        private readonly SemaphoreSlim _jobsLock = new SemaphoreSlim(1, 1);

        public BackgroundJobService(
            ILogger<BackgroundJobService> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
        }

        public async Task ScheduleJobAsync(string jobType, Dictionary<string, object> data, DateTime scheduledTime)
        {
            try
            {
                await _jobsLock.WaitAsync();
                try
                {
                    var job = new BackgroundJob
                    {
                        Id = Guid.NewGuid().ToString(),
                        Type = jobType,
                        Data = data,
                        ScheduledTime = scheduledTime
                    };

                    _jobs.Add(job);
                    
                    _logger.LogInformation("Scheduled job {JobId} of type {JobType} for {ScheduledTime}", 
                        job.Id, jobType, scheduledTime);
                }
                finally
                {
                    _jobsLock.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scheduling job of type {JobType}", jobType);
                throw;
            }
        }

        public async Task DeleteJobAsync(string jobType, string jobId)
        {
            try
            {
                await _jobsLock.WaitAsync();
                try
                {
                    var job = _jobs.FirstOrDefault(j => j.Id == jobId && j.Type == jobType);
                    
                    if (job != null)
                    {
                        _jobs.Remove(job);
                        _logger.LogInformation("Deleted job {JobId} of type {JobType}", jobId, jobType);
                    }
                }
                finally
                {
                    _jobsLock.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting job {JobId} of type {JobType}", jobId, jobType);
                throw;
            }
        }

        public async Task<IEnumerable<BackgroundJob>> GetJobsAsync(string jobType, string identifier)
        {
            try
            {
                await _jobsLock.WaitAsync();
                try
                {
                    // Filter jobs by type and identifier in the data
                    return _jobs
                        .Where(j => j.Type == jobType && 
                                   j.Data.Any(d => d.Value?.ToString() == identifier))
                        .ToList();
                }
                finally
                {
                    _jobsLock.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving jobs of type {JobType} for identifier {Identifier}", 
                    jobType, identifier);
                throw;
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Background Job Service is starting");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessDueJobsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing due jobs");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }

            _logger.LogInformation("Background Job Service is stopping");
        }

        private async Task ProcessDueJobsAsync()
        {
            List<BackgroundJob> dueJobs = new List<BackgroundJob>();
            
            await _jobsLock.WaitAsync();
            try
            {
                var now = DateTime.UtcNow;
                dueJobs = _jobs.Where(j => j.ScheduledTime <= now).ToList();
                
                // Remove due jobs from the list
                foreach (var job in dueJobs)
                {
                    _jobs.Remove(job);
                }
            }
            finally
            {
                _jobsLock.Release();
            }

            foreach (var job in dueJobs)
            {
                try
                {
                    _logger.LogInformation("Processing job {JobId} of type {JobType}", job.Id, job.Type);
                    
                    // Process the job based on its type
                    var success = await ProcessJobAsync(job);
                    
                    _logger.LogInformation("Job {JobId} of type {JobType} processed with status {Status}", 
                        job.Id, job.Type, success ? "Success" : "Failed");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing job {JobId} of type {JobType}", job.Id, job.Type);
                }
            }
        }

        private async Task<bool> ProcessJobAsync(BackgroundJob job)
        {
            try
            {
                switch (job.Type)
                {
                    case "SendNotification":
                        return await ProcessSendNotificationJobAsync(job.Data);
                    // Add other job types here as needed
                    default:
                        _logger.LogWarning("Unknown job type: {JobType}", job.Type);
                        return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing job {JobId}", job.Id);
                return false;
            }
        }

        private async Task<bool> ProcessSendNotificationJobAsync(Dictionary<string, object> data)
        {
            try
            {
                // Extract notification data
                if (!data.TryGetValue("userId", out var userIdObj) || 
                    !data.TryGetValue("title", out var titleObj) || 
                    !data.TryGetValue("message", out var messageObj) || 
                    !data.TryGetValue("type", out var typeObj))
                {
                    _logger.LogError("Missing required data for SendNotification job");
                    return false;
                }

                // Convert data to appropriate types
                if (!int.TryParse(userIdObj.ToString(), out var userId))
                {
                    _logger.LogError("Invalid userId in SendNotification job: {UserId}", userIdObj);
                    return false;
                }

                var title = titleObj.ToString();
                var message = messageObj.ToString();
                var typeStr = typeObj.ToString();
                
                if (!Enum.TryParse<NotificationType>(typeStr, out var notificationType))
                {
                    notificationType = NotificationType.Info;
                }

                // Get optional data
                data.TryGetValue("data", out var additionalData);
                string? additionalDataStr = additionalData?.ToString();

                // Get notification service from service provider
                using (var scope = _serviceProvider.CreateScope())
                {
                    // Get the API notification service directly
                    var apiNotificationService = scope.ServiceProvider.GetRequiredService<TDFAPI.Services.INotificationService>();
                    
                    // Send the notification using the API notification service
                    await apiNotificationService.SendNotificationAsync(
                        userId,
                        title,
                        message,
                        notificationType,
                        additionalDataStr);
                    
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing SendNotification job");
                return false;
            }
        }
    }
}