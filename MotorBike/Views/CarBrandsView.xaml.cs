using System.Windows;
using System.Windows.Controls;
using MotorBike.ViewModels;

namespace MotorBike.Views;

public partial class CarBrandsView : UserControl
{
    public CarBrandsView() => InitializeComponent();
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is CarBrandsViewModel vm) await vm.LoadDataCommand.ExecuteAsync(null);
    }
}
