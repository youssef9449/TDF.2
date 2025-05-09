using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace TDFMAUI.Converters
{
    public class DateTimeToStringConverter : IValueConverter
    {
        public string Format { get; set; } = "g";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime dt)
            {
                return dt.ToString(Format, culture);
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s && DateTime.TryParse(s, culture, DateTimeStyles.None, out var dt))
                return dt;
            return DateTime.MinValue;
        }
    }
}
