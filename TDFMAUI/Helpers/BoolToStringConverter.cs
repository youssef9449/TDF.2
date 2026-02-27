using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace TDFMAUI.Helpers
{
    public class BoolToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && parameter is string param)
            {
                var options = param.Split('|');
                if (options.Length == 2)
                    return b ? options[0] : options[1];
            }
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
