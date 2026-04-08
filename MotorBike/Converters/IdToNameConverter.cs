using System;
using System.Collections;
using System.Globalization;
using System.Windows.Data;

namespace MotorBike.Converters;

public class IdToNameConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length == 2 && values[0] is int id && values[1] is IEnumerable collection)
        {
            foreach (var item in collection)
            {
                var type = item.GetType();

                var idProp = type.GetProperty("CashId");
                var nameProp = type.GetProperty("CashName");

                if (idProp != null && nameProp != null)
                {
                    var itemId = idProp.GetValue(item);
                    if (itemId is int i && i == id)
                        return nameProp.GetValue(item)?.ToString() ?? "";
                }
            }
        }
        return "";
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
