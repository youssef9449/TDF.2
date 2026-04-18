using System.Collections.Generic;

namespace TDFAPI.Configuration.Options
{
    /// <summary>
    /// Strongly-typed CORS configuration. Bound from the <c>Cors</c> section
    /// when present, otherwise from the root-level <c>AllowedOrigins</c> and
    /// <c>DevelopmentAllowedOrigins</c> entries for backward compatibility
    /// with the existing <c>appsettings.json</c> / <c>config.ini</c> shape.
    /// </summary>
    public class CorsOptions
    {
        public const string SectionName = "Cors";

        /// <summary>
        /// Origins allowed when running outside of <c>Development</c>. The server
        /// will refuse to start in production if this list is empty.
        /// </summary>
        public List<string> AllowedOrigins { get; set; } = new();

        /// <summary>
        /// Origins allowed when <c>ASPNETCORE_ENVIRONMENT=Development</c>.
        /// If empty, a sensible default set of local dev server URLs is used.
        /// </summary>
        public List<string> DevelopmentAllowedOrigins { get; set; } = new();
    }
}
