using System.Windows;
using System.Windows.Controls;
using MotorBike.ViewModels;

namespace MotorBike.Views;

public partial class CashView : UserControl
{
    public CashView()
    {
        InitializeComponent();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is CashViewModel vm)
        {
            await vm.LoadCurrenciesCommand.ExecuteAsync(null);
            await vm.LoadDataCommand.ExecuteAsync(null);
        }
    }

    private void CashTypeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is CashViewModel vm && sender is ComboBox combo && vm.FormItem is not null)
        {
            vm.FormItem.CashType = (byte)combo.SelectedIndex;
        }
    }

    private void CurrencyChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is CashViewModel vm && sender is ComboBox combo && combo.SelectedItem is MotorBike.Models.Omla selectedCurrency && vm.FormItem is not null)
        {
            vm.FormItem.OmlaRate = selectedCurrency.OmlaRate;
            txtOmlaRate.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
        }
    }

    private void CalculateBalance(object sender, TextChangedEventArgs e)
    {
        if (DataContext is CashViewModel vm && vm.FormItem is not null)
        {
            double.TryParse(txtDebit.Text, out double debit);
            double.TryParse(txtCredit.Text, out double credit);
            vm.FormItem.Bal = debit - credit;
            
            // Refresh the binding target without breaking the binding
            txtBal.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
        }
    }
}
