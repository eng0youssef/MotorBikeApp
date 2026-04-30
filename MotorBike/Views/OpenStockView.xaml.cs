using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MotorBike.Models;
using MotorBike.ViewModels;

namespace MotorBike.Views;

public partial class OpenStockView : UserControl
{
    public OpenStockView() => InitializeComponent();

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is OpenStockViewModel vm)
        {
            await vm.LoadDataAsync();
            await vm.LoadRelatedDataAsync();
        }
    }

    // عند الضغط داخل TextBox البحث: افتح الـ Popup لو في نص
    private void ItemSearchBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is OpenStockViewModel vm && !string.IsNullOrWhiteSpace(vm.ItemSearchText))
            vm.IsItemPopupOpen = vm.ItemSearchResults.Count > 0;
    }

    // ضغط المسطرة = عرض كل الأصناف في الـ Popup
    private void ItemSearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space && DataContext is OpenStockViewModel vm)
        {
            vm.ItemSearchText = string.Empty; // نمسح الفلتر عشان يظهر الكل
            vm.ShowAllItemsCommand.Execute(null);
            e.Handled = true; // نمنع إضافة مسافة في النص
        }
    }

    // عند الخروج من TextBox: أغلق الـ Popup بتأخير بسيط لإتاحة Click على النتيجة
    private async void ItemSearchBox_LostFocus(object sender, RoutedEventArgs e)
    {
        await System.Threading.Tasks.Task.Delay(150);
        if (DataContext is OpenStockViewModel vm)
            vm.IsItemPopupOpen = false;
    }

    // عند اختيار صنف من قائمة النتائج
    private void ItemResult_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem lbi && lbi.DataContext is Item item
            && DataContext is OpenStockViewModel vm)
        {
            vm.SelectItemCommand.Execute(item);
        }
    }
}
