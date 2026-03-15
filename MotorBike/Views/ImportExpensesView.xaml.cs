using System.Windows;
using System.Windows.Controls;
using MotorBike.ViewModels;

namespace MotorBike.Views;

public partial class ImportExpensesView : UserControl
{
    public ImportExpensesView() => InitializeComponent();
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ImportExpensesViewModel vm) await vm.LoadDataAsync();
    }
}
