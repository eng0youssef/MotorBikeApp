using System.Windows;
using System.Windows.Controls;
using MotorBike.ViewModels;

namespace MotorBike.Views;

public partial class ExpPaymentsView : UserControl
{
    public ExpPaymentsView() => InitializeComponent();
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ExpPaymentsViewModel vm)
        {
            await vm.LoadDataAsync();
            await vm.LoadRelatedDataAsync();
        }
    }
}
