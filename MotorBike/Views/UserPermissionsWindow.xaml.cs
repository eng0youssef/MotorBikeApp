using System.Windows;
using MotorBike.ViewModels;

namespace MotorBike.Views;

public partial class UserPermissionsWindow : Window
{
    public UserPermissionsWindow(UserPermissionsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RequestClose = Close;
        Loaded += (s, e) => { _ = viewModel.LoadAsync(); };
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
