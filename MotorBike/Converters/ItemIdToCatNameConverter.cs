using System;
using System.Collections;
using System.Globalization;
using System.Windows.Data;
using MotorBike.Models;

namespace MotorBike.Converters;

/// <summary>
/// MultiValueConverter يأخذ (ItemId, ItemList, Categories) ويعيد اسم مجموعة الصنف.
/// Usage: values[0] = ItemId (int), values[1] = ItemList (IEnumerable&lt;Item&gt;), values[2] = Categories (IEnumerable&lt;ItemCategory&gt;)
/// </summary>
public class ItemIdToCatNameConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length != 3) return "";

        int itemId;
        try { itemId = System.Convert.ToInt32(values[0]); }
        catch { return ""; }

        if (values[1] is not IEnumerable itemList) return "";
        if (values[2] is not IEnumerable categories) return "";

        // ابحث عن الصنف في ItemList
        int catId = 0;
        foreach (var obj in itemList)
        {
            if (obj is Item item && item.ItemId == itemId)
            {
                catId = item.CatId;
                break;
            }
        }

        if (catId == 0) return "";

        // ابحث عن المجموعة في Categories
        foreach (var obj in categories)
        {
            if (obj is ItemCategory cat && cat.CatId == catId)
                return cat.CatName;
        }

        return "";
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
