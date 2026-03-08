using System.Windows; 
using System.Windows.Controls; 
using MotorBike.ViewModels;

namespace MotorBike.Views;

public partial class ExpensesView : UserControl
{
    public ExpensesView() => InitializeComponent();

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ExpensesViewModel vm) 
        {
            await vm.LoadDataCommand.ExecuteAsync(null); 
            await vm.LoadLookupsAsync();
        }
    }
}
