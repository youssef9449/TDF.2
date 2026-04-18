using Microsoft.Maui.Controls;
using System;
using System.Globalization;

namespace TDFMAUI.Converters
{
    public class StringNotEmptyConverter : IValueConverter
    {
        /// <summary>
        /// Converts a string to a boolean indicating whether the string is not null or empty
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string strValue)
            {
                return !string.IsNullOrEmpty(strValue);
            }
            
            return false;
        }

        /// <summary>
        /// One-way converter: the inverse mapping bool -> original string is undefined,
        /// so <see cref="ConvertBack"/> is intentionally unsupported.
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException(
                $"{nameof(StringNotEmptyConverter)} is a one-way converter and does not support {nameof(ConvertBack)}.");
        }
    }
}