using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace TDFMAUI.Converters
{
    public class LeaveTypeToEndDateVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string leaveType)
            {
                // Hide end date for Permission and External Assignment
                return !(leaveType == "Permission" || leaveType == "ExternalAssignment");
            }
            return true; // Default to showing end date
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Not needed for one-way binding
            throw new NotImplementedException();
        }
    }
}