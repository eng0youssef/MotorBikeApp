using System.Windows; 
using System.Windows.Controls; 
using MotorBike.ViewModels;

namespace MotorBike.Views;

public partial class ItemsView : UserControl
{
    public ItemsView() => InitializeComponent();

    private async void OnLoaded(object sender, RoutedEventArgs e)
    { 
        if (DataContext is ItemsViewModel vm) 
        {
            await vm.LoadDataCommand.ExecuteAsync(null); 
            await vm.LoadLookupsAsync();
        }
    }
}
