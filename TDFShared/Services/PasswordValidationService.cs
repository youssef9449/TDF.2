using System;
using System.Linq;
using System.Collections.Generic;
using TDFShared.Exceptions;

namespace TDFShared.Services
{
    /// <summary>
    /// Service for validating password strength and requirements
    /// DEPRECATED: Use SecurityService.IsPasswordStrong() and SecurityService.ValidatePassword() instead
    /// This class is maintained for backward compatibility
    /// </summary>
    [Obsolete("Use SecurityService for password validation instead. This class will be removed in a future version.")]
    public static class PasswordValidationService
    {
        private static readonly ISecurityService _securityService = new SecurityService();

        /// <summary>
        /// Validates the strength of a password
        /// DEPRECATED: Use SecurityService.IsPasswordStrong() instead
        /// </summary>
        /// <param name="password">The password to validate</param>
        /// <param name="validationMessage">Message describing why the password is invalid, if it is invalid</param>
        /// <returns>True if password meets requirements, false otherwise</returns>
        [Obsolete("Use SecurityService.IsPasswordStrong() instead")]
        public static bool IsPasswordStrong(string password, out string validationMessage)
        {
            return _securityService.IsPasswordStrong(password, out validationMessage);
        }

        /// <summary>
        /// Validates a password and throws a ValidationException if it doesn't meet requirements
        /// DEPRECATED: Use SecurityService.ValidatePassword() instead
        /// </summary>
        /// <param name="password">The password to validate</param>
        /// <exception cref="ValidationException">Thrown when password does not meet requirements</exception>
        [Obsolete("Use SecurityService.ValidatePassword() instead")]
        public static void ValidatePassword(string password)
        {
            _securityService.ValidatePassword(password);
        }
    }
}
