using System.Windows;
using System.Windows.Controls;
using MotorBike.ViewModels;

namespace MotorBike.Views;

public partial class SalesView : UserControl
{
    public SalesView()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is SalesViewModel vm)
        {
            vm.LoadRelatedDataCommand.Execute(null);
        }
    }

    private void IsCash_Changed(object sender, RoutedEventArgs e)
    {
        if (DataContext is SalesViewModel vm)
        {
            vm.RecalculateTotals();
        }
    }

    private void Totals_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (DataContext is SalesViewModel vm && sender is TextBox textBox)
        {
            if (textBox.IsFocused)
            {
                int caret = textBox.CaretIndex;
                vm.RecalculateTotals();
                textBox.CaretIndex = caret;
            }
        }
    }

    private void SearchItem_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.ListBoxItem item && item.DataContext is MotorBike.Models.Item selectedItem)
        {
            if (DataContext is SalesViewModel vm)
            {
                vm.SelectItemCommand.Execute(selectedItem);
            }
        }
    }

    private void ItemSearch_GotFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is SalesViewModel vm)
        {
            if (string.IsNullOrWhiteSpace(vm.ItemSearchText))
            {
                vm.FilteredItemsList = new System.Collections.ObjectModel.ObservableCollection<MotorBike.Models.Item>(vm.Items.Take(100));
            }
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
