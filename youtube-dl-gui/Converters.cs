using System;
using System.Windows.Data;
using System.Globalization;
using youtube_dl_gui.Youtube;

namespace youtube_dl_gui.Converters
{
    [ValueConversion(typeof(bool), typeof(bool))]
    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (targetType != typeof(bool))
                throw new InvalidOperationException("The target must be a boolean");

            return !(bool)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter,
            System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    [ValueConversion(typeof(string), typeof(bool))]
    public class EmptyStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !string.IsNullOrWhiteSpace((string)value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }


    [ValueConversion(typeof(string), typeof(string))]
    public class CodecToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string codecString)
            {
                return GetFriendlyFormatExtension(codecString);
            }
            return "Unknown";
        }

        static string GetFriendlyFormatExtension(string formatString)
        {
            if (string.IsNullOrEmpty(formatString))
            {
                return "Unknown";
            }
            else if (formatString.Equals("opus", StringComparison.OrdinalIgnoreCase))
            {
                return "Opus";
            }
            else if (formatString.Equals("vorbis", StringComparison.OrdinalIgnoreCase))
            {
                return "Vorbis";
            }
            else if (formatString.Equals("vp9", StringComparison.OrdinalIgnoreCase))
            {
                return "VP9";
            }
            else if (formatString.Equals("vp8.0", StringComparison.OrdinalIgnoreCase) || formatString.Equals("vp8", StringComparison.OrdinalIgnoreCase))
            {
                return "VP8";
            }
            else if (formatString.StartsWith("avc1", StringComparison.OrdinalIgnoreCase))
            {
                return "H264";
            }
            else if (formatString.StartsWith("av01", StringComparison.OrdinalIgnoreCase))
            {
                return "AV1";
            }
            else if (formatString.StartsWith("mp4a", StringComparison.OrdinalIgnoreCase))
            {
                return "M4A";
            }
            else
            {
                return formatString.FirstLetterToUpper();
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
