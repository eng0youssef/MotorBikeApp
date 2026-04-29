using System.Windows;
using CommunityToolkit.Mvvm.Input;
using MotorBike.DataAccess;
using MotorBike.Models;
using MotorBike.Views;

namespace MotorBike.ViewModels;

public partial class UsersViewModel : LookupViewModelBase<User>
{
    private readonly IDbConnectionFactory _db;

    public UsersViewModel(IRepository<User> repository, IDbConnectionFactory db) : base(repository) 
    { 
        _db = db;
    }

    protected override object GetEntityId(User entity) => entity.UserId;
    protected override bool IsNewRecord(User entity) => entity.UserId == 0;
    protected override void SetEntityId(User entity, int id) => entity.UserId = id;

    [RelayCommand]
    private void OpenPermissions()
    {
        if (SelectedItem == null)
        {
            MessageBox.Show("الرجاء تحديد مستخدم أولاً.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var vm = new UserPermissionsViewModel(_db, SelectedItem.UserId, SelectedItem.UserName);
        var win = new UserPermissionsWindow(vm);
        win.ShowDialog();
    }
}
