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
        /// Not implemented
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}