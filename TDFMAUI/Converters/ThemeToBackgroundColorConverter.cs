using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace TDFMAUI.Converters
{
    public class ThemeToBackgroundColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (Application.Current?.Resources is not { } resources)
            {
                return Colors.White;
            }

            if (value is AppTheme theme && theme == AppTheme.Dark)
            {
                if (resources.TryGetValue("BackgroundColorDark", out var darkColor))
                {
                    return darkColor;
                }
            }

            return resources.TryGetValue("BackgroundColor", out var bgColor)
                ? bgColor
                : Colors.White;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
