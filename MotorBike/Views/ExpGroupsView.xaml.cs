using System.Windows; using System.Windows.Controls; using MotorBike.ViewModels;
namespace MotorBike.Views;
public partial class ExpGroupsView : UserControl
{
    public ExpGroupsView() => InitializeComponent();
    private async void OnLoaded(object sender, RoutedEventArgs e)
    { if (DataContext is ExpGroupsViewModel vm) await vm.LoadDataCommand.ExecuteAsync(null); }
}
