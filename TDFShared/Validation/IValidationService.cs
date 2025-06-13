using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using TDFShared.DTOs.Common;

namespace TDFShared.Validation
{
    /// <summary>
    /// Interface for comprehensive validation services
    /// Provides unified validation patterns for client and server
    /// </summary>
    public interface IValidationService
    {
        /// <summary>
        /// Validates an object using data annotations and custom rules
        /// </summary>
        /// <typeparam name="T">Type of object to validate</typeparam>
        /// <param name="obj">Object to validate</param>
        /// <returns>Validation result with errors if any</returns>
        ValidationResult<T> ValidateObject<T>(T obj) where T : class;

        /// <summary>
        /// Validates an object and throws ValidationException if invalid
        /// </summary>
        /// <typeparam name="T">Type of object to validate</typeparam>
        /// <param name="obj">Object to validate</param>
        /// <exception cref="ValidationException">Thrown when validation fails</exception>
        void ValidateAndThrow<T>(T obj) where T : class;

        /// <summary>
        /// Validates a single property value
        /// </summary>
        /// <param name="value">Value to validate</param>
        /// <param name="propertyName">Name of the property</param>
        /// <param name="objectType">Type of the containing object</param>
        /// <returns>List of validation errors</returns>
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
        /// Validates password strength using security service
        /// </summary>
        /// <param name="password">Password to validate</param>
        /// <returns>Validation result with strength details</returns>
        PasswordValidationResult ValidatePassword(string? password);

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

    /// <summary>
    /// Generic validation result
    /// </summary>
    /// <typeparam name="T">Type of validated object</typeparam>
    public class ValidationResult<T> where T : class
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public T? ValidatedObject { get; set; }

        public static ValidationResult<T> Success(T obj) => new()
        {
            IsValid = true,
            ValidatedObject = obj
        };

        public static ValidationResult<T> Failure(List<string> errors) => new()
        {
            IsValid = false,
            Errors = errors
        };

        public static ValidationResult<T> Failure(string error) => new()
        {
            IsValid = false,
            Errors = new List<string> { error }
        };
    }

    /// <summary>
    /// Password validation result with detailed feedback
    /// </summary>
    public class PasswordValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public PasswordStrength Strength { get; set; }
        public string? StrengthMessage { get; set; }
    }

    /// <summary>
    /// Password strength levels
    /// </summary>
    public enum PasswordStrength
    {
        /// <summary>
        /// Password meets no or very few security requirements
        /// </summary>
        VeryWeak = 0,

        /// <summary>
        /// Password meets minimal security requirements
        /// </summary>
        Weak = 1,

        /// <summary>
        /// Password meets basic security requirements
        /// </summary>
        Fair = 2,

        /// <summary>
        /// Password meets most security requirements
        /// </summary>
        Good = 3,

        /// <summary>
        /// Password meets all security requirements
        /// </summary>
        Strong = 4,

        /// <summary>
        /// Password exceeds all security requirements
        /// </summary>
        VeryStrong = 5
    }
}
