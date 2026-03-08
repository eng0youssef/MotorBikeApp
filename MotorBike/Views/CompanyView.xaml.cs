using System.Windows;
using System.Windows.Controls;
using MotorBike.ViewModels;

namespace MotorBike.Views;

public partial class CompanyView : UserControl
{
    public CompanyView()
    {
        InitializeComponent();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is CompanyViewModel vm)
            await vm.LoadDataCommand.ExecuteAsync(null);
    }
}
