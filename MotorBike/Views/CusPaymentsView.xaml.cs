using System.Windows;
using System.Windows.Controls;
using MotorBike.ViewModels;

namespace MotorBike.Views;

public partial class CusPaymentsView : UserControl
{
    public CusPaymentsView() => InitializeComponent();
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is CusPaymentsViewModel vm)
        {
            await vm.LoadDataAsync();
            await vm.LoadRelatedDataAsync();
        }
    }
}
