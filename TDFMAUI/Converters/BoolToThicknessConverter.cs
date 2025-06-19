using System;
using System.Globalization;
using Microsoft.Maui;
using Microsoft.Maui.Controls;

namespace TDFMAUI.Converters
{
    public class BoolToThicknessConverter : IValueConverter
    {
    /// Converts a boolean to a Thickness.
    /// Parameter format: "trueThickness|falseThickness" where each thickness is in the format "left,top,right,bottom"
    /// </summary>
    
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool boolValue = value is bool b && b;

            if (parameter is string paramStr)
            {
                var parts = paramStr.Split('|');
                if (parts.Length == 2)
                {
                    string thicknessStr = boolValue ? parts[0] : parts[1];
                    return ParseThickness(thicknessStr);
                }
            }

            return new Thickness(0);
        }

        /// <summary>
        /// Not implemented
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }

        /// <summary>
        /// Parse a thickness string in the format "left,top,right,bottom" or "uniform"
        /// </summary>
        private Thickness ParseThickness(string thicknessStr)
        {


            if (string.IsNullOrEmpty(thicknessStr))
                return new Thickness(0);

            var parts = thicknessStr.Split(',');

            if (parts.Length == 1 && double.TryParse(parts[0], out double uniform))
            {
                return new Thickness(uniform);
            }
            else if (parts.Length == 4 &&
                    double.TryParse(parts[0], out double left) &&
                    double.TryParse(parts[1], out double top) &&
                    double.TryParse(parts[2], out double right) &&
                    double.TryParse(parts[3], out double bottom))
            {
                return new Thickness(left, top, right, bottom);
            }

            return new Thickness(0);
        }
    }
}