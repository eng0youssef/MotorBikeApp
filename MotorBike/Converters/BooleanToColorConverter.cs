using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MotorBike.Converters;

public class BooleanToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b && b)
        {
            return new SolidColorBrush(Color.FromRgb(91, 174, 124)); // Success Green
        }
        return new SolidColorBrush(Color.FromRgb(217, 83, 79)); // Error Red
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
