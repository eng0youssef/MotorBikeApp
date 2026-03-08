using System.Windows;
using System.Windows.Controls;
using MotorBike.ViewModels;

namespace MotorBike.Views;

public partial class CarModelsView : UserControl
{
    public CarModelsView()
    {
        InitializeComponent();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is CarModelsViewModel vm)
        {
            await vm.LoadBrandsCommand.ExecuteAsync(null);
            await vm.LoadDataCommand.ExecuteAsync(null);
        }
    }
}
