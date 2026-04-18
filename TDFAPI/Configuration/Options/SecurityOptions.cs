using System;

namespace TDFAPI.Configuration.Options
{
    /// <summary>
    /// Strongly-typed security configuration bound from the <c>Security</c> section.
    /// Covers account-lockout thresholds and the password-complexity policy.
    /// </summary>
    public class SecurityOptions
    {
        public const string SectionName = "Security";

        /// <summary>Number of consecutive failed login attempts before the account is locked.</summary>
        public int MaxFailedLoginAttempts { get; set; } = 5;

        /// <summary>How long an account stays locked once the failed-attempt threshold is hit.</summary>
        public int LockoutDurationMinutes { get; set; } = 15;

        /// <summary>Password complexity policy applied to new and reset passwords.</summary>
        public PasswordRequirementsOptions PasswordRequirements { get; set; } = new();

        /// <summary>Convenience wrapper over <see cref="LockoutDurationMinutes"/>.</summary>
        public TimeSpan LockoutDuration => TimeSpan.FromMinutes(LockoutDurationMinutes);
    }

    /// <summary>Password complexity policy.</summary>
    public class PasswordRequirementsOptions
    {
        public int MinimumLength { get; set; } = 12;
        public bool RequireUppercase { get; set; } = true;
        public bool RequireLowercase { get; set; } = true;
        public bool RequireDigit { get; set; } = true;
        public bool RequireSpecialCharacter { get; set; } = true;
    }
}
