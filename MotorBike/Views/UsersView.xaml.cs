using System.Windows;
using System.Windows.Controls;
using MotorBike.ViewModels;

namespace MotorBike.Views;

public partial class UsersView : UserControl
{
    public UsersView() => InitializeComponent();
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is UsersViewModel vm) await vm.LoadDataCommand.ExecuteAsync(null);
    }
}
