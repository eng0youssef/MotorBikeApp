namespace MotorBike.Views;

public partial class DbConnectionSetupWindow : System.Windows.Window
{
    public DbConnectionSetupWindow(ViewModels.DbConnectionSetupViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
