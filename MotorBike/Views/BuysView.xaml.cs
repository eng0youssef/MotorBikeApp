using System.Windows;
using System.Windows.Controls;
using MotorBike.ViewModels;

namespace MotorBike.Views;

public partial class BuysView : UserControl
{
    public BuysView()
    {
        InitializeComponent();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is BuysViewModel vm)
        {
            await vm.LoadRelatedDataAsync();
            await vm.AddNewAsync(); // Default to new invoice on load
        }
    }

    private void IsCash_Changed(object sender, RoutedEventArgs e)
    {
        if (DataContext is BuysViewModel vm)
        {
            vm.HandleCashModeChanged();
        }
    }

    private void Totals_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (DataContext is BuysViewModel vm && sender is TextBox textBox)
        {
            if (textBox.IsFocused)
            {
                int caret = textBox.CaretIndex;
                vm.RecalculateTotals();
                textBox.CaretIndex = caret;
            }
        }
    }

    private void ItemSearch_GotFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is BuysViewModel vm)
        {
            if (string.IsNullOrWhiteSpace(vm.ItemSearchText))
                vm.FilteredItemsList = new System.Collections.ObjectModel.ObservableCollection<MotorBike.Models.Item>(vm.Items.Take(100));
            vm.IsItemSearchPopupOpen = vm.FilteredItemsList.Count > 0;
        }
    }

    private void SupplierSearch_GotFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is BuysViewModel vm)
        {
            if (string.IsNullOrWhiteSpace(vm.SupplierSearchText))
                vm.FilteredSuppliersList = new System.Collections.ObjectModel.ObservableCollection<MotorBike.Models.Supplier>(vm.Suppliers);
            vm.IsSupplierSearchPopupOpen = vm.FilteredSuppliersList.Count > 0;
        }
    }

    private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        RefreshPopup(ItemSearchPopup);
        RefreshPopup(SupplierSearchPopup);
    }

    private static void RefreshPopup(System.Windows.Controls.Primitives.Popup popup)
    {
        if (popup.IsOpen)
        {
            var offset = popup.HorizontalOffset;
            popup.HorizontalOffset = offset + 1;
            popup.HorizontalOffset = offset;
        }
    }

    private void SearchItem_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem lbi && lbi.DataContext is MotorBike.Models.Item item && DataContext is BuysViewModel vm)
        {
            vm.SelectItemCommand.Execute(item);
        }
    }

    private void SearchSupplier_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem lbi && lbi.DataContext is MotorBike.Models.Supplier supplier && DataContext is BuysViewModel vm)
        {
            vm.SelectSupplierCommand.Execute(supplier);
        }
    }

    private void DataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (DataContext is BuysViewModel vm)
        {
            // Dispatch recalculation after cell value is committed
            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                vm.RecalculateTotals();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }
    }
}
