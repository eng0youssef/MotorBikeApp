using System.Windows; using System.Windows.Controls; using MotorBike.ViewModels;
namespace MotorBike.Views;
public partial class StoresView : UserControl
{
    public StoresView() => InitializeComponent();
    private async void OnLoaded(object sender, RoutedEventArgs e)
    { if (DataContext is StoresViewModel vm) await vm.LoadDataCommand.ExecuteAsync(null); }
}
