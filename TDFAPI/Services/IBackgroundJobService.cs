using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TDFAPI.Services
{
    /// <summary>
    /// Service for managing background jobs
    /// </summary>
    public interface IBackgroundJobService
    {
        /// <summary>
        /// Schedule a job to be executed at a specific time
        /// </summary>
        /// <param name="jobType">The type of job to schedule</param>
        /// <param name="data">The data to pass to the job</param>
        /// <param name="scheduledTime">When to execute the job</param>
        Task ScheduleJobAsync(string jobType, Dictionary<string, object> data, DateTime scheduledTime);

        /// <summary>
        /// Delete a scheduled job
        /// </summary>
        /// <param name="jobType">The type of job</param>
        /// <param name="jobId">The ID of the job to delete</param>
        Task DeleteJobAsync(string jobType, string jobId);

        /// <summary>
        /// Get all jobs of a specific type for a given identifier
        /// </summary>
        /// <param name="jobType">The type of job</param>
        /// <param name="identifier">The identifier to filter jobs by</param>
        Task<IEnumerable<BackgroundJob>> GetJobsAsync(string jobType, string identifier);
    }
} 