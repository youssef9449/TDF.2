using System;
using System.Globalization;
using System.IO;
using Microsoft.Maui.Controls;

namespace TDFMAUI.Converters
{
    /// <summary>
    /// Converts a byte array to an ImageSource for use in XAML
    /// </summary>
    public class ByteArrayToImageSourceConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is byte[] imageData && imageData.Length > 0)
            {
                return ImageSource.FromStream(() => new MemoryStream(imageData));
            }
            
            // Return default image or null
            return ImageSource.FromFile("default_profile.png");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // ConvertBack not implemented as we don't need to convert from ImageSource to byte[]
            throw new NotImplementedException();
        }
    }
} 