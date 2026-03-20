using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace TDFMAUI.Converters
{
    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return Application.Current.Resources["TextSecondaryColor"] as Color ?? Color.FromArgb("#90A4AE");

            string status = value.ToString().ToLowerInvariant();

            switch (status)
            {
                case "pending":
                    return Color.FromArgb("#FFB300"); // Amber
                case "approved":
                case "managerapproved":
                case "hrapproved":
                    return Color.FromArgb("#43A047"); // Green
                case "rejected":
                case "managerrejected":
                    return Color.FromArgb("#E53935"); // Red
                default:
                    return Application.Current.Resources["TextSecondaryColor"] as Color ?? Color.FromArgb("#90A4AE");
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // One-way binding only; no conversion back from color to status.
            return Binding.DoNothing;
        }
    }
}
