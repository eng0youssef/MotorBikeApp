using System;
using System.Globalization;
using System.Windows.Data;

namespace MotorBike.Converters;

public class CashTypeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is byte typeVal)
        {
            return typeVal switch
            {
                0 => "خزينة",
                1 => "بنك",
                _ => typeVal.ToString()
            };
        }
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
