using System.Windows;
using System.Windows.Controls;
using MotorBike.ViewModels;

namespace MotorBike.Views;

public partial class BuyCarView : UserControl
{
    public BuyCarView() => InitializeComponent();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is BuyCarViewModel vm) vm.LoadRelatedDataCommand.Execute(null);
    }

    private void IsCash_Changed(object sender, RoutedEventArgs e)
    {
        if (DataContext is BuyCarViewModel vm)
        {
            vm.HandleCashModeChanged();
        }
    }

    private void CarSearch_GotFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is BuyCarViewModel vm)
        {
            if (string.IsNullOrWhiteSpace(vm.CarSearchText))
                vm.FilteredSourceCarsList = new System.Collections.ObjectModel.ObservableCollection<MotorBike.Models.Car>(vm.SourceCars);
            vm.IsCarSearchPopupOpen = vm.FilteredSourceCarsList.Count > 0;
        }
    }

    private void SearchCar_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem lbi && lbi.DataContext is MotorBike.Models.Car car && DataContext is BuyCarViewModel vm)
        {
            vm.SelectExistingCarCommand.Execute(car);
        }
    }

    private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
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
