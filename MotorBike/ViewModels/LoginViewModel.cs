using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dapper;
using MotorBike.DataAccess;
using MotorBike.Models;

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

    /// <summary>
    /// يُستدعى لما يفشل الاتصال بالـ Database (مش خطأ في اليوزر/باسورد)
    /// </summary>
    public Action? OnConnectionFailed { get; set; }

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
                AppSession.CurrentUserId = (int)user.UserId;
                AppSession.CurrentUserName = (string)user.UserName;

                // Load Permissions
                var userSubs = await connection.QueryAsync<UserSub>("SELECT * FROM User_Sub WHERE UserID = @userId", new { userId = AppSession.CurrentUserId });
                AppSession.UserPermissions.Clear();
                foreach (var sub in userSubs)
                {
                    AppSession.UserPermissions[sub.FrmId] = sub.Ability ?? "";
                }

                OnLoginSuccess?.Invoke();
            }
            else
            {
                ErrorMessage = "اسم المستخدم أو كلمة المرور غير صحيحة، أو الحساب غير نشط.";
            }
        }
        catch (Exception ex) when (IsConnectionError(ex))
        {
            // خطأ في الاتصال بالـ Database → افتح نافذة الإعداد
            ErrorMessage = "تعذّر الاتصال بقاعدة البيانات.";
            OnConnectionFailed?.Invoke();
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

    /// <summary>
    /// يحدد إذا كان الـ Exception ده ناتج عن فشل الاتصال بالـ SQL Server
    /// </summary>
    private static bool IsConnectionError(Exception ex)
    {
        // SqlException بكودات الاتصال الشائعة
        if (ex is Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            return sqlEx.Number is -1 or 2 or 53 or 258 or 40613 or 10053 or 10054 or 10060 or 10061;
        }
        // أو SocketException داخل InnerException
        if (ex.InnerException is System.Net.Sockets.SocketException)
            return true;
        // أو الرسالة بتتكلم عن network/server
        var msg = ex.Message.ToLowerInvariant();
        return msg.Contains("network") || msg.Contains("server") || msg.Contains("connection")
            || msg.Contains("timeout") || msg.Contains("شبكة") || msg.Contains("اتصال");
    }
}
