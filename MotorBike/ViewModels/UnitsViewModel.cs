using MotorBike.DataAccess;
using MotorBike.Models;

namespace MotorBike.ViewModels;

public class UnitsViewModel : LookupViewModelBase<Unit>
{
    public UnitsViewModel(IRepository<Unit> repository) : base(repository) { }

    protected override object GetEntityId(Unit entity) => entity.UnitId;
    protected override bool IsNewRecord(Unit entity) => entity.UnitId == 0;
    protected override void SetEntityId(Unit entity, int id) => entity.UnitId = id;
}
