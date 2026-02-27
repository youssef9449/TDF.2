using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace TDFMAUI.Converters
{
    /// <summary>
    /// Converts a notification type to an appropriate color
    /// </summary>
    public class NotificationTypeToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string type)
            {
                switch (type.ToLowerInvariant())
                {
                    case "info":
                    case "information":
                        return Colors.Blue;
                    
                    case "success":
                        return Colors.Green;
                    
                    case "warning":
                        return Color.FromArgb("#F57C00"); // Orange
                    
                    case "error":
                    case "danger":
                        return Color.FromArgb("#D32F2F"); // Red
                        
                    case "request":
                    case "approval":
                        return Color.FromArgb("#7B1FA2"); // Purple
                        
                    case "announcement":
                        return Color.FromArgb("#0288D1"); // Light Blue
                        
                    default:
                        return Application.Current.Resources["TextSecondaryColor"] as Color ?? Colors.Gray;
                }
            }
            
            return Application.Current.Resources["TextSecondaryColor"] as Color ?? Colors.Gray; // Default color
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Not intended to be used for two-way binding
            throw new NotSupportedException();
        }
    }
}