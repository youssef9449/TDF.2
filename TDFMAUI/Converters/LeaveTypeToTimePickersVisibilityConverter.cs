using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace TDFMAUI.Converters
{
    public class LeaveTypeToTimePickersVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string leaveType)
            {
                return leaveType == "Permission" || leaveType == "External Assignment";
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Not needed for one-way binding
            throw new NotImplementedException();
        }
    }
}