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
}
