using MotorBike.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace MotorBike.Views;

public partial class ActivationWindow : Window
{
    public ActivationWindow(ActivationViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RequestClose += (success) =>
        {
            this.DialogResult = success;
            this.Close();
        };
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }
    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }
}
