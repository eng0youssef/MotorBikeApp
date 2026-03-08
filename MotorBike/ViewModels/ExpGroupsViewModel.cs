using MotorBike.DataAccess;
using MotorBike.Models;

namespace MotorBike.ViewModels;

public class ExpGroupsViewModel : LookupViewModelBase<ExpGroup>
{
    public ExpGroupsViewModel(IRepository<ExpGroup> repository) : base(repository) { }

    protected override object GetEntityId(ExpGroup entity) => entity.GroupId;
    protected override bool IsNewRecord(ExpGroup entity) => entity.GroupId == 0;
    protected override void SetEntityId(ExpGroup entity, int id) => entity.GroupId = id;
}
