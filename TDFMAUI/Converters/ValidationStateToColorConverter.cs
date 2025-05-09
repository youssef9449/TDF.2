using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace TDFMAUI.Converters
{
    public class ValidationStateToColorConverter : IValueConverter
    {
        public Color ErrorColor { get; set; } = Colors.OrangeRed;
        public Color ValidColor { get; set; } = Colors.Transparent; // Or Colors.Grey, etc.

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not IEnumerable<string> errors || parameter is not string fieldName)
            {
                return ValidColor;
            }

            bool hasError = false;
            string lowerFieldName = fieldName.ToLowerInvariant();

            // Map field names to expected error message fragments
            hasError = errors.Any(e =>
            {
                string lowerError = e.ToLowerInvariant();
                switch (lowerFieldName)
                {
                    case "leavetype":
                        return lowerError.Contains("leave type");
                    case "startdate":
                        return lowerError.Contains("start date"); // Catches "past" and "same calendar day"
                    case "enddate":
                        return lowerError.Contains("end date") || lowerError.Contains("same calendar day"); // Catches "before start" and "same calendar day"
                    case "starttime":
                    case "endtime":
                        return lowerError.Contains("time"); // Catches "must be provided" and "end time must be after"
                    // Add other fields like "Reason" if validation rules are added
                    default:
                        return false;
                }
            });


            return hasError ? ErrorColor : ValidColor;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException(); // Not needed for one-way binding
        }
    }
} 