using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using TDFShared.Constants;
using TDFShared.DTOs.Common;
using System.Reflection;
using System.Text.Json;


namespace TDFAPI.Controllers
{
    [Route(ApiRoutes.Health.Base)]
    [ApiController]
    public class HealthCheckController : ControllerBase
    {
        private readonly ILogger<HealthCheckController> _logger;
        private readonly IWebHostEnvironment _env;

        public HealthCheckController(
            ILogger<HealthCheckController> logger,
            IWebHostEnvironment env)
        {
            _logger = logger;
            _env = env;
        }

        /// <summary>
        /// Simple health check endpoint that doesn't require authentication
        /// </summary>
        [HttpGet]
        [Route(ApiRoutes.Health.GetDefault)]
        [AllowAnonymous]
        public IActionResult Get()
        {
            _logger.LogInformation("Health check requested");

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "API is operational",
                Data = new
                {
                    Status = "Healthy",
                    Timestamp = DateTime.UtcNow,
                    Environment = _env.EnvironmentName
                }
            });
        }

        /// <summary>
        /// Simple ping endpoint that doesn't require authentication
        /// </summary>
        [HttpGet("ping")]
        [Route(ApiRoutes.Health.Ping)]
        [AllowAnonymous]
        public IActionResult Ping()
        {
            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "Pong",
                Data = new
                {
                    Timestamp = DateTime.UtcNow
                }
            });
        }

        /// <summary>
        /// Detailed health check that requires authentication
        /// </summary>
        [HttpGet("detailed")]
        [Route(ApiRoutes.Health.Detailed)]
        [Authorize]
        public IActionResult GetDetailed()
        {
            _logger.LogInformation("Detailed health check requested");

            var assembly = Assembly.GetExecutingAssembly();
            var version = (assembly.GetName()?.Version is Version v) ? v.ToString() : "N/A";

            var healthInfo = new
            {
                Status = "Healthy",
                Version = version,
                Timestamp = DateTime.UtcNow,
                Environment = _env.EnvironmentName,
                MemoryUsage = GetMemoryUsage(),
                ProcessUptime = GetProcessUptime(),
                DatabaseStatus = "Connected", // This could be enhanced to actually test the DB connection
                Server = new
                {
                    OperatingSystem = Environment.OSVersion.ToString(),
                    MachineName = Environment.MachineName,
                    ProcessorCount = Environment.ProcessorCount,
                    DotNetVersion = Environment.Version.ToString()
                }
            };

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "API is operational",
                Data = healthInfo
            });
        }

        /// <summary>
        /// Echo endpoint for testing request/response
        /// </summary>
        [HttpPost("echo")]
        [AllowAnonymous]
        public IActionResult Echo([FromBody] object data)
        {
            _logger.LogInformation("Echo request received");

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "Echo response",
                Data = new
                {
                    Original = data,
                    Headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()),
                    Timestamp = DateTime.UtcNow
                }
            });
        }

        private string GetMemoryUsage()
        {
            // Get memory usage in MB
            long memoryBytes = GC.GetTotalMemory(false);
            double memoryMB = Math.Round(memoryBytes / 1024.0 / 1024.0, 2);
            return $"{memoryMB} MB";
        }

        private string GetProcessUptime()
        {
            // Get process uptime
            var process = System.Diagnostics.Process.GetCurrentProcess();
            var uptime = DateTime.Now - process.StartTime;
            return $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s";
        }
    }
}