using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dapper;
using MotorBike.DataAccess;

namespace MotorBike.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly IDbConnectionFactory _db;

    public LoginViewModel(IDbConnectionFactory db)
    {
        _db = db;
    }

    [ObservableProperty]
    private string _userName = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public Action? OnLoginSuccess { get; set; }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(UserName) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "يرجى إدخال اسم المستخدم وكلمة المرور.";
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;

        try
        {
            using var connection = _db.CreateConnection();
            var sql = "SELECT Top 1 User_ID as UserId, UserName FROM Users WHERE UserName = @userName AND UserPass = @password AND Active = 1";
            var user = await connection.QueryFirstOrDefaultAsync<dynamic>(sql, new { userName = UserName, password = Password });

            if (user != null)
            {
                // Login successful
                AppSession.CurrentUserId = (int)user.UserId;
                AppSession.CurrentUserName = (string)user.UserName;
                
                OnLoginSuccess?.Invoke();
            }
            else
            {
                ErrorMessage = "اسم المستخدم أو كلمة المرور غير صحيحة، أو الحساب غير نشط.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = "حدث خطأ أثناء الاتصال بقاعدة البيانات: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
