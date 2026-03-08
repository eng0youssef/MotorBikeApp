using System;
using System.Globalization;
using System.Windows.Data;

namespace MotorBike.Converters;

public class HideZeroConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double d && d == 0) return string.Empty;
        if (value is int i && i == 0) return string.Empty;
        if (value is float f && f == 0) return string.Empty;
        if (value is decimal m && m == 0m) return string.Empty;

        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string text = value as string;

        if (string.IsNullOrWhiteSpace(text))
        {
            if (targetType == typeof(double) || targetType == typeof(double?)) return 0d;
            if (targetType == typeof(int) || targetType == typeof(int?)) return 0;
            if (targetType == typeof(float) || targetType == typeof(float?)) return 0f;
            if (targetType == typeof(decimal) || targetType == typeof(decimal?)) return 0m;
            return null;
        }

        return value;
    }
}
