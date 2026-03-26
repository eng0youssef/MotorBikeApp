using System.Windows;
using System.Windows.Controls;
using MotorBike.ViewModels;

namespace MotorBike.Views;

public partial class ReSalesView : UserControl
{
    public ReSalesView() => InitializeComponent();
    private async void OnLoaded(object sender, RoutedEventArgs e) 
    { 
        if (DataContext is ReSalesViewModel vm) 
        {
            await vm.LoadRelatedDataAsync();
            await vm.AddNewAsync();
        }
    }
    private void IsCash_Changed(object sender, RoutedEventArgs e) { if (DataContext is ReSalesViewModel vm) vm.RecalculateTotals(); }
    private void IsTax_Changed(object sender, RoutedEventArgs e) { if (DataContext is ReSalesViewModel vm) vm.RecalculateTotals(); }
    private void Totals_TextChanged(object sender, TextChangedEventArgs e) { if (DataContext is ReSalesViewModel vm) vm.RecalculateTotals(); }
    private void SearchItem_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem lbi && lbi.DataContext is MotorBike.Models.Item item && DataContext is ReSalesViewModel vm)
            vm.SelectItemCommand.Execute(item);
    }

    private void ItemSearch_GotFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is ReSalesViewModel vm)
        {
            if (string.IsNullOrWhiteSpace(vm.ItemSearchText))
                vm.FilteredItemsList = new System.Collections.ObjectModel.ObservableCollection<MotorBike.Models.Item>(vm.Items.Take(100));
            vm.IsItemSearchPopupOpen = vm.FilteredItemsList.Count > 0;
        }
    }

    private void CustomerSearch_GotFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is ReSalesViewModel vm)
        {
            if (string.IsNullOrWhiteSpace(vm.CustomerSearchText))
                vm.FilteredCustomersList = new System.Collections.ObjectModel.ObservableCollection<MotorBike.Models.Customer>(vm.Customers.Take(100));
            vm.IsCustomerSearchPopupOpen = vm.FilteredCustomersList.Count > 0;
        }
    }

    private void SearchCustomer_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem lbi && lbi.DataContext is MotorBike.Models.Customer customer && DataContext is ReSalesViewModel vm)
            vm.SelectCustomerCommand.Execute(customer);
    }

    private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (ItemSearchPopup.IsOpen || CustomerSearchPopup.IsOpen)
        {
            var offset = ItemSearchPopup.HorizontalOffset;
            ItemSearchPopup.HorizontalOffset = offset + 1;
            ItemSearchPopup.HorizontalOffset = offset;
            
            offset = CustomerSearchPopup.HorizontalOffset;
            CustomerSearchPopup.HorizontalOffset = offset + 1;
            CustomerSearchPopup.HorizontalOffset = offset;
        }
    }
}
