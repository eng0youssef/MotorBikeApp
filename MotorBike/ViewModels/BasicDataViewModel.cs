using CommunityToolkit.Mvvm.ComponentModel;

namespace MotorBike.ViewModels;

public partial class BasicDataViewModel : ObservableObject
{
    private readonly MotorBike.DataAccess.IDbConnectionFactory _db;

    [ObservableProperty]
    private string _currentPassword = string.Empty;

    public BasicDataViewModel(MotorBike.DataAccess.IDbConnectionFactory db)
    {
        _db = db;
        _ = LoadCurrentPasswordAsync();
    }

    private async System.Threading.Tasks.Task LoadCurrentPasswordAsync()
    {
        if (AppSession.CurrentUserId == null) return;
        
        try
        {
            using var connection = _db.CreateConnection();
            var pass = await Dapper.SqlMapper.QueryFirstOrDefaultAsync<string>(
                connection, 
                "SELECT UserPass FROM Users WHERE User_ID = @id", 
                new { id = AppSession.CurrentUserId });
                
            CurrentPassword = pass ?? string.Empty;
        }
        catch 
        {
            // Ignore load errors
        }
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private async System.Threading.Tasks.Task ChangePasswordAsync(string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword))
        {
            System.Windows.MessageBox.Show("الرجاء إدخال كلمة مرور جديدة.", "تنبيه", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        if (AppSession.CurrentUserId == null)
        {
            System.Windows.MessageBox.Show("لا يوجد مستخدم مسجل الدخول حالياً.", "خطأ", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            return;
        }

        try
        {
            using var connection = _db.CreateConnection();
            var sql = "UPDATE Users SET UserPass = @pass WHERE User_ID = @id";
            await Dapper.SqlMapper.ExecuteAsync(connection, sql, new { pass = newPassword, id = AppSession.CurrentUserId });

            System.Windows.MessageBox.Show("تم تغيير كلمة المرور بنجاح.", "تأكيد", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }
        catch (System.Exception ex)
        {
            System.Windows.MessageBox.Show("حدث خطأ أثناء تغيير كلمة المرور: " + ex.Message, "خطأ", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }
}
