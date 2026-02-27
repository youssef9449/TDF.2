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
            try
            {
                if (value is byte[] imageData && imageData.Length > 0)
                {
                    return ImageSource.FromStream(() => new MemoryStream(imageData));
                }
            }
            catch (Exception)
            {
                // If there's an error loading the image data, fall back to default
            }
            
            // Return default image when no data or error occurs
            return ImageSource.FromFile("default_profile.png");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // ConvertBack not supported as we don't need to convert from ImageSource to byte[]
            throw new NotSupportedException();
        }
    }
} 