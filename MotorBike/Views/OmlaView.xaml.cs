using System.Windows; using System.Windows.Controls; using MotorBike.ViewModels;
namespace MotorBike.Views;
public partial class OmlaView : UserControl
{
    public OmlaView() => InitializeComponent();
    private async void OnLoaded(object sender, RoutedEventArgs e)
    { if (DataContext is OmlaViewModel vm) await vm.LoadDataCommand.ExecuteAsync(null); }
}
