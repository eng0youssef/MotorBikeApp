using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MotorBike.Converters;

/// <summary>
/// يحوّل النص إلى Visibility:
/// - نص فارغ أو null  → Collapsed (مخفي)
/// - نص فيه قيمة     → Visible (ظاهر)
/// </summary>
public class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => string.IsNullOrWhiteSpace(value as string)
            ? Visibility.Collapsed
            : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
