using MotorBike.DataAccess;
using MotorBike.Models;

namespace MotorBike.ViewModels;

public class ItemCategoriesViewModel : LookupViewModelBase<ItemCategory>
{
    public ItemCategoriesViewModel(IRepository<ItemCategory> repository) : base(repository) { }

    protected override object GetEntityId(ItemCategory entity) => entity.CatId;
    protected override bool IsNewRecord(ItemCategory entity) => entity.CatId == 0;
    protected override void SetEntityId(ItemCategory entity, int id) => entity.CatId = id;
}
