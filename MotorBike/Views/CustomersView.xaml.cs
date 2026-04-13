using System.Windows;
using System.Windows.Controls;
using MotorBike.ViewModels;

namespace MotorBike.Views;

public partial class CustomersView : UserControl
{
    public CustomersView()
    {
        InitializeComponent();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is CustomersViewModel vm)
        {
            await vm.LoadRelatedDataAsync();
            await vm.LoadDataAsync();
        }
    }

    private void txtDebitCredit_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (DataContext is CustomersViewModel vm && vm.FormItem != null)
        {
            double.TryParse(txtDebit.Text, out double debit);
            double.TryParse(txtCredit.Text, out double credit);
            double bal = debit - credit;
            vm.FormItem.Debit = debit;
            vm.FormItem.Credit = credit;
            vm.FormItem.Bal = bal;
            txtBal.Text = bal.ToString("0.##"); // Format to avoid overly long decimals
        }
    }
}
