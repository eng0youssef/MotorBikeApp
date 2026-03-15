using System.Windows;
using System.Windows.Controls;
using MotorBike.ViewModels;

namespace MotorBike.Views;

public partial class CashTransfersView : UserControl
{
    public CashTransfersView() => InitializeComponent();
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is CashTransfersViewModel vm)
        {
            await vm.LoadDataAsync();
            await vm.LoadRelatedDataAsync();
        }
    }
}
