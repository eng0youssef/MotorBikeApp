using System.Windows;
using System.Windows.Controls;
using MotorBike.ViewModels;
namespace MotorBike.Views;
public partial class UnitsView : UserControl
{
    public UnitsView() => InitializeComponent();
    private async void OnLoaded(object sender, RoutedEventArgs e)
    { if (DataContext is UnitsViewModel vm) await vm.LoadDataCommand.ExecuteAsync(null); }
}
