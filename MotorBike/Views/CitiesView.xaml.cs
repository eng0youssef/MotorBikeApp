using System.Windows; using System.Windows.Controls; using MotorBike.ViewModels;
namespace MotorBike.Views;
public partial class CitiesView : UserControl
{
    public CitiesView() => InitializeComponent();
    private async void OnLoaded(object sender, RoutedEventArgs e)
    { if (DataContext is CitiesViewModel vm) await vm.LoadDataCommand.ExecuteAsync(null); }
}
