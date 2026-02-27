using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace TDFMAUI.Converters
{
    /// <summary>
    /// Converts a DateTime to a "time ago" string (e.g., "5 minutes ago")
    /// </summary>
    public class TimeAgoConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime dateTime)
            {
                var timeSpan = DateTime.Now.Subtract(dateTime);

                if (timeSpan.TotalSeconds < 60)
                    return "just now";
                
                if (timeSpan.TotalMinutes < 60)
                {
                    var minutes = (int)timeSpan.TotalMinutes;
                    return $"{minutes} {(minutes == 1 ? "minute" : "minutes")} ago";
                }
                
                if (timeSpan.TotalHours < 24)
                {
                    var hours = (int)timeSpan.TotalHours;
                    return $"{hours} {(hours == 1 ? "hour" : "hours")} ago";
                }
                
                if (timeSpan.TotalDays < 7)
                {
                    var days = (int)timeSpan.TotalDays;
                    return $"{days} {(days == 1 ? "day" : "days")} ago";
                }
                
                if (timeSpan.TotalDays < 30)
                {
                    var weeks = (int)(timeSpan.TotalDays / 7);
                    return $"{weeks} {(weeks == 1 ? "week" : "weeks")} ago";
                }
                
                // For older dates, just return the actual date
                return dateTime.ToString("MMM d, yyyy");
            }
            
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Not intended to be used for two-way binding
            throw new NotSupportedException();
        }
    }
} 