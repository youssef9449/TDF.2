using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using TDFShared.Services;
using TDFShared.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TDFShared.Validation
{
    /// <summary>
    /// Comprehensive validation service providing unified validation patterns
    /// for both client and server-side validation
    /// </summary>
    public class ValidationService : IValidationService
    {
        private readonly ISecurityService _securityService;
        private static readonly Regex EmailRegex = new(
            @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Initializes a new instance of the ValidationService class.
        /// </summary>
        /// <param name="securityService">The security service used for password validation.</param>
        /// <exception cref="ArgumentNullException">Thrown when securityService is null.</exception>
        public ValidationService(ISecurityService securityService)
        {
            _securityService = securityService ?? throw new ArgumentNullException(nameof(securityService));
        }

        /// <summary>
        /// Validates an object using data annotations and returns a validation result.
        /// </summary>
        /// <typeparam name="T">The type of object to validate.</typeparam>
        /// <param name="obj">The object to validate.</param>
        /// <returns>A ValidationResult containing the validation status and any error messages.</returns>
        public ValidationResult<T> ValidateObject<T>(T obj) where T : class
        {
            if (obj == null)
                return ValidationResult<T>.Failure("Object cannot be null");

            var validationResults = new List<ValidationResult>();
            var validationContext = new ValidationContext(obj);

            bool isValid = Validator.TryValidateObject(obj, validationContext, validationResults, true);

            if (isValid)
                return ValidationResult<T>.Success(obj);

            var errors = validationResults
                .Where(vr => !string.IsNullOrEmpty(vr.ErrorMessage))
                .Select(vr => vr.ErrorMessage!)
                .ToList();

            return ValidationResult<T>.Failure(errors);
        }

        /// <summary>
        /// Validates an object and throws a ValidationException if validation fails.
        /// </summary>
        /// <typeparam name="T">The type of object to validate.</typeparam>
        /// <param name="obj">The object to validate.</param>
        /// <exception cref="ValidationException">Thrown when validation fails.</exception>
        public void ValidateAndThrow<T>(T obj) where T : class
        {
            var result = ValidateObject(obj);
            if (!result.IsValid)
            {
                throw new System.ComponentModel.DataAnnotations.ValidationException(string.Join("; ", result.Errors));
            }
        }

        /// <summary>
        /// Validates a specific property of an object using its validation attributes.
        /// </summary>
        /// <param name="value">The value to validate.</param>
        /// <param name="propertyName">The name of the property being validated.</param>
        /// <param name="objectType">The type of the object containing the property.</param>
        /// <returns>A list of validation error messages, if any.</returns>
        public List<string> ValidateProperty(object? value, string propertyName, Type objectType)
        {
            var errors = new List<string>();
            var validationContext = new ValidationContext(new object())
            {
                MemberName = propertyName
            };

            var validationResults = new List<ValidationResult>();

            // Get validation attributes for the property
            var property = objectType.GetProperty(propertyName);
            if (property != null)
            {
                var attributes = property.GetCustomAttributes(typeof(ValidationAttribute), true)
                    .Cast<ValidationAttribute>();

                foreach (var attribute in attributes)
                {
                    if (!attribute.IsValid(value))
                    {
                        var errorMessage = attribute.FormatErrorMessage(propertyName);
                        errors.Add(errorMessage);
                    }
                }
            }

            return errors;
        }

        /// <summary>
        /// Validates that a required string value is not null or empty.
        /// </summary>
        /// <param name="value">The value to validate.</param>
        /// <param name="fieldName">The name of the field being validated.</param>
        /// <param name="customMessage">Optional custom error message.</param>
        /// <returns>An error message if validation fails, null if validation succeeds.</returns>
        public string? ValidateRequired(string? value, string fieldName, string? customMessage = null)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return customMessage ?? $"{fieldName} is required and cannot be empty.";
            }
            return null;
        }

        /// <summary>
        /// Validates that a string's length is within specified bounds.
        /// </summary>
        /// <param name="value">The string to validate.</param>
        /// <param name="fieldName">The name of the field being validated.</param>
        /// <param name="minLength">Optional minimum length requirement.</param>
        /// <param name="maxLength">Optional maximum length requirement.</param>
        /// <returns>An error message if validation fails, null if validation succeeds.</returns>
        public string? ValidateStringLength(string? value, string fieldName, int? minLength = null, int? maxLength = null)
        {
            if (string.IsNullOrEmpty(value))
                return null; // Use ValidateRequired for null/empty checks

            var length = value.Length;

            if (minLength.HasValue && length < minLength.Value)
            {
                return $"{fieldName} must be at least {minLength.Value} characters long.";
            }

            if (maxLength.HasValue && length > maxLength.Value)
            {
                return $"{fieldName} cannot exceed {maxLength.Value} characters.";
            }

            return null;
        }

        /// <summary>
        /// Validates that a numeric value is within specified bounds.
        /// </summary>
        /// <param name="value">The value to validate.</param>
        /// <param name="fieldName">The name of the field being validated.</param>
        /// <param name="min">Optional minimum value.</param>
        /// <param name="max">Optional maximum value.</param>
        /// <returns>An error message if validation fails, null if validation succeeds.</returns>
        public string? ValidateRange(int value, string fieldName, int? min = null, int? max = null)
        {
            if (min.HasValue && value < min.Value)
            {
                return $"{fieldName} must be at least {min.Value}.";
            }

            if (max.HasValue && value > max.Value)
            {
                return $"{fieldName} cannot exceed {max.Value}.";
            }

            return null;
        }

        /// <summary>
        /// Validates a date range, ensuring dates are in the future and end date is after start date.
        /// </summary>
        /// <param name="startDate">The start date of the range.</param>
        /// <param name="endDate">Optional end date of the range.</param>
        /// <param name="fieldName">The name of the field being validated.</param>
        /// <param name="minDaysFromNow">Minimum number of days from today required.</param>
        /// <returns>A list of validation error messages, if any.</returns>
        public List<string> ValidateDateRange(DateTime startDate, DateTime? endDate, string fieldName = "Date", int minDaysFromNow = 0)
        {
            var errors = new List<string>();

            // Use the same logic as FutureDateAttribute for consistency
            if (startDate.Date < DateTime.Today.AddDays(minDaysFromNow))
            {
                var message = minDaysFromNow > 0
                    ? $"{fieldName} must be at least {minDaysFromNow} day(s) from today."
                    : $"{fieldName} cannot be in the past.";
                errors.Add(message);
            }

            // Validate end date is after start date
            if (endDate.HasValue && endDate.Value.Date < startDate.Date)
            {
                errors.Add("End date must be on or after the start date.");
            }

            return errors;
        }

        /// <summary>
        /// Validates a password for strength and security requirements.
        /// </summary>
        /// <param name="password">The password to validate.</param>
        /// <returns>A PasswordValidationResult containing validation status, errors, and strength assessment.</returns>
        public PasswordValidationResult ValidatePassword(string? password)
        {
            if (string.IsNullOrEmpty(password))
            {
                return new PasswordValidationResult
                {
                    IsValid = false,
                    Errors = new List<string> { "Password is required." },
                    Strength = PasswordStrength.VeryWeak
                };
            }

            // Use the security service for password validation
            bool isStrong = _securityService.IsPasswordStrong(password, out string validationMessage);

            var result = new PasswordValidationResult
            {
                IsValid = isStrong,
                StrengthMessage = validationMessage
            };

            if (!isStrong)
            {
                result.Errors.Add(validationMessage);
            }

            // Calculate password strength
            result.Strength = CalculatePasswordStrength(password);

            return result;
        }

        private PasswordStrength CalculatePasswordStrength(string password)
        {
            if (string.IsNullOrEmpty(password))
                return PasswordStrength.VeryWeak;

            int score = 0;

            // Length scoring
            if (password.Length >= 8) score++;
            if (password.Length >= 12) score++;
            if (password.Length >= 16) score++;

            // Character variety scoring
            if (password.Any(char.IsLower)) score++;
            if (password.Any(char.IsUpper)) score++;
            if (password.Any(char.IsDigit)) score++;
            if (password.Any(c => !char.IsLetterOrDigit(c))) score++;

            // Pattern complexity
            if (HasNoRepeatingPatterns(password)) score++;

            return score switch
            {
                0 or 1 => PasswordStrength.VeryWeak,
                2 or 3 => PasswordStrength.Weak,
                4 or 5 => PasswordStrength.Fair,
                6 or 7 => PasswordStrength.Good,
                8 => PasswordStrength.Strong,
                _ => PasswordStrength.VeryStrong
            };
        }

        private static bool HasNoRepeatingPatterns(string password)
        {
            // Check for simple repeating patterns
            for (int i = 0; i < password.Length - 2; i++)
            {
                if (password[i] == password[i + 1] && password[i + 1] == password[i + 2])
                {
                    return false; // Found 3 consecutive identical characters
                }
            }

            // Check for sequential patterns (abc, 123, etc.)
            for (int i = 0; i < password.Length - 2; i++)
            {
                if (password[i] + 1 == password[i + 1] && password[i + 1] + 1 == password[i + 2])
                {
                    return false; // Found ascending sequence
                }
                if (password[i] - 1 == password[i + 1] && password[i + 1] - 1 == password[i + 2])
                {
                    return false; // Found descending sequence
                }
            }

            return true;
        }

        /// <summary>
        /// Sanitizes input string by removing potentially dangerous content.
        /// </summary>
        /// <param name="input">The input string to sanitize.</param>
        /// <param name="allowHtml">Whether to allow HTML content in the output.</param>
        /// <returns>A sanitized version of the input string.</returns>
        public string SanitizeInput(string? input, bool allowHtml = false)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            var sanitized = input.Trim();

            if (!allowHtml)
            {
                // Remove HTML tags
                sanitized = Regex.Replace(sanitized, @"<[^>]*>", string.Empty);
            }

            // Remove potentially dangerous characters for SQL injection
            sanitized = sanitized.Replace("'", "''"); // Escape single quotes
            sanitized = Regex.Replace(sanitized, @"--.*$", string.Empty, RegexOptions.Multiline); // Remove SQL comments
            sanitized = Regex.Replace(sanitized, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline); // Remove SQL block comments

            // Remove script injection attempts
            sanitized = Regex.Replace(sanitized, @"<script\b[^<]*(?:(?!<\/script>)<[^<]*)*<\/script>", string.Empty, RegexOptions.IgnoreCase);
            sanitized = Regex.Replace(sanitized, @"javascript:", string.Empty, RegexOptions.IgnoreCase);
            sanitized = Regex.Replace(sanitized, @"vbscript:", string.Empty, RegexOptions.IgnoreCase);
            sanitized = Regex.Replace(sanitized, @"onload", string.Empty, RegexOptions.IgnoreCase);
            sanitized = Regex.Replace(sanitized, @"onerror", string.Empty, RegexOptions.IgnoreCase);

            return sanitized;
        }

        /// <summary>
        /// Checks if a string contains potentially dangerous patterns.
        /// </summary>
        /// <param name="input">The input string to check.</param>
        /// <returns>True if dangerous patterns are found, false otherwise.</returns>
        public bool ContainsDangerousPatterns(string? input)
        {
            if (string.IsNullOrEmpty(input))
                return false;

            var dangerousPatterns = new[]
            {
                @"<script\b[^<]*(?:(?!<\/script>)<[^<]*)*<\/script>", // Script tags
                @"javascript:", // JavaScript protocol
                @"vbscript:", // VBScript protocol
                @"data:text/html", // Data URLs with HTML
                @"(?:'|--|\b(select|union|insert|drop|alter|declare|xp_)\b)", // SQL injection patterns
                @"(\b(exec|execute|sp_|xp_)\b)", // SQL stored procedure calls
                @"(\b(cmd|command|shell|system)\b)", // Command injection patterns
                @"(\.\./|\.\.\\)", // Directory traversal
                @"(%2e%2e%2f|%2e%2e%5c)", // URL encoded directory traversal
            };

            return dangerousPatterns.Any(pattern =>
                Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled));
        }
    }
}
