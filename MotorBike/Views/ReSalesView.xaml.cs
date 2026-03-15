using System.Windows;
using System.Windows.Controls;
using MotorBike.ViewModels;

namespace MotorBike.Views;

public partial class ReSalesView : UserControl
{
    public ReSalesView() => InitializeComponent();
    private void OnLoaded(object sender, RoutedEventArgs e) { if (DataContext is ReSalesViewModel vm) vm.LoadRelatedDataCommand.Execute(null); }
    private void IsCash_Changed(object sender, RoutedEventArgs e) { }
    private void Totals_TextChanged(object sender, TextChangedEventArgs e) { if (DataContext is ReSalesViewModel vm) vm.RecalculateTotals(); }
    private void SearchItem_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem lbi && lbi.DataContext is MotorBike.Models.Item item && DataContext is ReSalesViewModel vm)
            vm.SelectItemCommand.Execute(item);
    }
}
