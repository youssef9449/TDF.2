using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace TDFMAUI.Converters
{
    /// <summary>
    /// Converts a boolean value to a visibility value.
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Converts a boolean value to a visibility value.
        /// True becomes Visible, False becomes Collapsed.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                // For MAUI, we return the boolean directly since Visibility enum isn't used
                // The IsVisible property uses a boolean
                return boolValue;
            }
            
            // Default to visible (true) if conversion fails
            return true;
        }

        /// <summary>
        /// Converts a visibility value back to a boolean value.
        /// Visible becomes True, Collapsed becomes False.
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool visibilityValue)
            {
                // For MAUI, just return the boolean value
                return visibilityValue;
            }
            
            // Default to true if conversion fails
            return true;
        }
    }
}
