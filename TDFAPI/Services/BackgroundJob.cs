using System;
using System.Collections.Generic;

namespace TDFAPI.Services
{
    /// <summary>
    /// Represents a background job to be processed
    /// </summary>
    public class BackgroundJob
    {
        /// <summary>
        /// Unique identifier for the job
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        /// <summary>
        /// Type of job (e.g., "SendNotification")
        /// </summary>
        public string Type { get; set; } = string.Empty;
        
        /// <summary>
        /// Data for the job
        /// </summary>
        public Dictionary<string, object> Data { get; set; } = new Dictionary<string, object>();
        
        /// <summary>
        /// When the job is scheduled to run
        /// </summary>
        public DateTime ScheduledTime { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// When the job was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// When the job started processing
        /// </summary>
        public DateTime? StartedAt { get; set; }
        
        /// <summary>
        /// When the job completed (successfully or with failure)
        /// </summary>
        public DateTime? CompletedAt { get; set; }
        
        /// <summary>
        /// Current status of the job
        /// </summary>
        public string Status { get; set; } = "Scheduled";
        
        /// <summary>
        /// Error message if the job failed
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}