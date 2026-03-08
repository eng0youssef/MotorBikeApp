using System.Windows;
using MotorBike.ViewModels;

namespace MotorBike;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}