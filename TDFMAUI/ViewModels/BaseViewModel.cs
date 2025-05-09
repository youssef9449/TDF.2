using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq; // Add for LINQ methods
using System.Text.RegularExpressions; // Add for Regex
using CommunityToolkit.Mvvm.ComponentModel;

namespace TDFMAUI.ViewModels
{
    public abstract partial class BaseViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private bool _isBusy;

        // Password strength validation helper (mirrors backend logic)
        protected bool IsPasswordStrong(string password, out string validationMessage)
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
    }
} 