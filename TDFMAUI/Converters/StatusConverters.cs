using Microsoft.Maui.Controls;
using System.Globalization;

namespace TDFMAUI.Converters
{
    public class IsPendingConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string status)
            {
                return status.Equals("Pending", StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // One-way binding only; no conversion back from bool to status.
            return Binding.DoNothing;
        }
    }
} 