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

        public FutureDateAttribute(int minDaysFromNow = 0)
        {
            _minDaysFromNow = minDaysFromNow;
            ErrorMessage = minDaysFromNow > 0 
                ? $"Date must be at least {minDaysFromNow} day(s) from today."
                : "Date must be in the future.";
        }

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

        public DateRangeAttribute(string startDateProperty, int maxDurationDays = 365)
        {
            _startDateProperty = startDateProperty;
            _maxDurationDays = maxDurationDays;
            ErrorMessage = $"End date must be after start date and within {maxDurationDays} days.";
        }

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

        public RequiredForLeaveTypeAttribute(string leaveTypeProperty, params LeaveType[] requiredForTypes)
        {
            _leaveTypeProperty = leaveTypeProperty;
            _requiredForTypes = requiredForTypes;
            ErrorMessage = $"This field is required for {string.Join(", ", requiredForTypes)} leave types.";
        }

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

        public TimeRangeAttribute(string startTimeProperty)
        {
            _startTimeProperty = startTimeProperty;
            ErrorMessage = "End time must be after start time.";
        }

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

        public UsernameAttribute()
        {
            ErrorMessage = "Username must be 3-20 characters and contain only letters, numbers, dots, underscores, or hyphens.";
        }

        public override bool IsValid(object? value)
        {
            if (value is not string username)
                return true; // Let Required attribute handle null values

            return UsernameRegex.IsMatch(username);
        }
    }

    /// <summary>
    /// Custom validation attribute for department codes
    /// </summary>
    public class DepartmentCodeAttribute : ValidationAttribute
    {
        private static readonly Regex DepartmentCodeRegex = new(
            @"^[A-Z]{2,5}$", 
            RegexOptions.Compiled);

        public DepartmentCodeAttribute()
        {
            ErrorMessage = "Department code must be 2-5 uppercase letters.";
        }

        public override bool IsValid(object? value)
        {
            if (value is not string code)
                return true; // Let Required attribute handle null values

            return DepartmentCodeRegex.IsMatch(code);
        }
    }

    /// <summary>
    /// Custom validation attribute for business days only
    /// </summary>
    public class BusinessDayAttribute : ValidationAttribute
    {
        public BusinessDayAttribute()
        {
            ErrorMessage = "Date must be a business day (Monday-Friday).";
        }

        public override bool IsValid(object? value)
        {
            if (value is not DateTime date)
                return true; // Let Required attribute handle null values

            return date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday;
        }
    }
}
