using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace TDFMAUI.Converters
{
    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string status)
            {
                switch (status.ToLowerInvariant())
                {
                    case "pending":
                        return Color.FromArgb("#FFB300"); // Amber
                    case "approved":
                        return Color.FromArgb("#43A047"); // Green
                    case "rejected":
                        return Color.FromArgb("#E53935"); // Red
                    default:
                        return Color.FromArgb("#90A4AE"); // Grey
                }
            }
            return Color.FromArgb("#90A4AE"); // Default Grey
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // One-way binding only; no conversion back from color to status.
            return Binding.DoNothing;
        }
    }
}
