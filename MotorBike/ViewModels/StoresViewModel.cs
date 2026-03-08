using MotorBike.DataAccess;
using MotorBike.Models;

namespace MotorBike.ViewModels;

public class StoresViewModel : LookupViewModelBase<Store>
{
    public StoresViewModel(IRepository<Store> repository) : base(repository) { }

    protected override object GetEntityId(Store entity) => entity.StoreId;
    protected override bool IsNewRecord(Store entity) => entity.StoreId == 0;
    protected override void SetEntityId(Store entity, int id) => entity.StoreId = id;
}
