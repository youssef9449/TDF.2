using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using TDFShared.Services;
using TDFShared.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using TDFShared.Validation.Results;
using System.Threading.Tasks;

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
        public ObjectValidationResult<T> ValidateObject<T>(T obj) where T : class
        {
            if (obj == null)
            {
                var nullResult = new ObjectValidationResult<T>();
                nullResult.IsValid = false;
                nullResult.Errors.Add("Object cannot be null");
                return nullResult;
            }

            var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
            var validationContext = new ValidationContext(obj);

            bool isValid = Validator.TryValidateObject(obj, validationContext, validationResults, true);

            if (isValid)
                return ObjectValidationResult<T>.Success(obj);

            var errors = validationResults
                .Where(vr => !string.IsNullOrEmpty(vr.ErrorMessage))
                .Select(vr => vr.ErrorMessage!)
                .ToList();

            var errorResult = new ObjectValidationResult<T>();
            errorResult.IsValid = false;
            errorResult.Errors.AddRange(errors);
            return errorResult;
        }

        /// <summary>
        /// Validates an object and throws a ValidationException if validation fails.
        /// </summary>
        /// <typeparam name="T">The type of object to validate.</typeparam>
        /// <param name="obj">The object to validate.</param>
        /// <exception cref="TDFShared.Exceptions.ValidationException">Thrown when validation fails.</exception>
        public void ValidateAndThrow<T>(T obj) where T : class
        {
            var result = ValidateObject(obj);
            if (!result.IsValid)
            {
                throw new TDFShared.Exceptions.ValidationException(string.Join("; ", result.Errors));
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

            var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

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
            var minDate = DateTime.Today.AddDays(minDaysFromNow);
            if (startDate.Date < minDate)
            {
                errors.Add($"{fieldName} must be at least {minDaysFromNow} days from today.");
            }

            if (endDate.HasValue)
            {
                if (endDate.Value.Date < startDate.Date)
                {
                    errors.Add($"{fieldName} end date must be after start date.");
                }
            }

            return errors;
        }

        /// <summary>
        /// Validates a password against security requirements.
        /// </summary>
        /// <param name="password">The password to validate.</param>
        /// <returns>A PasswordValidationResult containing validation status, errors, and strength assessment.</returns>
        public PasswordValidationResult ValidatePassword(string? password)
        {
            var result = new PasswordValidationResult();

            if (string.IsNullOrEmpty(password))
            {
                result.IsValid = false;
                result.Errors.Add("Password is required.");
                result.Strength = PasswordStrength.VeryWeak;
                result.StrengthMessage = "Password is empty.";
                return result;
            }

            // Basic requirements
            var meetsBasicRequirements = true;
            if (password.Length < 8)
            {
                result.Errors.Add("Password must be at least 8 characters long.");
                meetsBasicRequirements = false;
            }

            if (!password.Any(char.IsUpper))
            {
                result.Errors.Add("Password must contain at least one uppercase letter.");
                meetsBasicRequirements = false;
            }

            if (!password.Any(char.IsLower))
            {
                result.Errors.Add("Password must contain at least one lowercase letter.");
                meetsBasicRequirements = false;
            }

            if (!password.Any(char.IsDigit))
            {
                result.Errors.Add("Password must contain at least one number.");
                meetsBasicRequirements = false;
            }

            if (!password.Any(c => !char.IsLetterOrDigit(c)))
            {
                result.Errors.Add("Password must contain at least one special character.");
                meetsBasicRequirements = false;
            }

            // Calculate password strength
            result.Strength = CalculatePasswordStrength(password, meetsBasicRequirements);
            result.StrengthMessage = GetStrengthMessage(result.Strength);
            result.IsValid = meetsBasicRequirements;

            return result;
        }

        private PasswordStrength CalculatePasswordStrength(string password, bool meetsBasicRequirements)
        {
            if (!meetsBasicRequirements)
                return PasswordStrength.VeryWeak;

            var score = 0;

            // Length contribution
            if (password.Length >= 12) score += 2;
            else if (password.Length >= 10) score += 1;

            // Character variety contribution
            if (password.Any(char.IsUpper) && password.Any(char.IsLower)) score += 1;
            if (password.Any(char.IsDigit)) score += 1;
            if (password.Any(c => !char.IsLetterOrDigit(c))) score += 1;

            // Complexity contribution
            if (HasNoRepeatingPatterns(password)) score += 1;

            return score switch
            {
                0 => PasswordStrength.VeryWeak,
                1 => PasswordStrength.Weak,
                2 => PasswordStrength.Fair,
                3 => PasswordStrength.Good,
                4 => PasswordStrength.Strong,
                _ => PasswordStrength.VeryStrong
            };
        }

        private static bool HasNoRepeatingPatterns(string password)
        {
            // Check for repeating characters
            for (int i = 0; i < password.Length - 2; i++)
            {
                if (password[i] == password[i + 1] && password[i] == password[i + 2])
                    return false;
            }

            // Check for common patterns
            var commonPatterns = new[]
            {
                "123", "abc", "qwerty", "password", "admin"
            };

            return !commonPatterns.Any(pattern => 
                password.ToLower().Contains(pattern));
        }

        private static string GetStrengthMessage(PasswordStrength strength) => strength switch
        {
            PasswordStrength.VeryWeak => "Password is very weak and does not meet basic requirements.",
            PasswordStrength.Weak => "Password is weak and should be strengthened.",
            PasswordStrength.Fair => "Password meets basic requirements but could be stronger.",
            PasswordStrength.Good => "Password is good and meets most security requirements.",
            PasswordStrength.Strong => "Password is strong and meets all security requirements.",
            PasswordStrength.VeryStrong => "Password is very strong and exceeds security requirements.",
            _ => "Unknown password strength."
        };

        /// <summary>
        /// Validates a password against security requirements and throws a ValidationException if validation fails.
        /// </summary>
        /// <param name="password">The password to validate.</param>
        /// <exception cref="TDFShared.Exceptions.ValidationException">Thrown when validation fails.</exception>
        public void ValidatePasswordAndThrow(string? password)
        {
            var result = ValidatePassword(password);
            if (!result.IsValid)
            {
                throw new TDFShared.Exceptions.ValidationException(string.Join("; ", result.Errors));
            }
        }

        /// <summary>
        /// Validates a password against security requirements asynchronously.
        /// </summary>
        /// <param name="password">The password to validate.</param>
        /// <returns>A task that represents the asynchronous validation operation.</returns>
        public async Task<PasswordValidationResult> ValidatePasswordAsync(string? password)
        {
            // For now, we'll just call the synchronous method
            // In the future, this could be made truly asynchronous if needed
            return await Task.FromResult(ValidatePassword(password));
        }

        /// <summary>
        /// Validates a password against security requirements asynchronously and throws a ValidationException if validation fails.
        /// </summary>
        /// <param name="password">The password to validate.</param>
        /// <exception cref="TDFShared.Exceptions.ValidationException">Thrown when validation fails.</exception>
        public async Task ValidatePasswordAndThrowAsync(string? password)
        {
            var result = await ValidatePasswordAsync(password);
            if (!result.IsValid)
            {
                throw new TDFShared.Exceptions.ValidationException(string.Join("; ", result.Errors));
            }
        }

        /// <summary>
        /// Sanitizes input to prevent common injection attacks
        /// </summary>
        /// <param name="input">Input string to sanitize</param>
        /// <param name="allowHtml">Whether to allow HTML tags</param>
        /// <returns>Sanitized string</returns>
        public string SanitizeInput(string? input, bool allowHtml = false)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            // Remove potentially dangerous characters
            var sanitized = input.Replace("<", "&lt;")
                               .Replace(">", "&gt;")
                               .Replace("\"", "&quot;")
                               .Replace("'", "&#x27;")
                               .Replace("&", "&amp;");

            if (allowHtml)
            {
                // Allow specific HTML tags if requested
                sanitized = sanitized.Replace("&lt;b&gt;", "<b>")
                                   .Replace("&lt;/b&gt;", "</b>")
                                   .Replace("&lt;i&gt;", "<i>")
                                   .Replace("&lt;/i&gt;", "</i>")
                                   .Replace("&lt;u&gt;", "<u>")
                                   .Replace("&lt;/u&gt;", "</u>")
                                   .Replace("&lt;p&gt;", "<p>")
                                   .Replace("&lt;/p&gt;", "</p>")
                                   .Replace("&lt;br&gt;", "<br>");
            }

            return sanitized;
        }

        /// <summary>
        /// Checks if a string contains potentially dangerous patterns
        /// </summary>
        /// <param name="input">Input to check</param>
        /// <returns>True if dangerous patterns are detected</returns>
        public bool ContainsDangerousPatterns(string? input)
        {
            if (string.IsNullOrEmpty(input))
                return false;

            // Check for SQL injection patterns
            var sqlPatterns = new[]
            {
                "SELECT", "INSERT", "UPDATE", "DELETE", "DROP", "UNION",
                "--", "/*", "*/", "EXEC", "EXECUTE", "DECLARE"
            };

            var upperInput = input.ToUpper();
            return sqlPatterns.Any(pattern => upperInput.Contains(pattern)) ||
                   input.Contains("'") ||
                   input.Contains("\"") ||
                   input.Contains(";") ||
                   input.Contains("--");
        }
    }
}
