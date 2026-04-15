using System;
using System.Collections;
using System.Globalization;
using System.Windows.Data;

namespace MotorBike.Converters;

public class IdToNameConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length != 2 || values[0] == null || values[1] is not IEnumerable collection)
            return "";

        // ✅ تحويل موحد بدل الاشتراط الصارم على int
        int id;
        try { id = System.Convert.ToInt32(values[0]); }
        catch { return ""; }

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
            if (idProp != null && nameProp != null)
            {
                // ✅ مقارنة موحدة بدل is int i
                try
                {
                    int itemId = System.Convert.ToInt32(idProp.GetValue(item));
                    if (itemId == id)
                        return nameProp.GetValue(item)?.ToString() ?? "";
                }
                catch { continue; }
            }
        }
        return "";
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}