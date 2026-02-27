using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace TDFMAUI.Converters
{
    /// <summary>
    /// Converts an integer value to a boolean. Intended for checking if collections have items.
    /// Returns true if the value is greater than zero; otherwise, false.
    /// </summary>
    public class IntToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int intValue)
            {
                return intValue > 0;
            }
            
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
} 