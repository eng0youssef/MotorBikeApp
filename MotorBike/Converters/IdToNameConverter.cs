using System;
using System.Collections;
using System.Globalization;
using System.Windows.Data;

namespace MotorBike.Converters;

public class IdToNameConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length == 2 && values[0] is int id && values[1] is IEnumerable collection && parameter is string props)
        {
            var parts = props.Split(',');
            if (parts.Length == 2)
            {
                string idProp = parts[0].Trim();
                string nameProp = parts[1].Trim();

                foreach (var item in collection)
                {
                    var type = item.GetType();
                    var propInfoId = type.GetProperty(idProp);
                    if (propInfoId != null && propInfoId.GetValue(item) is int itemId && itemId == id)
                    {
                        var propInfoName = type.GetProperty(nameProp);
                        return propInfoName?.GetValue(item)?.ToString() ?? string.Empty;
                    }
                }
            }
        }
        return string.Empty;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
