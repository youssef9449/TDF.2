using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace TDFMAUI.Converters
{
    public class ThemeToBackgroundColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is AppTheme theme)
            {
                return theme == AppTheme.Dark 
                    ? Application.Current.Resources["DarkBackground"] 
                    : Application.Current.Resources["BackgroundColor"];
            }
            return Application.Current.Resources["BackgroundColor"];
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
