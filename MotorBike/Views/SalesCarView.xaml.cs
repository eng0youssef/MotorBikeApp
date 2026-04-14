using System.Windows;
using System.Windows.Controls;
using MotorBike.ViewModels;

namespace MotorBike.Views;

public partial class SalesCarView : UserControl
{
    public SalesCarView() => InitializeComponent();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is SalesCarViewModel vm) vm.LoadRelatedDataCommand.Execute(null);
    }
    private void IsCash_Changed(object sender, RoutedEventArgs e)
    {
        if (DataContext is SalesCarViewModel vm)
        {
            vm.HandleCashModeChanged();
        }
    }

    private void CustomerSearch_GotFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is SalesCarViewModel vm)
        {
            if (string.IsNullOrWhiteSpace(vm.CustomerSearchText))
                vm.FilteredCustomersList = new System.Collections.ObjectModel.ObservableCollection<MotorBike.Models.Customer>(vm.Customers.Take(100));
            vm.IsCustomerPopupOpen = vm.FilteredCustomersList.Count > 0;
        }
    }

    private void SearchCustomer_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem lbi && lbi.DataContext is MotorBike.Models.Customer customer && DataContext is SalesCarViewModel vm)
        {
            vm.SelectCustomerCommand.Execute(customer);
        }
    }

    private void CarSearch_GotFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is SalesCarViewModel vm)
        {
            if (string.IsNullOrWhiteSpace(vm.CarSearchText))
                vm.FilteredCarsList = new System.Collections.ObjectModel.ObservableCollection<MotorBike.Models.Car>(vm.Cars.Take(100));
            vm.IsCarPopupOpen = vm.FilteredCarsList.Count > 0;
        }
    }

    private void SearchCar_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem lbi && lbi.DataContext is MotorBike.Models.Car car && DataContext is SalesCarViewModel vm)
        {
            vm.SelectCarCommand.Execute(car);
        }
    }

    private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (CustomerSearchPopup != null) RefreshPopup(CustomerSearchPopup);
        if (CarSearchPopup != null) RefreshPopup(CarSearchPopup);
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
}
