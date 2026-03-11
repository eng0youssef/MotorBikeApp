using System.Windows;
using System.Windows.Controls;
using MotorBike.ViewModels;

namespace MotorBike.Views;

public partial class BuysView : UserControl
{
    public BuysView()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is BuysViewModel vm)
        {
            vm.LoadRelatedDataCommand.Execute(null);
        }
    }

    private void IsCash_Changed(object sender, RoutedEventArgs e)
    {
        if (DataContext is BuysViewModel vm)
        {
            vm.RecalculateTotals();
        }
    }

    private void Totals_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (DataContext is BuysViewModel vm && sender is TextBox textBox)
        {
            if (textBox.IsFocused)
            {
                int caret = textBox.CaretIndex;
                vm.RecalculateTotals();
                textBox.CaretIndex = caret;
            }
        }
    }
}
