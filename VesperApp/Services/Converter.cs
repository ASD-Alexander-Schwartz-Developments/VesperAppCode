using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using AvaloniaEdit;
using System;
using System.Diagnostics;
using System.Globalization;
using static System.Net.Mime.MediaTypeNames;

namespace VesperApp.Services {
    public static class Converter {
        static readonly byte[] _DRtoXY = new byte[] {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 100, 91, 92, 93, 94, 95, 96, 97, 98, 11, 12, 13, 14, 21, 22, 23, 24, 31, 32, 33, 34, 41, 42, 43, 44, 51, 52, 53, 54, 61, 62, 63, 64, 71, 72, 73, 74, 81, 82, 83, 84, 15, 16, 17, 18, 25, 26, 27, 28, 35, 36, 37, 38, 45, 46, 47, 48, 55, 56, 57, 58, 65, 66, 67, 68, 75, 76, 77, 78, 85, 86, 87, 88, 89, 79, 69, 59, 49, 39, 29, 19, 80, 70, 60, 50, 40, 30, 20, 10, 1, 2, 3, 4, 5, 6, 7, 8, 0, 0, 0, 0};

        public static byte DRtoXY(int dr) => _DRtoXY[dr];
        public static byte XYtoDR(int xy) => (byte)Array.IndexOf(_DRtoXY, (byte)xy);
    }



    public class UpDownUintConverter : IValueConverter
    {

        public static readonly UpDownUintConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var str = value?.ToString();
            if (str == null)
            {
                Debug.WriteLine("Convert unset value");
                return AvaloniaProperty.UnsetValue;
            }
            /*if (UInt32.TryParse(str, NumberStyles.Number, CultureInfo.InvariantCulture, out uint x))
                return (decimal)x;*/

            if (targetType == typeof(string))
            {
                Debug.WriteLine("Convert string value: " + str);

                return str;
            }

            Debug.WriteLine("Convert " + targetType.FullName + " value: " + str);

            return AvaloniaProperty.UnsetValue;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            try
            {
                if (value is string && value != null)
                {
                    Debug.WriteLine("ConvertBack " + targetType.FullName + " value: " + value);

                    if (UInt32.TryParse((string)value, NumberStyles.Number, CultureInfo.InvariantCulture, out uint x))
                    {
                        Debug.WriteLine("ConvertBack to uint32 OK");

                        return (UInt32)x;
                    }
                }

                return AvaloniaProperty.UnsetValue;
            }
            catch (Exception cbex)
            {
                Debug.WriteLine("ConvertBack Exception " + targetType.FullName + " value: " + value.ToString() + " > " + cbex.Message);

                return AvaloniaProperty.UnsetValue;
            }

        }
    }
}