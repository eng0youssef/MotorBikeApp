using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using MotorBike.ViewModels;

namespace MotorBike.Views;

public partial class LoginWindow : Window
{
    private readonly LoginViewModel _viewModel;

    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        
        // Setup success callback
        _viewModel.OnLoginSuccess = () =>
        {
            var mainWindow = App.Services.GetRequiredService<MainWindow>();
            Application.Current.MainWindow = mainWindow;
            mainWindow.Show();
            this.Close();
        };
    }

    private void txtPassword_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm)
        {
            vm.Password = ((PasswordBox)sender).Password;
        }
    }

    private void txtUserName_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            txtPassword.Focus();
        }
    }

    private void txtPassword_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (_viewModel.LoginCommand.CanExecute(null))
            {
                _viewModel.LoginCommand.Execute(null);
            }
        }
    }

    private void btnActivation_Click(object sender, RoutedEventArgs e)
    {
        var activationWindow = App.Services.GetRequiredService<ActivationWindow>();
        activationWindow.ShowDialog();
    }
}
