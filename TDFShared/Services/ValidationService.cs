using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.RegularExpressions;
using TDFShared.Exceptions;

namespace TDFShared.Services
{
    /// <summary>
    /// Enhanced validation service providing common validation utilities and patterns
    /// </summary>
    public static class ValidationService
    {
        /// <summary>
        /// Validates an object using data annotations and returns validation results
        /// </summary>
        /// <param name="obj">Object to validate</param>
        /// <returns>List of validation results</returns>
        public static List<ValidationResult> ValidateObject(object obj)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));

            var validationResults = new List<ValidationResult>();
            var validationContext = new ValidationContext(obj);
            
            Validator.TryValidateObject(obj, validationContext, validationResults, true);
            
            return validationResults;
        }

        /// <summary>
        /// Validates an object and throws ValidationException if invalid
        /// </summary>
        /// <param name="obj">Object to validate</param>
        /// <param name="objectName">Name of the object for error messages</param>
        /// <exception cref="ValidationException">Thrown when validation fails</exception>
        public static void ValidateObjectAndThrow(object obj, string objectName = "Object")
        {
            var validationResults = ValidateObject(obj);
            
            if (validationResults.Any())
            {
                var errorMessages = validationResults.Select(vr => vr.ErrorMessage).Where(em => !string.IsNullOrEmpty(em));
                var combinedMessage = $"{objectName} validation failed: {string.Join("; ", errorMessages)}";
                throw new ValidationException(combinedMessage);
            }
        }

        /// <summary>
        /// Validates that a string is not null, empty, or whitespace
        /// </summary>
        /// <param name="value">Value to validate</param>
        /// <param name="fieldName">Name of the field for error messages</param>
        /// <param name="customMessage">Custom error message (optional)</param>
        /// <exception cref="ValidationException">Thrown when validation fails</exception>
        public static void ValidateRequired(string? value, string fieldName, string? customMessage = null)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                var message = customMessage ?? $"{fieldName} is required and cannot be empty.";
                throw new ValidationException(message);
            }
        }

        /// <summary>
        /// Validates that a value is not null
        /// </summary>
        /// <param name="value">Value to validate</param>
        /// <param name="fieldName">Name of the field for error messages</param>
        /// <param name="customMessage">Custom error message (optional)</param>
        /// <exception cref="ValidationException">Thrown when validation fails</exception>
        public static void ValidateNotNull(object? value, string fieldName, string? customMessage = null)
        {
            if (value == null)
            {
                var message = customMessage ?? $"{fieldName} cannot be null.";
                throw new ValidationException(message);
            }
        }

        /// <summary>
        /// Validates string length constraints
        /// </summary>
        /// <param name="value">Value to validate</param>
        /// <param name="fieldName">Name of the field for error messages</param>
        /// <param name="minLength">Minimum length (optional)</param>
        /// <param name="maxLength">Maximum length (optional)</param>
        /// <exception cref="ValidationException">Thrown when validation fails</exception>
        public static void ValidateStringLength(string? value, string fieldName, int? minLength = null, int? maxLength = null)
        {
            if (value == null) return; // Use ValidateRequired for null checks

            if (minLength.HasValue && value.Length < minLength.Value)
            {
                throw new ValidationException($"{fieldName} must be at least {minLength.Value} characters long.");
            }

            if (maxLength.HasValue && value.Length > maxLength.Value)
            {
                throw new ValidationException($"{fieldName} cannot exceed {maxLength.Value} characters.");
            }
        }

        /// <summary>
        /// Validates numeric range constraints
        /// </summary>
        /// <param name="value">Value to validate</param>
        /// <param name="fieldName">Name of the field for error messages</param>
        /// <param name="minValue">Minimum value (optional)</param>
        /// <param name="maxValue">Maximum value (optional)</param>
        /// <exception cref="ValidationException">Thrown when validation fails</exception>
        public static void ValidateRange(int value, string fieldName, int? minValue = null, int? maxValue = null)
        {
            if (minValue.HasValue && value < minValue.Value)
            {
                throw new ValidationException($"{fieldName} must be at least {minValue.Value}.");
            }

            if (maxValue.HasValue && value > maxValue.Value)
            {
                throw new ValidationException($"{fieldName} cannot exceed {maxValue.Value}.");
            }
        }

        /// <summary>
        /// Validates decimal range constraints
        /// </summary>
        /// <param name="value">Value to validate</param>
        /// <param name="fieldName">Name of the field for error messages</param>
        /// <param name="minValue">Minimum value (optional)</param>
        /// <param name="maxValue">Maximum value (optional)</param>
        /// <exception cref="ValidationException">Thrown when validation fails</exception>
        public static void ValidateRange(decimal value, string fieldName, decimal? minValue = null, decimal? maxValue = null)
        {
            if (minValue.HasValue && value < minValue.Value)
            {
                throw new ValidationException($"{fieldName} must be at least {minValue.Value}.");
            }

            if (maxValue.HasValue && value > maxValue.Value)
            {
                throw new ValidationException($"{fieldName} cannot exceed {maxValue.Value}.");
            }
        }

        /// <summary>
        /// Validates date range constraints
        /// </summary>
        /// <param name="value">Value to validate</param>
        /// <param name="fieldName">Name of the field for error messages</param>
        /// <param name="minDate">Minimum date (optional)</param>
        /// <param name="maxDate">Maximum date (optional)</param>
        /// <exception cref="ValidationException">Thrown when validation fails</exception>
        public static void ValidateDateRange(DateTime value, string fieldName, DateTime? minDate = null, DateTime? maxDate = null)
        {
            if (minDate.HasValue && value < minDate.Value)
            {
                throw new ValidationException($"{fieldName} cannot be earlier than {minDate.Value:yyyy-MM-dd}.");
            }

            if (maxDate.HasValue && value > maxDate.Value)
            {
                throw new ValidationException($"{fieldName} cannot be later than {maxDate.Value:yyyy-MM-dd}.");
            }
        }

        /// <summary>
        /// Validates email format
        /// </summary>
        /// <param name="email">Email to validate</param>
        /// <param name="fieldName">Name of the field for error messages</param>
        /// <exception cref="ValidationException">Thrown when validation fails</exception>
        public static void ValidateEmail(string? email, string fieldName = "Email")
        {
            if (string.IsNullOrWhiteSpace(email))
                return; // Use ValidateRequired for null/empty checks

            var emailRegex = new Regex(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", RegexOptions.Compiled);
            
            if (!emailRegex.IsMatch(email))
            {
                throw new ValidationException($"{fieldName} must be a valid email address.");
            }
        }

        /// <summary>
        /// Validates that a date is not in the past
        /// </summary>
        /// <param name="date">Date to validate</param>
        /// <param name="fieldName">Name of the field for error messages</param>
        /// <param name="allowToday">Whether today's date is allowed</param>
        /// <exception cref="ValidationException">Thrown when validation fails</exception>
        public static void ValidateNotInPast(DateTime date, string fieldName, bool allowToday = true)
        {
            var today = DateTime.Today;
            var compareDate = allowToday ? today : today.AddDays(1);

            if (date.Date < compareDate)
            {
                var message = allowToday 
                    ? $"{fieldName} cannot be in the past."
                    : $"{fieldName} must be in the future.";
                throw new ValidationException(message);
            }
        }

        /// <summary>
        /// Validates that end date is not before start date
        /// </summary>
        /// <param name="startDate">Start date</param>
        /// <param name="endDate">End date</param>
        /// <param name="startFieldName">Name of start date field</param>
        /// <param name="endFieldName">Name of end date field</param>
        /// <exception cref="ValidationException">Thrown when validation fails</exception>
        public static void ValidateDateOrder(DateTime startDate, DateTime? endDate, string startFieldName = "Start date", string endFieldName = "End date")
        {
            if (endDate.HasValue && endDate.Value.Date < startDate.Date)
            {
                throw new ValidationException($"{endFieldName} cannot be before {startFieldName}.");
            }
        }

        /// <summary>
        /// Validates that a collection is not null or empty
        /// </summary>
        /// <param name="collection">Collection to validate</param>
        /// <param name="fieldName">Name of the field for error messages</param>
        /// <exception cref="ValidationException">Thrown when validation fails</exception>
        public static void ValidateCollectionNotEmpty<T>(IEnumerable<T>? collection, string fieldName)
        {
            if (collection == null || !collection.Any())
            {
                throw new ValidationException($"{fieldName} cannot be null or empty.");
            }
        }

        /// <summary>
        /// Validates that a value is within a predefined set of allowed values
        /// </summary>
        /// <param name="value">Value to validate</param>
        /// <param name="allowedValues">Set of allowed values</param>
        /// <param name="fieldName">Name of the field for error messages</param>
        /// <exception cref="ValidationException">Thrown when validation fails</exception>
        public static void ValidateAllowedValues<T>(T value, IEnumerable<T> allowedValues, string fieldName)
        {
            if (value == null || !allowedValues.Contains(value))
            {
                var allowedValuesString = string.Join(", ", allowedValues);
                throw new ValidationException($"{fieldName} must be one of the following values: {allowedValuesString}.");
            }
        }

        /// <summary>
        /// Validates multiple conditions and collects all error messages
        /// </summary>
        /// <param name="validations">Dictionary of validation conditions and their error messages</param>
        /// <exception cref="ValidationException">Thrown when any validation fails</exception>
        public static void ValidateMultiple(Dictionary<bool, string> validations)
        {
            var errors = validations.Where(kvp => !kvp.Key).Select(kvp => kvp.Value).ToList();
            
            if (errors.Any())
            {
                throw new ValidationException(string.Join("; ", errors));
            }
        }

        /// <summary>
        /// Validates multiple conditions and collects all error messages (async version)
        /// </summary>
        /// <param name="validations">List of validation functions that return error messages (null/empty if valid)</param>
        /// <exception cref="ValidationException">Thrown when any validation fails</exception>
        public static void ValidateMultiple(params Func<string?>[] validations)
        {
            var errors = validations
                .Select(validation => validation())
                .Where(error => !string.IsNullOrEmpty(error))
                .ToList();
            
            if (errors.Any())
            {
                throw new ValidationException(string.Join("; ", errors));
            }
        }
    }
}
