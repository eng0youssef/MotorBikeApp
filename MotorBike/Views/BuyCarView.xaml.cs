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
}
