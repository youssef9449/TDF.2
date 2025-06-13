using System;
using System.Collections.Generic;

namespace TDFShared.Validation.Results
{
    /// <summary>
    /// Base class for all validation results
    /// </summary>
    public abstract class BaseValidationResult
    {
        /// <summary>
        /// Gets or sets whether the validation was successful
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Gets or sets the list of validation errors
        /// </summary>
        public List<string> Errors { get; set; } = new();

        /// <summary>
        /// Gets or sets the list of validation warnings
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// Gets or sets additional metadata about the validation
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();

        /// <summary>
        /// Creates a successful validation result
        /// </summary>
        public static T Success<T>() where T : BaseValidationResult, new()
        {
            return new T { IsValid = true };
        }

        /// <summary>
        /// Creates a failed validation result with a single error
        /// </summary>
        public static T Failure<T>(string error) where T : BaseValidationResult, new()
        {
            return new T
            {
                IsValid = false,
                Errors = new List<string> { error }
            };
        }

        /// <summary>
        /// Creates a failed validation result with multiple errors
        /// </summary>
        public static T Failure<T>(List<string> errors) where T : BaseValidationResult, new()
        {
            return new T
            {
                IsValid = false,
                Errors = errors
            };
        }

        /// <summary>
        /// Adds a warning to the validation result
        /// </summary>
        public T AddWarning<T>(string warning) where T : BaseValidationResult
        {
            Warnings.Add(warning);
            return (T)this;
        }

        /// <summary>
        /// Adds metadata to the validation result
        /// </summary>
        public T AddMetadata<T>(string key, object value) where T : BaseValidationResult
        {
            Metadata[key] = value;
            return (T)this;
        }
    }

    /// <summary>
    /// Generic validation result for object validation
    /// </summary>
    /// <typeparam name="T">Type of the validated object</typeparam>
    public class ObjectValidationResult<T> : BaseValidationResult where T : class
    {
        /// <summary>
        /// Gets or sets the validated object
        /// </summary>
        public T? ValidatedObject { get; set; }

        /// <summary>
        /// Creates a successful validation result with the validated object
        /// </summary>
        public static ObjectValidationResult<T> Success(T obj) => new()
        {
            IsValid = true,
            ValidatedObject = obj
        };
    }

    /// <summary>
    /// Validation result for business rule validation
    /// </summary>
    public class BusinessRuleValidationResult : BaseValidationResult
    {
        /// <summary>
        /// Gets or sets the rule name
        /// </summary>
        public string? RuleName { get; set; }

        /// <summary>
        /// Gets or sets the rule description
        /// </summary>
        public string? RuleDescription { get; set; }
    }

    /// <summary>
    /// Validation result for password validation
    /// </summary>
    public class PasswordValidationResult : BaseValidationResult
    {
        /// <summary>
        /// Gets or sets the password strength
        /// </summary>
        public PasswordStrength Strength { get; set; }

        /// <summary>
        /// Gets or sets the password strength message
        /// </summary>
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

    /// <summary>
    /// Validation result for service-level validation
    /// </summary>
    public class ServiceValidationResult : BaseValidationResult
    {
        /// <summary>
        /// Gets or sets the error message (for backward compatibility)
        /// </summary>
        public string? ErrorMessage
        {
            get => Errors.Count > 0 ? string.Join("; ", Errors) : null;
            set
            {
                Errors.Clear();
                if (!string.IsNullOrEmpty(value))
                {
                    Errors.Add(value);
                }
            }
        }

        /// <summary>
        /// Creates a successful validation result
        /// </summary>
        public static ServiceValidationResult Success() => new()
        {
            IsValid = true
        };

        /// <summary>
        /// Creates a failed validation result with a single error
        /// </summary>
        public static ServiceValidationResult Failure(string error) => new()
        {
            IsValid = false,
            Errors = new List<string> { error }
        };

        /// <summary>
        /// Creates a failed validation result with multiple errors
        /// </summary>
        public static ServiceValidationResult Failure(List<string> errors) => new()
        {
            IsValid = false,
            Errors = errors
        };

        /// <summary>
        /// Adds a warning to the validation result
        /// </summary>
        public ServiceValidationResult AddWarning(string warning)
        {
            Warnings.Add(warning);
            return this;
        }

        /// <summary>
        /// Adds metadata to the validation result
        /// </summary>
        public ServiceValidationResult AddMetadata(string key, object value)
        {
            Metadata[key] = value;
            return this;
        }
    }
} 