using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using TDFShared.DTOs.Common;
using TDFShared.Validation.Results;
using TDFShared.Exceptions;

namespace TDFShared.Validation
{
    /// <summary>
    /// Interface for validation service providing unified validation patterns
    /// for both client and server-side validation
    /// </summary>
    public interface IValidationService
    {
        /// <summary>
        /// Validates an object using data annotations and returns a validation result.
        /// </summary>
        /// <typeparam name="T">The type of object to validate.</typeparam>
        /// <param name="obj">The object to validate.</param>
        /// <returns>A ValidationResult containing the validation status and any error messages.</returns>
        ObjectValidationResult<T> ValidateObject<T>(T obj) where T : class;

        /// <summary>
        /// Validates an object and throws a ValidationException if validation fails.
        /// </summary>
        /// <typeparam name="T">The type of object to validate.</typeparam>
        /// <param name="obj">The object to validate.</param>
        /// <exception cref="TDFShared.Exceptions.ValidationException">Thrown when validation fails.</exception>
        void ValidateAndThrow<T>(T obj) where T : class;

        /// <summary>
        /// Validates a specific property of an object using its validation attributes.
        /// </summary>
        /// <param name="value">The value to validate.</param>
        /// <param name="propertyName">The name of the property being validated.</param>
        /// <param name="objectType">The type of the object containing the property.</param>
        /// <returns>A list of validation error messages, if any.</returns>
        List<string> ValidateProperty(object? value, string propertyName, Type objectType);

        /// <summary>
        /// Validates required string field
        /// </summary>
        /// <param name="value">Value to validate</param>
        /// <param name="fieldName">Name of the field</param>
        /// <param name="customMessage">Custom error message</param>
        /// <returns>Validation error or null if valid</returns>
        string? ValidateRequired(string? value, string fieldName, string? customMessage = null);

        /// <summary>
        /// Validates string length constraints
        /// </summary>
        /// <param name="value">Value to validate</param>
        /// <param name="fieldName">Name of the field</param>
        /// <param name="minLength">Minimum length (optional)</param>
        /// <param name="maxLength">Maximum length (optional)</param>
        /// <returns>Validation error or null if valid</returns>
        string? ValidateStringLength(string? value, string fieldName, int? minLength = null, int? maxLength = null);

        /// <summary>
        /// Validates numeric range
        /// </summary>
        /// <param name="value">Value to validate</param>
        /// <param name="fieldName">Name of the field</param>
        /// <param name="min">Minimum value (optional)</param>
        /// <param name="max">Maximum value (optional)</param>
        /// <returns>Validation error or null if valid</returns>
        string? ValidateRange(int value, string fieldName, int? min = null, int? max = null);

        /// <summary>
        /// Validates date range with optional minimum days from now requirement
        /// </summary>
        /// <param name="startDate">Start date</param>
        /// <param name="endDate">End date (optional)</param>
        /// <param name="fieldName">Name of the field</param>
        /// <param name="minDaysFromNow">Minimum days from today (0 = today allowed, 1 = tomorrow minimum, etc.)</param>
        /// <returns>List of validation errors</returns>
        List<string> ValidateDateRange(DateTime startDate, DateTime? endDate, string fieldName = "Date", int minDaysFromNow = 0);

        /// <summary>
        /// Validates a password against security requirements.
        /// </summary>
        /// <param name="password">The password to validate.</param>
        /// <returns>A PasswordValidationResult containing validation status, errors, and strength assessment.</returns>
        PasswordValidationResult ValidatePassword(string? password);

        /// <summary>
        /// Validates a password against security requirements and throws a ValidationException if validation fails.
        /// </summary>
        /// <param name="password">The password to validate.</param>
        /// <exception cref="TDFShared.Exceptions.ValidationException">Thrown when validation fails.</exception>
        void ValidatePasswordAndThrow(string? password);

        /// <summary>
        /// Validates a password against security requirements asynchronously.
        /// </summary>
        /// <param name="password">The password to validate.</param>
        /// <returns>A task that represents the asynchronous validation operation.</returns>
        Task<PasswordValidationResult> ValidatePasswordAsync(string? password);

        /// <summary>
        /// Validates a password against security requirements asynchronously and throws a ValidationException if validation fails.
        /// </summary>
        /// <param name="password">The password to validate.</param>
        /// <exception cref="TDFShared.Exceptions.ValidationException">Thrown when validation fails.</exception>
        Task ValidatePasswordAndThrowAsync(string? password);

        /// <summary>
        /// Sanitizes input to prevent common injection attacks
        /// </summary>
        /// <param name="input">Input string to sanitize</param>
        /// <param name="allowHtml">Whether to allow HTML tags</param>
        /// <returns>Sanitized string</returns>
        string SanitizeInput(string? input, bool allowHtml = false);

        /// <summary>
        /// Checks if a string contains potentially dangerous patterns
        /// </summary>
        /// <param name="input">Input to check</param>
        /// <returns>True if dangerous patterns are detected</returns>
        bool ContainsDangerousPatterns(string? input);
    }
}
