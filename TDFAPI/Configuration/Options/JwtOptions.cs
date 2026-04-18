namespace TDFAPI.Configuration.Options
{
    /// <summary>
    /// Strongly-typed JWT configuration bound from the <c>Jwt</c> section.
    /// </summary>
    public class JwtOptions
    {
        public const string SectionName = "Jwt";

        /// <summary>
        /// Symmetric signing key. Must be at least 32 characters long.
        /// Prefer supplying via the <c>JWT_SECRET_KEY</c> environment variable
        /// rather than a configuration file.
        /// </summary>
        public string SecretKey { get; set; } = string.Empty;

        public string Issuer { get; set; } = "TDFAPI";
        public string Audience { get; set; } = "TDFClient";

        /// <summary>Lifetime of issued access tokens, in minutes.</summary>
        public int TokenValidityInMinutes { get; set; } = 60;

        /// <summary>Lifetime of issued refresh tokens, in days.</summary>
        public int RefreshTokenValidityInDays { get; set; } = 7;
    }
}
