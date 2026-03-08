using MotorBike.DataAccess;
using MotorBike.Models;

namespace MotorBike.ViewModels;

public class UsersViewModel : LookupViewModelBase<User>
{
    public UsersViewModel(IRepository<User> repository) : base(repository) { }

    protected override object GetEntityId(User entity) => entity.UserId;
    protected override bool IsNewRecord(User entity) => entity.UserId == 0;
    protected override void SetEntityId(User entity, int id) => entity.UserId = id;
}
