using System.Windows.Controls;

namespace MotorBike.Views;

public partial class BasicDataView : UserControl
{
    public BasicDataView()
    {
        InitializeComponent();
    }

    private bool _isUpdatingPassword;

    private void TxtPasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_isUpdatingPassword) return;
        _isUpdatingPassword = true;
        TxtTextBox.Text = TxtPasswordBox.Password;
        _isUpdatingPassword = false;
    }

    private void TxtTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingPassword) return;
        _isUpdatingPassword = true;
        TxtPasswordBox.Password = TxtTextBox.Text;
        _isUpdatingPassword = false;
    }

    private void BtnTogglePassword_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (TxtPasswordBox.Visibility == System.Windows.Visibility.Visible)
        {
            TxtPasswordBox.Visibility = System.Windows.Visibility.Collapsed;
            TxtTextBox.Visibility = System.Windows.Visibility.Visible;
            TxtEyeIcon.Text = "🙈"; 
        }
        else
        {
            TxtPasswordBox.Visibility = System.Windows.Visibility.Visible;
            TxtTextBox.Visibility = System.Windows.Visibility.Collapsed;
            TxtEyeIcon.Text = "👁️";
        }
    }
}
