using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.RegularExpressions;
using TDFShared.Enums;

namespace TDFShared.Validation
{
    /// <summary>
    /// Custom validation attribute for future dates
    /// </summary>
    public class FutureDateAttribute : ValidationAttribute
    {
        private readonly int _minDaysFromNow;

        /// <summary>
        /// Initializes a new instance of the FutureDateAttribute class.
        /// </summary>
        /// <param name="minDaysFromNow">Minimum number of days from today required (0 = today allowed, 1 = tomorrow minimum, etc.)</param>
        public FutureDateAttribute(int minDaysFromNow = 0)
        {
            _minDaysFromNow = minDaysFromNow;
            ErrorMessage = minDaysFromNow > 0 
                ? $"Date must be at least {minDaysFromNow} day(s) from today."
                : "Date must be in the future.";
        }

        /// <summary>
        /// Determines whether the value of the date is valid.
        /// </summary>
        /// <param name="value">The value to validate.</param>
        /// <returns>True if the value is null or a valid future date; otherwise, false.</returns>
        public override bool IsValid(object? value)
        {
            if (value is not DateTime date)
                return true; // Let Required attribute handle null values

            return date.Date >= DateTime.Today.AddDays(_minDaysFromNow);
        }
    }

    /// <summary>
    /// Custom validation attribute for date ranges
    /// </summary>
    public class DateRangeAttribute : ValidationAttribute
    {
        private readonly string _startDateProperty;
        private readonly int _maxDurationDays;

        /// <summary>
        /// Initializes a new instance of the DateRangeAttribute class.
        /// </summary>
        /// <param name="startDateProperty">The name of the property containing the start date.</param>
        /// <param name="maxDurationDays">Maximum allowed duration in days (default: 365).</param>
        public DateRangeAttribute(string startDateProperty, int maxDurationDays = 365)
        {
            _startDateProperty = startDateProperty;
            _maxDurationDays = maxDurationDays;
            ErrorMessage = $"End date must be after start date and within {maxDurationDays} days.";
        }

        /// <summary>
        /// Validates the end date against the start date and maximum duration.
        /// </summary>
        /// <param name="value">The end date to validate.</param>
        /// <param name="validationContext">The validation context.</param>
        /// <returns>ValidationResult.Success if valid; otherwise, a ValidationResult with an error message.</returns>
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is not DateTime endDate)
                return ValidationResult.Success; // Let Required attribute handle null values

            var startDateProperty = validationContext.ObjectType.GetProperty(_startDateProperty);
            if (startDateProperty == null)
                return new ValidationResult($"Property {_startDateProperty} not found.");

            var startDateValue = startDateProperty.GetValue(validationContext.ObjectInstance);
            if (startDateValue is not DateTime startDate)
                return ValidationResult.Success;

            if (endDate.Date < startDate.Date)
                return new ValidationResult("End date must be on or after start date.");

            var duration = (endDate.Date - startDate.Date).Days;
            if (duration > _maxDurationDays)
                return new ValidationResult($"Date range cannot exceed {_maxDurationDays} days.");

            return ValidationResult.Success;
        }
    }

    /// <summary>
    /// Custom validation attribute for leave types that require time
    /// </summary>
    public class RequiredForLeaveTypeAttribute : ValidationAttribute
    {
        private readonly string _leaveTypeProperty;
        private readonly LeaveType[] _requiredForTypes;

        /// <summary>
        /// Initializes a new instance of the RequiredForLeaveTypeAttribute class.
        /// </summary>
        /// <param name="leaveTypeProperty">The name of the property containing the leave type.</param>
        /// <param name="requiredForTypes">The leave types for which this field is required.</param>
        public RequiredForLeaveTypeAttribute(string leaveTypeProperty, params LeaveType[] requiredForTypes)
        {
            _leaveTypeProperty = leaveTypeProperty;
            _requiredForTypes = requiredForTypes;
            ErrorMessage = $"This field is required for {string.Join(", ", requiredForTypes)} leave types.";
        }

        /// <summary>
        /// Validates that the field has a value when the leave type requires it.
        /// </summary>
        /// <param name="value">The value to validate.</param>
        /// <param name="validationContext">The validation context.</param>
        /// <returns>ValidationResult.Success if valid; otherwise, a ValidationResult with an error message.</returns>
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            var leaveTypeProperty = validationContext.ObjectType.GetProperty(_leaveTypeProperty);
            if (leaveTypeProperty == null)
                return new ValidationResult($"Property {_leaveTypeProperty} not found.");

            var leaveTypeValue = leaveTypeProperty.GetValue(validationContext.ObjectInstance);
            if (leaveTypeValue is not LeaveType leaveType)
                return ValidationResult.Success;

            bool isRequired = _requiredForTypes.Contains(leaveType);
            bool hasValue = value != null && !string.IsNullOrWhiteSpace(value.ToString());

            if (isRequired && !hasValue)
                return new ValidationResult(ErrorMessage);

            return ValidationResult.Success;
        }
    }

    /// <summary>
    /// Custom validation attribute for time ranges
    /// </summary>
    public class TimeRangeAttribute : ValidationAttribute
    {
        private readonly string _startTimeProperty;

        /// <summary>
        /// Initializes a new instance of the TimeRangeAttribute class.
        /// </summary>
        /// <param name="startTimeProperty">The name of the property containing the start time.</param>
        public TimeRangeAttribute(string startTimeProperty)
        {
            _startTimeProperty = startTimeProperty;
            ErrorMessage = "End time must be after start time.";
        }

        /// <summary>
        /// Validates that the end time is after the start time.
        /// </summary>
        /// <param name="value">The end time to validate.</param>
        /// <param name="validationContext">The validation context.</param>
        /// <returns>ValidationResult.Success if valid; otherwise, a ValidationResult with an error message.</returns>
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is not TimeSpan endTime)
                return ValidationResult.Success;

            var startTimeProperty = validationContext.ObjectType.GetProperty(_startTimeProperty);
            if (startTimeProperty == null)
                return new ValidationResult($"Property {_startTimeProperty} not found.");

            var startTimeValue = startTimeProperty.GetValue(validationContext.ObjectInstance);
            if (startTimeValue is not TimeSpan startTime)
                return ValidationResult.Success;

            if (endTime <= startTime)
                return new ValidationResult(ErrorMessage);

            return ValidationResult.Success;
        }
    }

    /// <summary>
    /// Custom validation attribute for strong passwords
    /// </summary>
    public class StrongPasswordAttribute : ValidationAttribute
    {
        private readonly int _minLength;
        private readonly bool _requireUppercase;
        private readonly bool _requireLowercase;
        private readonly bool _requireDigit;
        private readonly bool _requireSpecialChar;

        /// <summary>
        /// Initializes a new instance of the StrongPasswordAttribute class.
        /// </summary>
        /// <param name="minLength">Minimum password length (default: 8).</param>
        /// <param name="requireUppercase">Whether to require uppercase letters (default: true).</param>
        /// <param name="requireLowercase">Whether to require lowercase letters (default: true).</param>
        /// <param name="requireDigit">Whether to require digits (default: true).</param>
        /// <param name="requireSpecialChar">Whether to require special characters (default: true).</param>
        public StrongPasswordAttribute(
            int minLength = 8,
            bool requireUppercase = true,
            bool requireLowercase = true,
            bool requireDigit = true,
            bool requireSpecialChar = true)
        {
            _minLength = minLength;
            _requireUppercase = requireUppercase;
            _requireLowercase = requireLowercase;
            _requireDigit = requireDigit;
            _requireSpecialChar = requireSpecialChar;

            var requirements = new List<string>();
            if (minLength > 0) requirements.Add($"at least {minLength} characters");
            if (requireUppercase) requirements.Add("uppercase letter");
            if (requireLowercase) requirements.Add("lowercase letter");
            if (requireDigit) requirements.Add("digit");
            if (requireSpecialChar) requirements.Add("special character");

            ErrorMessage = $"Password must contain {string.Join(", ", requirements)}.";
        }

        /// <summary>
        /// Validates that the password meets the strength requirements.
        /// </summary>
        /// <param name="value">The password to validate.</param>
        /// <returns>True if the password meets all requirements; otherwise, false.</returns>
        public override bool IsValid(object? value)
        {
            if (value is not string password)
                return true; // Let Required attribute handle null values

            if (password.Length < _minLength)
                return false;

            if (_requireUppercase && !password.Any(char.IsUpper))
                return false;

            if (_requireLowercase && !password.Any(char.IsLower))
                return false;

            if (_requireDigit && !password.Any(char.IsDigit))
                return false;

            if (_requireSpecialChar && !password.Any(c => !char.IsLetterOrDigit(c)))
                return false;

            return true;
        }
    }

    /// <summary>
    /// Custom validation attribute for username format
    /// </summary>
    public class UsernameAttribute : ValidationAttribute
    {
        private static readonly Regex UsernameRegex = new(
            @"^[a-zA-Z0-9._-]{3,20}$", 
            RegexOptions.Compiled);

        private static readonly HashSet<string> ReservedUsernames = new(StringComparer.OrdinalIgnoreCase)
        {
            "admin", "administrator", "system", "root", "guest", "anonymous",
            "support", "help", "info", "noreply", "no-reply", "webmaster",
            "mail", "email", "contact", "security", "test", "demo"
        };

        /// <summary>
        /// Initializes a new instance of the UsernameAttribute class.
        /// </summary>
        public UsernameAttribute()
        {
            ErrorMessage = "Username must be 3-20 characters and contain only letters, numbers, dots, underscores, or hyphens.";
        }

        /// <summary>
        /// Validates that the username meets the format requirements and is not reserved.
        /// </summary>
        /// <param name="value">The username to validate.</param>
        /// <returns>True if the username is valid; otherwise, false.</returns>
        public override bool IsValid(object? value)
        {
            if (value is not string username)
                return true; // Let Required attribute handle null values

            // Check basic format
            if (!UsernameRegex.IsMatch(username))
                return false;

            // Check for reserved usernames
            if (ReservedUsernames.Contains(username))
            {
                ErrorMessage = "This username is reserved and cannot be used.";
                return false;
            }

            // Check for consecutive special characters
            if (ContainsConsecutiveSpecialChars(username))
            {
                ErrorMessage = "Username cannot contain consecutive special characters.";
                return false;
            }

            // Check for common patterns that might indicate automated attempts
            if (IsCommonPattern(username))
            {
                ErrorMessage = "This username pattern is not allowed.";
                return false;
            }

            return true;
        }

        private bool ContainsConsecutiveSpecialChars(string username)
        {
            for (int i = 0; i < username.Length - 1; i++)
            {
                if (IsSpecialChar(username[i]) && IsSpecialChar(username[i + 1]))
                    return true;
            }
            return false;
        }

        private bool IsSpecialChar(char c)
        {
            return c == '.' || c == '_' || c == '-';
        }

        private bool IsCommonPattern(string username)
        {
            // Check for sequential numbers
            if (Regex.IsMatch(username, @"\d{3,}"))
            {
                var numbers = username.Where(char.IsDigit).ToArray();
                if (numbers.Length >= 3)
                {
                    for (int i = 0; i < numbers.Length - 2; i++)
                    {
                        if (numbers[i] + 1 == numbers[i + 1] && numbers[i + 1] + 1 == numbers[i + 2])
                            return true;
                    }
                }
            }

            // Check for repeated characters
            if (Regex.IsMatch(username, @"(.)\1{2,}"))
                return true;

            // Check for common prefixes/suffixes
            var commonPatterns = new[] { "test", "demo", "user", "admin", "temp", "tmp" };
            return commonPatterns.Any(pattern => 
                username.StartsWith(pattern, StringComparison.OrdinalIgnoreCase) || 
                username.EndsWith(pattern, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Custom validation attribute for department codes
    /// </summary>
    public class DepartmentCodeAttribute : ValidationAttribute
    {
        private static readonly Regex DepartmentCodeRegex = new(
            @"^[A-Z]{2,4}$",
            RegexOptions.Compiled);

        /// <summary>
        /// Initializes a new instance of the DepartmentCodeAttribute class.
        /// </summary>
        public DepartmentCodeAttribute()
        {
            ErrorMessage = "Department code must be 2-4 uppercase letters.";
        }

        /// <summary>
        /// Validates that the department code meets the format requirements.
        /// </summary>
        /// <param name="value">The department code to validate.</param>
        /// <returns>True if the department code is valid; otherwise, false.</returns>
        public override bool IsValid(object? value)
        {
            if (value is not string code)
                return true; // Let Required attribute handle null values

            return DepartmentCodeRegex.IsMatch(code);
        }
    }

    /// <summary>
    /// Custom validation attribute for business days
    /// </summary>
    public class BusinessDayAttribute : ValidationAttribute
    {
        /// <summary>
        /// Initializes a new instance of the BusinessDayAttribute class.
        /// </summary>
        public BusinessDayAttribute()
        {
            ErrorMessage = "Date must be a business day (Monday through Friday).";
        }

        /// <summary>
        /// Validates that the date is a business day.
        /// </summary>
        /// <param name="value">The date to validate.</param>
        /// <returns>True if the date is a business day; otherwise, false.</returns>
        public override bool IsValid(object? value)
        {
            if (value is not DateTime date)
                return true; // Let Required attribute handle null values

            return date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday;
        }
    }
}
