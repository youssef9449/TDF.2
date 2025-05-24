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

        public ValidationService(ISecurityService securityService)
        {
            _securityService = securityService ?? throw new ArgumentNullException(nameof(securityService));
        }

        public ValidationResult<T> ValidateObject<T>(T obj) where T : class
        {
            if (obj == null)
                return ValidationResult<T>.Failure("Object cannot be null");

            var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
            var validationContext = new System.ComponentModel.DataAnnotations.ValidationContext(obj);

            bool isValid = Validator.TryValidateObject(obj, validationContext, validationResults, true);

            if (isValid)
                return ValidationResult<T>.Success(obj);

            var errors = validationResults
                .Where(vr => !string.IsNullOrEmpty(vr.ErrorMessage))
                .Select(vr => vr.ErrorMessage!)
                .ToList();

            return ValidationResult<T>.Failure(errors);
        }

        public void ValidateAndThrow<T>(T obj) where T : class
        {
            var result = ValidateObject(obj);
            if (!result.IsValid)
            {
                throw new System.ComponentModel.DataAnnotations.ValidationException(string.Join("; ", result.Errors));
            }
        }

        public List<string> ValidateProperty(object? value, string propertyName, Type objectType)
        {
            var errors = new List<string>();
            var validationContext = new System.ComponentModel.DataAnnotations.ValidationContext(new object())
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

        public string? ValidateRequired(string? value, string fieldName, string? customMessage = null)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return customMessage ?? $"{fieldName} is required and cannot be empty.";
            }
            return null;
        }

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
