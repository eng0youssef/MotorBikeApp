using System.Windows;
using System.Windows.Controls;
using MotorBike.ViewModels;

namespace MotorBike.Views;

public partial class ImportInvoiceView : UserControl
{
    public ImportInvoiceView() => InitializeComponent();

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ImportInvoiceViewModel vm)
            await vm.LoadAsync();
    }
}
