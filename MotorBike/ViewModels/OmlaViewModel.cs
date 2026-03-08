using MotorBike.DataAccess;
using MotorBike.Models;

namespace MotorBike.ViewModels;

public partial class OmlaViewModel : LookupViewModelBase<Omla>
{
    public OmlaViewModel(IRepository<Omla> repository) : base(repository) { }

    protected override object GetEntityId(Omla entity) => entity.OmlaId;
    protected override bool IsNewRecord(Omla entity) => entity.OmlaId == 0;
    protected override void SetEntityId(Omla entity, int id) => entity.OmlaId = (byte)id;

    protected override void SetDefaultValues(Omla entity)
    {
        base.SetDefaultValues(entity);
        entity.OmlaRate = 1m;
    }
}
