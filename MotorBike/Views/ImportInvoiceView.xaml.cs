using System.Windows;
using System.Windows.Controls;
using MotorBike.ViewModels;

namespace MotorBike.Views;

public partial class ImportInvoiceView : UserControl
{
    public ImportInvoiceView() => InitializeComponent();

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ImportInvoiceViewModel vm)
            await vm.LoadAsync();
    }

    // ── Smart Supplier Search ────────────────────────────────────────────────

    private void SupplierSearch_GotFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is ImportInvoiceViewModel vm)
        {
            if (string.IsNullOrWhiteSpace(vm.SupplierSearchText))
                vm.FilteredSuppliersList = new System.Collections.ObjectModel.ObservableCollection<MotorBike.Models.ImportSupplier>(vm.Suppliers);
            vm.IsSupplierSearchPopupOpen = vm.FilteredSuppliersList.Count > 0;
        }
    }

    private void SearchSupplier_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem lbi
            && lbi.DataContext is MotorBike.Models.ImportSupplier supplier
            && DataContext is ImportInvoiceViewModel vm)
        {
            vm.SelectSupplierCommand.Execute(supplier);
        }
    }

    // ── Smart Item Search ────────────────────────────────────────────────────

    private void ItemSearch_GotFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is ImportInvoiceViewModel vm)
        {
            if (string.IsNullOrWhiteSpace(vm.ItemSearchText))
                vm.FilteredItemsList = new System.Collections.ObjectModel.ObservableCollection<MotorBike.Models.Item>(vm.ItemsList.Take(100));
            vm.IsItemSearchPopupOpen = vm.FilteredItemsList.Count > 0;
        }
    }

    private void SearchItem_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem lbi
            && lbi.DataContext is MotorBike.Models.Item item
            && DataContext is ImportInvoiceViewModel vm)
        {
            vm.SelectItemCommand.Execute(item);
        }
    }

    // ── Popup refresh on scroll ──────────────────────────────────────────────

    private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        RefreshPopup(SupplierSearchPopup);
        RefreshPopup(ItemSearchPopup);
    }

    private static void RefreshPopup(System.Windows.Controls.Primitives.Popup popup)
    {
        if (popup?.IsOpen == true)
        {
            var offset = popup.HorizontalOffset;
            popup.HorizontalOffset = offset + 1;
            popup.HorizontalOffset = offset;
        }
    }

    private void DataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (DataContext is ImportInvoiceViewModel vm)
        {
            string? header = e.Column.Header?.ToString();
            bool isPercentageEdit = header == "النسبة %";

            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                vm.RecalculateTotalsFromGrid(isPercentageEdit);
            }), System.Windows.Threading.DispatcherPriority.Background);
        }
    }
}
