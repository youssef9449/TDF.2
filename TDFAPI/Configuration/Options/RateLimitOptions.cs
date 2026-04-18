namespace TDFAPI.Configuration.Options
{
    /// <summary>
    /// Strongly-typed rate-limit configuration bound from the <c>RateLimiting</c>
    /// section. All limits are per-IP, per-minute.
    /// </summary>
    public class RateLimitOptions
    {
        public const string SectionName = "RateLimiting";

        /// <summary>Global cap applied to every request.</summary>
        public int GlobalLimitPerMinute { get; set; } = 100;

        /// <summary>Cap applied to the authentication endpoints.</summary>
        public int AuthLimitPerMinute { get; set; } = 10;

        /// <summary>Cap applied to regular API endpoints.</summary>
        public int ApiLimitPerMinute { get; set; } = 60;

        /// <summary>Cap applied to static-content endpoints.</summary>
        public int StaticLimitPerMinute { get; set; } = 200;
    }
}
