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

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is SalesViewModel vm)
        {
            await vm.LoadRelatedDataAsync();
            await vm.AddNewAsync(); // Default to new invoice on load
        }
    }

    private void IsCash_Changed(object sender, RoutedEventArgs e)
    {
        if (DataContext is SalesViewModel vm)
        {
            vm.HandleCashModeChanged();
        }
    }

    private void IsTax_Changed(object sender, RoutedEventArgs e)
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
        if (sender is ListBoxItem item && item.DataContext is MotorBike.Models.Item selectedItem)
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

    private void CustomerSearch_GotFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is SalesViewModel vm)
        {
            if (string.IsNullOrWhiteSpace(vm.CustomerSearchText))
                vm.FilteredCustomersList = new System.Collections.ObjectModel.ObservableCollection<MotorBike.Models.Customer>(vm.Customers);
            vm.IsCustomerSearchPopupOpen = vm.FilteredCustomersList.Count > 0;
        }
    }

    private void SearchCustomer_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem lbi && lbi.DataContext is MotorBike.Models.Customer customer && DataContext is SalesViewModel vm)
        {
            vm.SelectCustomerCommand.Execute(customer);
        }
    }

    private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        RefreshPopup(ItemSearchPopup);
        RefreshPopup(CustomerSearchPopup);
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

    private void DataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (DataContext is SalesViewModel vm)
        {
            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                vm.RecalculateTotals();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }
    }
}
