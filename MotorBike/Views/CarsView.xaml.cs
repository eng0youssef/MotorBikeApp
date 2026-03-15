using System.Windows;
using System.Windows.Controls;
using MotorBike.ViewModels;

namespace MotorBike.Views;

public partial class CarsView : UserControl
{
    public CarsView()
    {
        InitializeComponent();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is CarsViewModel vm)
        {
            await vm.LoadRelatedDataAsync();
            await vm.LoadDataAsync();
        }
    }
}
