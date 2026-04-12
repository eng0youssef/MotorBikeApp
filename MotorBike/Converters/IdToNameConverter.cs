using System;
using System.Collections;
using System.Globalization;
using System.Windows.Data;

namespace MotorBike.Converters;

public class IdToNameConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length != 2 || values[0] is not int id || values[1] is not IEnumerable collection)
            return "";

        // Parse "IdPropertyName,NamePropertyName" from ConverterParameter
        string idPropName = "CashId";
        string namePropName = "CashName";

        if (parameter is string paramStr)
        {
            var parts = paramStr.Split(',');
            if (parts.Length == 2)
            {
                idPropName = parts[0].Trim();
                namePropName = parts[1].Trim();
            }
        }

        foreach (var item in collection)
        {
            if (item == null) continue;
            var type = item.GetType();
            var idProp = type.GetProperty(idPropName);
            var nameProp = type.GetProperty(namePropName);

            if (idProp != null && nameProp != null && idProp.GetValue(item) is int i && i == id)
                return nameProp.GetValue(item)?.ToString() ?? "";
        }

        return "";
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}