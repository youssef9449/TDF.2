using System.Linq;
using System.Collections.Generic;
using TDFShared.Exceptions;

namespace TDFShared.Services
{
    /// <summary>
    /// Service for validating password strength and requirements
    /// </summary>
    public static class PasswordValidationService
    {
        /// <summary>
        /// Validates the strength of a password
        /// </summary>
        /// <param name="password">The password to validate</param>
        /// <param name="validationMessage">Message describing why the password is invalid, if it is invalid</param>
        /// <returns>True if password meets requirements, false otherwise</returns>
        public static bool IsPasswordStrong(string password, out string validationMessage)
        {
            validationMessage = string.Empty;
            if (string.IsNullOrEmpty(password))
            {
                validationMessage = "Password cannot be empty.";
                return false;
            }

            // Password must be at least 8 characters
            if (password.Length < 8)
            {
                validationMessage = "Password must be at least 8 characters long.";
                return false;
            }

            // Password must contain at least one uppercase letter
            if (!password.Any(char.IsUpper))
            {
                validationMessage = "Password must contain at least one uppercase letter.";
                return false;
            }

            // Password must contain at least one lowercase letter
            if (!password.Any(char.IsLower))
            {
                validationMessage = "Password must contain at least one lowercase letter.";
                return false;
            }

            // Password must contain at least one digit
            if (!password.Any(char.IsDigit))
            {
                validationMessage = "Password must contain at least one digit.";
                return false;
            }

            // Password must contain at least one special character
            if (!password.Any(c => !char.IsLetterOrDigit(c)))
            {
                validationMessage = "Password must contain at least one special character (e.g., !@#$).";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validates a password and throws a ValidationException if it doesn't meet requirements
        /// </summary>
        /// <param name="password">The password to validate</param>
        /// <exception cref="ValidationException">Thrown when password does not meet requirements</exception>
        public static void ValidatePassword(string password)
        {
            if (!IsPasswordStrong(password, out string message))
            {
                throw new ValidationException(message);
            }
        }
    }
}
