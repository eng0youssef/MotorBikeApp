using System.Windows;
using System.Windows.Controls;
using MotorBike.ViewModels;

namespace MotorBike.Views;

public partial class ReBuyView : UserControl
{
    public ReBuyView() => InitializeComponent();
    private void OnLoaded(object sender, RoutedEventArgs e) { if (DataContext is ReBuyViewModel vm) vm.LoadRelatedDataCommand.Execute(null); }
    private void IsCash_Changed(object sender, RoutedEventArgs e) { }
    private void Totals_TextChanged(object sender, TextChangedEventArgs e) { if (DataContext is ReBuyViewModel vm) vm.RecalculateTotals(); }
    private void SearchItem_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem lbi && lbi.DataContext is MotorBike.Models.Item item && DataContext is ReBuyViewModel vm)
            vm.SelectItemCommand.Execute(item);
    }

    private void ItemSearch_GotFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is ReBuyViewModel vm)
        {
            if (string.IsNullOrWhiteSpace(vm.ItemSearchText))
                vm.FilteredItemsList = new System.Collections.ObjectModel.ObservableCollection<MotorBike.Models.Item>(vm.Items.Take(100));
            vm.IsItemSearchPopupOpen = vm.FilteredItemsList.Count > 0;
        }
    }

    private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (ItemSearchPopup.IsOpen)
        {
            var offset = ItemSearchPopup.HorizontalOffset;
            ItemSearchPopup.HorizontalOffset = offset + 1;
            ItemSearchPopup.HorizontalOffset = offset;
        }
    }
}
