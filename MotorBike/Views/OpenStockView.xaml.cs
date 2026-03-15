using System.Windows;
using System.Windows.Controls;
using MotorBike.ViewModels;

namespace MotorBike.Views;

public partial class OpenStockView : UserControl
{
    public OpenStockView() => InitializeComponent();

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is OpenStockViewModel vm)
        {
            await vm.LoadDataAsync();
            await vm.LoadRelatedDataAsync();
        }
    }
}
